using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading.Channels;
using PixelBattle.Extensions;
using PixelBattle.Structures;

namespace PixelBattle.WriteAheadLog;

public sealed class WalWriter : IDisposable, IAsyncDisposable
{
    private const uint WriterVersion = 1;

    private readonly TimeProvider _timeProvider;
    private readonly FileStream _walFileStream;
    private readonly Channel<WalAckRecord> _walChannel;
    private readonly Channel<WalRecord> _commitedChannel;
    private Task? _groupedCommitTask;
    private readonly WalWriterOptions _options;
    private readonly int _salt;

    private WalWriter(FileStream walFileStream, int salt, WalWriterOptions options)
    {
        _timeProvider = TimeProvider.System;
        _walFileStream = walFileStream;
        _options = options;
        _salt = salt;
        _walChannel = Channel.CreateBounded<WalAckRecord>(new BoundedChannelOptions(_options.MaxQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = false,
        });
        _commitedChannel = Channel.CreateUnbounded<WalRecord>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = true,
        });
    }

    public async Task AppendAsync(PixelColorInfo colorInfo)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await _walChannel.Writer.WriteAsync(new WalAckRecord(colorInfo, tcs));

        await tcs.Task;
    }

    private async Task GroupedWalCommitAsync()
    {
        var reader = _walChannel.Reader;
        var writer = _commitedChannel.Writer;

        var batch = new List<WalAckRecord>(_options.MaxBatchCommitSize);
        var commitedBatch = new List<WalRecord>(_options.MaxBatchCommitSize);
        var buffer = new byte[WalRecord.BinaryLength + sizeof(uint)];
        while (await reader.WaitToReadAsync())
        {
            while (batch.Count < _options.MaxBatchCommitSize && reader.TryRead(out var record))
            {
                batch.Add(record);
            }

            try
            {
                foreach (var walAckRecord in batch)
                {
                    var walRecord = new WalRecord(
                        _timeProvider.GetUtcNow().Ticks,
                        _salt,
                        walAckRecord.Color
                    );
                    if (!walRecord.TryWriteWithCrc32(buffer, out var written))
                    {
                        walAckRecord.TaskCompletionSource.TrySetException(new InvalidOperationException());
                    }
                    else
                    {
                        Debug.Assert(written == buffer.Length);
                        commitedBatch.Add(walRecord);
                    }
                }

                _walFileStream.Write(buffer);
                _walFileStream.Flush(true);
            }
            catch (Exception e)
            {
                foreach (var record in batch)
                {
                    record.TaskCompletionSource.TrySetException(e);
                }

                continue; //TODO may be throw;
            }

            foreach (var walAckRecord in batch)
            {
                walAckRecord.TaskCompletionSource.TrySetResult();
            }

            foreach (var walRecord in commitedBatch)
            {
                await writer.WriteAsync(walRecord);
            }

            batch.Clear();
            commitedBatch.Clear();
        }
    }

    private void Start()
    {
        _groupedCommitTask = GroupedWalCommitAsync();
    }

    public static WalWriter Create(string path, WalWriterOptions? options = null)
    {
        FileStream? stream = null;
        try
        {
            stream = new FileStream(
                path,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.Read
            );

            var headers = new WalHeaders(
                WalHeaders.DefaultMagicNumber,
                WriterVersion,
                RandomNumberGenerator.GetInt32(int.MaxValue)
            );

            Span<byte> buffer = stackalloc byte[WalHeaders.BinaryLength + sizeof(uint)];
            var written = headers.WriteWithCrc32(buffer);

            Debug.Assert(written == buffer.Length);

            stream.Write(buffer);
            stream.Flush(true);

            var writer = new WalWriter(stream, headers.Salt, options ?? new WalWriterOptions());

            writer.Start();
            return writer;
        }
        catch
        {
            stream?.Dispose();
            throw;
        }
    }

    public static WalWriter OpenOrCreate(string path, WalWriterOptions? options = null)
    {
        FileStream? stream = null;
        try
        {
            stream = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read
            );
            var headersLength = WalHeaders.BinaryLength + sizeof(uint);
            Span<byte> buffer = stackalloc byte[headersLength];

            stream.ReadExactly(buffer);
            var headers = BinaryUtils.ReadWithCrc32Validation<WalHeaders>(buffer);

            if (headers.Version != WriterVersion)
            {
                throw new InvalidOperationException();
            }

            stream.SetLength(stream.Length - (stream.Length - headersLength) % WalRecord.BinaryLength);
            stream.Flush(true);
            stream.Seek(0, SeekOrigin.End);

            return new WalWriter(stream, headers.Salt, options ?? new WalWriterOptions());
        }
        catch
        {
            stream?.Dispose();

            throw;
        }
    }

    private record WalAckRecord(PixelColorInfo Color, TaskCompletionSource TaskCompletionSource);

    public void Dispose()
    {
        _walChannel.Writer.TryComplete();
        _groupedCommitTask?.GetAwaiter().GetResult();

        _walFileStream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _walChannel.Writer.TryComplete();
        if (_groupedCommitTask is not null)
        {
            await _groupedCommitTask;
        }

        await _walFileStream.DisposeAsync();
    }
}
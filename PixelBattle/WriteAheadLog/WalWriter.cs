using System.Threading.Channels;
using PixelBattle.Binary;
using PixelBattle.Structures;

namespace PixelBattle.WriteAheadLog;

public class WalWriter : IDisposable, IAsyncDisposable
{
    private const int WalMaxBatch = 1024;
    private const int WalBufferSize = 4096;
    private const uint Version = 1;

    private readonly TimeProvider _timeProvider;
    private readonly FileStream _walFileStream;
    private readonly Channel<WalAckRecord> _walChannel;
    private readonly Task _groupedCommitTask;

    public WalWriter(FileStream walFileStream)
    {
        _timeProvider = TimeProvider.System;
        _walFileStream = walFileStream;
        _walChannel = Channel.CreateBounded<WalAckRecord>(new BoundedChannelOptions(WalMaxBatch)
        {
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false,
            SingleReader = true,
        });
        _groupedCommitTask = GroupedWalCommitAsync();
    }

    public async Task AppendAsync(UpdateColor record)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await _walChannel.Writer.WriteAsync(new WalAckRecord(record, tcs));

        await tcs.Task;
    }

    private async Task GroupedWalCommitAsync()
    {
        var reader = _walChannel.Reader;

        var batch = new List<WalAckRecord>(WalMaxBatch);
        while (await reader.WaitToReadAsync())
        {
            try
            {
                while (batch.Count < WalMaxBatch && reader.TryRead(out var record))
                {
                    batch.Add(record);
                }

                try
                {
                    foreach (var walAckRecord in batch)
                    {
                        var walRecord = new WalRecord(
                            _timeProvider.GetUtcNow().Ticks,
                            Version,
                            walAckRecord.UpdateColor.X,
                            walAckRecord.UpdateColor.Y,
                            walAckRecord.UpdateColor.Color
                        );
                        _walFileStream.Write(Utils.AsSpan(ref walRecord));
                    }

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
                    walAckRecord.TaskCompletionSource.SetResult();
                }
            }
            finally
            {
                batch.Clear();
            }
        }
    }

    public static WalWriter Create(string path)
    {
        FileStream? stream = null;
        try
        {
            stream = new FileStream(
                path,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.Read,
                WalBufferSize,
                FileOptions.SequentialScan
            );

            return new WalWriter(stream);
        }
        catch
        {
            stream?.Dispose();
            throw;
        }
    }

    public static async Task<WalWriter> OpenOrCreateAsync(string path)
    {
        FileStream? stream = null;
        try
        {
            stream = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read,
                WalBufferSize
            );

            var position = stream.Length / WalRecord.BinaryLength * WalRecord.BinaryLength;

            stream.Seek(position, SeekOrigin.Begin);

            return new WalWriter(stream);
        }
        catch
        {
            if (stream is not null)
            {
                await stream.DisposeAsync();
            }

            throw;
        }
    }

    private record WalAckRecord(UpdateColor UpdateColor, TaskCompletionSource TaskCompletionSource);

    public void Dispose()
    {
        _walChannel.Writer.TryComplete();
        _groupedCommitTask.GetAwaiter().GetResult();

        _walFileStream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _walChannel.Writer.TryComplete();
        await _groupedCommitTask;

        await _walFileStream.DisposeAsync();
    }
}
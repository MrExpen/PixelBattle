using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using System.Threading.Tasks.Sources;
using PixelBattle.Binary;
using PixelBattle.Structures;

namespace PixelBattle;

public class WriteAheadLog : IAsyncDisposable, IDisposable
{
    private const long WalMmfScanCountThreshold = 1024 * 1024;
    private const int ScanLastCount = 5;
    private const int WalBufferSize = 4096;
    private const uint Version = 1;

    //TODO get from constructor
    private const int WalMaxBatch = 4096;
    private const double BatchIntervalMicroseconds = 0;

    private readonly TimeProvider _timeProvider;
    private readonly FileStream _walFileStream;
    private readonly Channel<WalAckRecord> _walChannel;
    private readonly Channel<WalRecord> _commitedChannel;
    private readonly Task _groupedCommitTask;

    public ChannelReader<WalRecord> CommitedReader => _commitedChannel.Reader;

    private WriteAheadLog(FileStream walFileStream, Channel<WalRecord> commitedChannel)
    {
        _walFileStream = walFileStream;
        _timeProvider = TimeProvider.System;
        _walChannel = Channel.CreateBounded<WalAckRecord>(new BoundedChannelOptions(WalMaxBatch)
        {
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false,
            SingleReader = true,
        });
        _commitedChannel = commitedChannel;
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
        var writer = _commitedChannel.Writer;
        var deltaFreq = (long)(BatchIntervalMicroseconds * TimeSpan.TicksPerSecond * TimeSpan.TicksPerMicrosecond /
                               _timeProvider.TimestampFrequency);

        var batch = new List<WalAckRecord>(WalMaxBatch);
        var commitedBatch = new List<WalRecord>(WalMaxBatch);
        while (await reader.WaitToReadAsync())
        {
            try
            {
                var startTimestamp = _timeProvider.GetTimestamp();
                do
                {
                    while (batch.Count < WalMaxBatch && reader.TryRead(out var record))
                    {
                        batch.Add(record);
                    }
                } while (_timeProvider.GetTimestamp() - startTimestamp < deltaFreq && batch.Count < WalMaxBatch);


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
                        _walFileStream.Write(Utils.ToSpan(ref walRecord));
                        commitedBatch.Add(walRecord);
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

                foreach (var walRecord in commitedBatch)
                {
                    await writer.WriteAsync(walRecord);
                }
            }
            finally
            {
                batch.Clear();
                commitedBatch.Clear();
            }
        }
    }

    public static WriteAheadLog Create(string path)
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

            var channel = Channel.CreateUnbounded<WalRecord>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
            });

            return new WriteAheadLog(stream, channel);
        }
        catch
        {
            stream?.Dispose();
            throw;
        }
    }

    public static async Task<WriteAheadLog> OpenOrCreateAsync(string path, long lastAppliedTimestamp)
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

            var channel = Channel.CreateUnbounded<WalRecord>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
            });

            var commitedIndex = GetWalCommitedIndex(stream, lastAppliedTimestamp);

            stream.Seek(commitedIndex * WalRecord.BinaryLength, SeekOrigin.Begin);

            var writer = channel.Writer;
            while (stream.Position + WalRecord.BinaryLength <= stream.Length)
            {
                WalRecord record = default;
                stream.ReadExactly(Utils.ToSpan(ref record));
                await writer.WriteAsync(record);
            }

            return new WriteAheadLog(stream, channel);
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

    private static long GetWalCommitedIndex(FileStream stream, long lastAppliedTimestamp)
    {
        var count = stream.Length / WalRecord.BinaryLength;
        if (count == 0)
            return 0;

        //Check few last records to speedup
        {
            var lastCount = (int)Math.Min(ScanLastCount, count);
            Span<byte> buffer = stackalloc byte[lastCount * WalRecord.BinaryLength];
            stream.Seek((count - lastCount) * WalRecord.BinaryLength, SeekOrigin.Begin);
            stream.ReadExactly(buffer);
            for (var i = 0; i < lastCount; i++)
            {
                var timespan = MemoryMarshal.Read<long>(buffer.Slice((lastCount - i - 1) * WalRecord.BinaryLength));
                if (timespan <= lastAppliedTimestamp)
                {
                    return count - i;
                }
            }
        }

        long l = 0;
        long r = count;

        while (r - l + 1 > WalMmfScanCountThreshold)
        {
            var m = l + (r - l) / 2;
            stream.Seek(m * WalRecord.BinaryLength, SeekOrigin.Begin);
            long mV = 0;
            stream.ReadExactly(Utils.ToSpan(ref mV, Marshal.SizeOf<long>()));
            if (mV <= lastAppliedTimestamp)
            {
                l = m + 1;
            }
            else
            {
                r = m;
            }
        }

        using var mmf = MemoryMappedFile.CreateFromFile(stream, null, 0,
            MemoryMappedFileAccess.ReadWrite,
            HandleInheritability.None, true);
        using var accessor = mmf.CreateViewAccessor(0, count * WalRecord.BinaryLength);

        while (l < r)
        {
            var m = l + (r - l) / 2;
            var mV = accessor.ReadInt64(m * WalRecord.BinaryLength);
            if (mV <= lastAppliedTimestamp)
            {
                l = m + 1;
            }
            else
            {
                r = m;
            }
        }

        return l;
    }

    private record WalAckRecord(UpdateColor UpdateColor, TaskCompletionSource TaskCompletionSource);

    public void Dispose()
    {
        _walChannel.Writer.TryComplete();
        _groupedCommitTask.GetAwaiter().GetResult();
        _commitedChannel.Writer.TryComplete();

        _walFileStream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _walChannel.Writer.TryComplete();
        await _groupedCommitTask;
        _commitedChannel.Writer.TryComplete();

        await _walFileStream.DisposeAsync();
    }
}
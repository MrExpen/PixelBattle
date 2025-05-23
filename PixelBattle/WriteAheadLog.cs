using System.Threading.Channels;
using PixelBattle.Binary;
using PixelBattle.Structures;

namespace PixelBattle;

public class WriteAheadLog : IAsyncDisposable, IDisposable
{
    private const int WalBufferSize = 4096;
    private const int WalMaxBatch = 100;
    private const double BatchIntervalMicroseconds = 0;
    private const uint Version = 1;

    private readonly TimeProvider _timeProvider;
    private readonly FileStream _walFileStream;
    private readonly Channel<WalAckRecord> _walChannel;
    private readonly List<WalRecord> _commitedList;
    private readonly Task _groupedCommitTask;

    private WriteAheadLog(FileStream walFileStream, List<WalRecord> commitedList)
    {
        _walFileStream = walFileStream;
        _timeProvider = TimeProvider.System;
        _walChannel = Channel.CreateBounded<WalAckRecord>(new BoundedChannelOptions(WalMaxBatch)
        {
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false,
            SingleReader = true,
        });
        _commitedList = commitedList;
        _groupedCommitTask = GroupedWalCommitAsync(CancellationToken.None);
    }

    public async Task AppendAsync(UpdateColor record)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await _walChannel.Writer.WriteAsync(new WalAckRecord(record, tcs));

        await tcs.Task;
    }

    private async Task GroupedWalCommitAsync(CancellationToken cancellationToken)
    {
        var reader = _walChannel.Reader;
        var deltaFreq = (long)(BatchIntervalMicroseconds * TimeSpan.TicksPerSecond * TimeSpan.TicksPerMicrosecond /
                               _timeProvider.TimestampFrequency);

        var batch = new List<WalAckRecord>(WalMaxBatch);
        var commitedBatch = new List<WalRecord>(WalMaxBatch);
        while (!cancellationToken.IsCancellationRequested && await reader.WaitToReadAsync(cancellationToken))
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

                _commitedList.AddRange(commitedBatch);
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

            return new WriteAheadLog(stream, []);
        }
        catch
        {
            stream?.Dispose();
            throw;
        }
    }

    public static WriteAheadLog OpenOrCreate(string path)
    {
        FileStream? stream = null;
        try
        {
            stream = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read,
                WalBufferSize,
                FileOptions.SequentialScan
            );

            var list = new List<WalRecord>();
            WalRecord record = default;
            var recordSpan = Utils.ToSpan(ref record);
            while (stream.Position + WalRecord.BinaryLength <= stream.Length)
            {
                stream.ReadExactly(recordSpan);
                list.Add(record);
            }

            return new WriteAheadLog(stream, list);
        }
        catch
        {
            stream?.Dispose();
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
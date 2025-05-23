using System.Threading.Channels;
using PixelBattle.Structures;

namespace PixelBattle;

public class WriteAheadLog : IAsyncDisposable, IDisposable
{
    private const int WalBufferSize = 4096;
    private const int WalMaxBatch = 100;
    private const uint Version = 1;

    private readonly TimeProvider _timeProvider;
    private readonly FileStream _walFileStream;
    private readonly Channel<WalAckRecord> _walChannel;
    private readonly Channel<CommitedWalRecord> _commitedChannel;
    private readonly Task _groupedCommitTask;

    private WriteAheadLog(FileStream walFileStream)
    {
        _walFileStream = walFileStream;
        _timeProvider = TimeProvider.System;
        _walChannel = Channel.CreateBounded<WalAckRecord>(new BoundedChannelOptions(WalMaxBatch)
        {
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false,
            SingleReader = true,
        });
        _commitedChannel = Channel.CreateUnbounded<CommitedWalRecord>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = true
        });
        _groupedCommitTask = GroupedWalCommitAsync(CancellationToken.None);
    }

    public async Task Add(ChangeOneColorRecord record)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await _walChannel.Writer.WriteAsync(new WalAckRecord(OperationType.ChangeOne, record, tcs));

        await tcs.Task;
    }

    private async Task GroupedWalCommitAsync(CancellationToken cancellationToken)
    {
        var reader = _walChannel.Reader;
        var writer = _commitedChannel.Writer;

        var batch = new List<WalAckRecord>(WalMaxBatch);
        var buffer = new byte[WalRecordHeaders.BinaryLength + ChangeOneColorRecord.BinaryLength];
        while (!cancellationToken.IsCancellationRequested && await reader.WaitToReadAsync(cancellationToken))
        {
            //TODO Add delay to batch
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
                        //TODO add try catch inside
                        var headers = new WalRecordHeaders(
                            _timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
                            Version,
                            walAckRecord.Operation
                        );
                        var written = headers.Write(buffer);

                        switch (walAckRecord.Operation)
                        {
                            case OperationType.Sync:
                                break;
                            case OperationType.ChangeOne:
                                written += walAckRecord.ChangeOneColorRecord!.Value.Write(buffer.AsSpan(written));
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        _walFileStream.Write(buffer, 0, written);
                    }

                    _walFileStream.Flush(true);
                }
                catch (Exception e)
                {
                    foreach (var record in batch)
                    {
                        record.TaskCompletionSource.TrySetException(e);
                    }

                    continue;
                }

                foreach (var walAckRecord in batch)
                {
                    walAckRecord.TaskCompletionSource.SetResult();

                    if (walAckRecord.Operation == OperationType.ChangeOne)
                    {
                        await writer.WriteAsync(new CommitedWalRecord(walAckRecord.ChangeOneColorRecord!.Value));
                    }
                }
            }
            finally
            {
                batch.Clear();
            }
        }
    }

    public static WriteAheadLog CreateNew(string path)
    {
        FileStream? stream = null;
        try
        {
            stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.Read,
                WalBufferSize,
                FileOptions.SequentialScan
            );

            return new WriteAheadLog(stream);
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

            //Todo read

            return new WriteAheadLog(stream);
        }
        catch
        {
            stream?.Dispose();
            throw;
        }
    }

    public static WriteAheadLog Open(string path)
    {
        FileStream? stream = null;
        try
        {
            stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.Read,
                WalBufferSize,
                FileOptions.SequentialScan
            );

            //Todo read

            return new WriteAheadLog(stream);
        }
        catch
        {
            stream?.Dispose();
            throw;
        }
    }

    private record WalAckRecord(
        OperationType Operation,
        ChangeOneColorRecord? ChangeOneColorRecord,
        TaskCompletionSource TaskCompletionSource
    );

    public record CommitedWalRecord(ChangeOneColorRecord ChangeOneColorRecord);

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
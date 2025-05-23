using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using PixelBattle.Binary;
using PixelBattle.Structures;

namespace PixelBattle;

public sealed class PixelBattleDatabase : IDisposable, IAsyncDisposable
{
    private const int WalBufferSize = 4096;
    private const int WalMaxBatch = 100;

    private static readonly byte[] MagicBytes = "PBDFEXPN"u8.ToArray();
    private static ulong MagicNumber => MemoryMarshal.Read<ulong>(MagicBytes);
    private const uint CurrentVersion = 1;

    private readonly int _width;
    private readonly int _height;

    private readonly TimeProvider _timeProvider;

    private readonly FileStream _dbFileStream;
    private readonly FileStream _walFileStream;
    private readonly MemoryMappedFile _memoryMappedFile;
    private readonly MemoryMappedViewAccessor _accessor;

    private readonly Channel<WalAckRecord> _walChannel;

    private PixelBattleDatabase(FileStream dbFileStream, FileStream walFileStream, int width, int height)
    {
        _timeProvider = TimeProvider.System;
        _dbFileStream = dbFileStream;
        _walFileStream = walFileStream;
        _width = width;
        _height = height;
        //Todo remove magic number
        _walChannel = Channel.CreateBounded<WalAckRecord>(new BoundedChannelOptions(WalMaxBatch)
        {
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false,
            SingleReader = true,
        });
        _ = GroupedWalCommitAsync(CancellationToken.None);

        _memoryMappedFile = MemoryMappedFile.CreateFromFile(_dbFileStream, null, 0, MemoryMappedFileAccess.ReadWrite,
            HandleInheritability.None, true);
        _accessor = _memoryMappedFile.CreateViewAccessor(_dbFileStream.Position,
            _dbFileStream.Length - _dbFileStream.Position,
            MemoryMappedFileAccess.ReadWrite);
    }

    public async Task SetAsync(int x, int y, byte color)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, _width);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, _height);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await _walChannel.Writer.WriteAsync(
            new WalAckRecord(
                OperationType.ChangeOne,
                new ChangeOneColorRecord(x, y, color),
                tcs
            )
        );

        await tcs.Task;
    }

    private async Task GroupedWalCommitAsync(CancellationToken cancellationToken)
    {
        var reader = _walChannel.Reader;

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
                            CurrentVersion,
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
                }
            }
            finally
            {
                batch.Clear();
            }
        }
    }

    public void Dispose()
    {
        _walChannel.Writer.TryComplete();
        _walFileStream.Dispose();
        _accessor.Dispose();
        _memoryMappedFile.Dispose();
        _dbFileStream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _walChannel.Writer.TryComplete();
        await _walFileStream.DisposeAsync();
        _accessor.Dispose();
        _memoryMappedFile.Dispose();
        await _dbFileStream.DisposeAsync();
    }

    public static PixelBattleDatabase Create(string path, int width, int height)
    {
        Debug.Assert(MagicBytes.Length == Marshal.SizeOf<ulong>());
        FileStream? dbStream = null, walStream = null;

        try
        {
            dbStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);

            dbStream.SetLength(DbHeaders.BinaryLength + width * height);

            var headers = new DbHeaders(MagicNumber, CurrentVersion, width, height);

            dbStream.Write(Utils.ToSpan(ref headers));

            walStream = new FileStream(
                GetWalFileName(path),
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.Read,
                WalBufferSize,
                FileOptions.SequentialScan
            );

            return new PixelBattleDatabase(
                dbStream,
                walStream,
                width,
                height
            );
        }
        catch
        {
            dbStream?.Dispose();
            walStream?.Dispose();

            throw;
        }
    }

    public static PixelBattleDatabase Open(string path)
    {
        Debug.Assert(MagicBytes.Length == Marshal.SizeOf<ulong>());

        FileStream? dbStream = null, walStream = null;

        try
        {
            dbStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

            DbHeaders dbHeaders = default;
            dbStream.ReadExactly(Utils.ToSpan(ref dbHeaders));

            if (dbHeaders.MagicNumber != MagicNumber)
            {
                throw new InvalidOperationException("Not supported file");
            }

            if (dbHeaders.Version != CurrentVersion)
            {
                throw new InvalidOperationException("Version not supported");
            }

            walStream = new FileStream(
                GetWalFileName(path),
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.Read,
                WalBufferSize,
                FileOptions.SequentialScan
            );

            return new PixelBattleDatabase(
                dbStream,
                walStream,
                dbHeaders.Width,
                dbHeaders.Height
            );
        }
        catch
        {
            dbStream?.Dispose();
            walStream?.Dispose();

            throw;
        }
    }

    private static string GetWalFileName(string dbName)
    {
        return dbName + "-wal";
    }

    private record WalAckRecord(
        OperationType Operation,
        ChangeOneColorRecord? ChangeOneColorRecord,
        TaskCompletionSource TaskCompletionSource);
}
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PixelBattle.Binary;
using PixelBattle.Structures;

namespace PixelBattle;

public sealed class PixelBattleDatabase : IDisposable, IAsyncDisposable
{
    private static readonly byte[] MagicBytes = "PBDFEXPN"u8.ToArray();
    private static ulong MagicNumber => MemoryMarshal.Read<ulong>(MagicBytes);
    private const uint CurrentVersion = 1;
    private const int MaxApplyBatchSize = 1000;

    public int Width { get; }
    public int Height { get; }

    public int ChunkSize { get; }

    public static int ChunkMetadataSize => Marshal.SizeOf<long>();

    public int ChunkSizeWithMetadata => ChunkSize + ChunkMetadataSize;

    public int ChunksCount => _chunkLocks.Length;

    private readonly Lock[] _chunkLocks;
    private readonly WriteAheadLog _writeAheadLog;
    private readonly FileStream _dbFileStream;
    private readonly MemoryMappedFile _memoryMappedFile;
    private readonly MemoryMappedViewAccessor _accessor;
    private Task? _applyWalTask;

    private PixelBattleDatabase(
        FileStream dbFileStream,
        WriteAheadLog writeAheadLog,
        MemoryMappedFile memoryMappedFile,
        MemoryMappedViewAccessor accessor,
        int width,
        int height,
        int chunkSize
    )
    {
        _dbFileStream = dbFileStream;
        _memoryMappedFile = memoryMappedFile;
        _accessor = accessor;
        _writeAheadLog = writeAheadLog;
        Width = width;
        Height = height;
        ChunkSize = chunkSize;
        _chunkLocks = new Lock[Width * Height / ChunkSize];
        for (var i = 0; i < _chunkLocks.Length; i++)
        {
            _chunkLocks[i] = new Lock();
        }
    }

    public async Task SetAsync(int x, int y, byte color)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);

        await _writeAheadLog.AppendAsync(new UpdateColor(x, y, color));
    }

    public byte[] GetChunk(int chunkNumber)
    {
        var buffer = new byte[ChunkSizeWithMetadata];
        WriteChunk(buffer, chunkNumber);
        return buffer;
    }

    public void WriteChunk(Span<byte> buffer, int chunkNumber)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(chunkNumber, ChunksCount);

        if (buffer.Length < ChunkSizeWithMetadata)
            throw new InvalidOperationException();

        lock (_chunkLocks[chunkNumber])
        {
            _accessor.SafeMemoryMappedViewHandle.ReadSpan(
                (ulong)(DbHeaders.BinaryLength + ChunkSizeWithMetadata * chunkNumber), buffer[..ChunkSizeWithMetadata]);
        }
    }

    private void ApplyWalOnce()
    {
        var reader = _writeAheadLog.CommitedReader;
        long lastAppliedTimestamp = -1;
        while (reader.TryRead(out var record))
        {
            UpdateChunk(ref record);
            lastAppliedTimestamp = record.Timestamp;
        }

        if (lastAppliedTimestamp <= 0)
            return;

        _accessor.Write(DbHeaders.LastAppliedTimestampOffset, lastAppliedTimestamp);
        _accessor.Flush();
    }

    private async Task ApplyWalLoopAsync()
    {
        var reader = _writeAheadLog.CommitedReader;

        while (await reader.WaitToReadAsync())
        {
            long lastAppliedTimestamp = -1;
            for (var batchSize = 0; batchSize < MaxApplyBatchSize && reader.TryRead(out var record); batchSize++)
            {
                UpdateChunkWithLock(ref record);
                lastAppliedTimestamp = record.Timestamp;
            }

            if (lastAppliedTimestamp <= 0)
                continue;

            _accessor.Write(DbHeaders.LastAppliedTimestampOffset, lastAppliedTimestamp);
            _accessor.Flush();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateChunk(ref WalRecord record)
    {
        var offset = record.Y * Width + record.X;
        var chunkNumber = offset / ChunkSize;
        var globalOffset = DbHeaders.BinaryLength + (chunkNumber + 1) * ChunkMetadataSize + offset;

        _accessor.Write(globalOffset, record.Color);
        _accessor.Write(DbHeaders.BinaryLength + chunkNumber * ChunkSizeWithMetadata, record.Timestamp);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateChunkWithLock(ref WalRecord record)
    {
        var offset = record.Y * Width + record.X;
        var chunkNumber = offset / ChunkSize;
        var globalOffset = DbHeaders.BinaryLength + (chunkNumber + 1) * ChunkMetadataSize + offset;

        lock (_chunkLocks[chunkNumber])
        {
            _accessor.Write(globalOffset, record.Color);
            _accessor.Write(DbHeaders.BinaryLength + chunkNumber * ChunkSizeWithMetadata, record.Timestamp);
        }
    }

    private void Start()
    {
        if (_applyWalTask is not null)
            return;

        _applyWalTask = ApplyWalLoopAsync();
    }

    public void Dispose()
    {
        _writeAheadLog.Dispose();
        _applyWalTask?.GetAwaiter().GetResult();

        _accessor.Dispose();
        _memoryMappedFile.Dispose();
        _dbFileStream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _writeAheadLog.DisposeAsync();
        if (_applyWalTask is not null)
        {
            await _applyWalTask;
        }

        _accessor.Dispose();
        _memoryMappedFile.Dispose();
        await _dbFileStream.DisposeAsync();
    }

    public static PixelBattleDatabase Create(string path, int width, int height, int chunkSize)
    {
        Debug.Assert(MagicBytes.Length == Marshal.SizeOf<ulong>());
        FileStream? dbStream = null;
        WriteAheadLog? wal = null;
        MemoryMappedFile? memoryMappedFile = null;
        MemoryMappedViewAccessor? accessor = null;
        PixelBattleDatabase? pixelBattleDatabase = null;
        try
        {
            if ((width * height) % chunkSize != 0)
                throw new ArgumentException("size of canvas must devide by chunk size", nameof(chunkSize));

            dbStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);

            dbStream.SetLength(DbHeaders.BinaryLength + width * height +
                               width * height / chunkSize * ChunkMetadataSize);

            var headers = new DbHeaders(MagicNumber, 0, CurrentVersion, width, height, chunkSize);

            dbStream.Write(Utils.ToSpan(ref headers));

            wal = WriteAheadLog.Create(GetWalFileName(path));

            memoryMappedFile = MemoryMappedFile.CreateFromFile(dbStream, null, 0, MemoryMappedFileAccess.ReadWrite,
                HandleInheritability.None, true);
            accessor = memoryMappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);

            pixelBattleDatabase = new PixelBattleDatabase(
                dbStream,
                wal,
                memoryMappedFile,
                accessor,
                width,
                height,
                chunkSize
            );

            pixelBattleDatabase.Start();

            return pixelBattleDatabase;
        }
        catch
        {
            if (pixelBattleDatabase is not null)
            {
                pixelBattleDatabase.Dispose();
            }
            else
            {
                wal?.Dispose();
                accessor?.Dispose();
                memoryMappedFile?.Dispose();
                dbStream?.Dispose();
            }

            throw;
        }
    }

    public static async Task<PixelBattleDatabase> OpenAsync(string path)
    {
        Debug.Assert(MagicBytes.Length == Marshal.SizeOf<ulong>());

        FileStream? dbStream = null;
        WriteAheadLog? wal = null;
        MemoryMappedFile? memoryMappedFile = null;
        MemoryMappedViewAccessor? accessor = null;
        PixelBattleDatabase? pixelBattleDatabase = null;
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

            if ((dbHeaders.Width * dbHeaders.Height) % dbHeaders.ChunkSize != 0)
            {
                throw new InvalidOperationException();
            }

            if ((DbHeaders.BinaryLength + dbHeaders.Width * dbHeaders.Height + dbHeaders.Width * dbHeaders.Height /
                    dbHeaders.ChunkSize * ChunkMetadataSize) != dbStream.Length)
            {
                throw new InvalidOperationException();
            }

            wal = await WriteAheadLog.OpenOrCreateAsync(GetWalFileName(path), dbHeaders.LastAppliedTimestamp);


            memoryMappedFile = MemoryMappedFile.CreateFromFile(dbStream, null, 0, MemoryMappedFileAccess.ReadWrite,
                HandleInheritability.None, true);
            accessor = memoryMappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);

            pixelBattleDatabase = new PixelBattleDatabase(
                dbStream,
                wal,
                memoryMappedFile,
                accessor,
                dbHeaders.Width,
                dbHeaders.Height,
                dbHeaders.ChunkSize
            );

            pixelBattleDatabase.ApplyWalOnce();
            pixelBattleDatabase.Start();

            return pixelBattleDatabase;
        }
        catch
        {
            if (pixelBattleDatabase is not null)
            {
                await pixelBattleDatabase.DisposeAsync();
            }
            else
            {
                if (wal is not null)
                {
                    await wal.DisposeAsync();
                }

                accessor?.Dispose();
                memoryMappedFile?.Dispose();

                if (dbStream is not null)
                {
                    await dbStream.DisposeAsync();
                }
            }

            throw;
        }
    }

    private static string GetWalFileName(string dbName)
    {
        return dbName + "-wal";
    }
}
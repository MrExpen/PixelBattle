using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using PixelBattle.Binary;
using PixelBattle.Structures;

namespace PixelBattle;

public sealed class PixelBattleDatabase : IDisposable, IAsyncDisposable
{
    private static readonly byte[] MagicBytes = "PBDFEXPN"u8.ToArray();
    private static ulong MagicNumber => MemoryMarshal.Read<ulong>(MagicBytes);
    private const uint CurrentVersion = 1;

    private readonly int _width;
    private readonly int _height;

    private readonly WriteAheadLog _writeAheadLog;
    private readonly FileStream _dbFileStream;
    private readonly MemoryMappedFile _memoryMappedFile;
    private readonly MemoryMappedViewAccessor _accessor;

    private PixelBattleDatabase(FileStream dbFileStream, WriteAheadLog writeAheadLog, int width, int height)
    {
        _dbFileStream = dbFileStream;
        _writeAheadLog = writeAheadLog;
        _width = width;
        _height = height;

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

        await _writeAheadLog.Add(new ChangeOneColorRecord(x, y, color));
    }

    public void Dispose()
    {
        _writeAheadLog.Dispose();
        _accessor.Dispose();
        _memoryMappedFile.Dispose();
        _dbFileStream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _writeAheadLog.DisposeAsync();
        _accessor.Dispose();
        _memoryMappedFile.Dispose();
        await _dbFileStream.DisposeAsync();
    }

    public static PixelBattleDatabase Create(string path, int width, int height)
    {
        Debug.Assert(MagicBytes.Length == Marshal.SizeOf<ulong>());
        FileStream? dbStream = null;
        WriteAheadLog? wal = null;

        try
        {
            dbStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);

            dbStream.SetLength(DbHeaders.BinaryLength + width * height);

            var headers = new DbHeaders(MagicNumber, CurrentVersion, width, height);

            dbStream.Write(Utils.ToSpan(ref headers));

            wal = WriteAheadLog.CreateNew(GetWalFileName(path));

            return new PixelBattleDatabase(
                dbStream,
                wal,
                width,
                height
            );
        }
        catch
        {
            dbStream?.Dispose();
            wal?.Dispose();

            throw;
        }
    }

    public static PixelBattleDatabase Open(string path)
    {
        Debug.Assert(MagicBytes.Length == Marshal.SizeOf<ulong>());

        FileStream? dbStream = null;
        WriteAheadLog? wal = null;
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

            wal = WriteAheadLog.OpenOrCreate(path);

            return new PixelBattleDatabase(
                dbStream,
                wal,
                dbHeaders.Width,
                dbHeaders.Height
            );
        }
        catch
        {
            dbStream?.Dispose();
            wal?.Dispose();

            throw;
        }
    }

    private static string GetWalFileName(string dbName)
    {
        return dbName + "-wal";
    }
}
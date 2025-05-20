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

    private readonly uint _width;
    private readonly uint _height;

    private readonly Lock _walLock;

    private readonly FileStream _dbFileStream;
    private readonly FileStream _walFileStream;
    private readonly MemoryMappedFile _memoryMappedFile;
    private readonly MemoryMappedViewAccessor _accessor;

    private PixelBattleDatabase(FileStream dbFileStream, FileStream walFileStream, uint width, uint height)
    {
        _dbFileStream = dbFileStream;
        _walFileStream = walFileStream;
        _walLock = new Lock();
        _width = width;
        _height = height;

        _memoryMappedFile = MemoryMappedFile.CreateFromFile(_dbFileStream, null, 0, MemoryMappedFileAccess.ReadWrite,
            HandleInheritability.None, true);
        _accessor = _memoryMappedFile.CreateViewAccessor(_dbFileStream.Position,
            _dbFileStream.Length - _dbFileStream.Position,
            MemoryMappedFileAccess.ReadWrite);
    }

    public void Set(int x, int y, byte color)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)x, _width);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)y, _height);
        
        lock (_walLock)
        {
            var walRecord = new WalRecord(TimeProvider.System.GetTimestamp(), (uint)x, (uint)y, color);
            _walFileStream.Write(Utils.ToSpan(ref walRecord));
            _walFileStream.Flush(true);
            
            var offset = y * _width + x;
            _accessor.Write(offset, color);
            _accessor.Flush();
        }
    }

    public void Dispose()
    {
        _walFileStream.Dispose();
        _accessor.Dispose();
        _memoryMappedFile.Dispose();
        _dbFileStream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _walFileStream.DisposeAsync();
        _accessor.Dispose();
        _memoryMappedFile.Dispose();
        await _dbFileStream.DisposeAsync();
    }

    public static PixelBattleDatabase Create(string path, uint width, uint height)
    {
        Debug.Assert(MagicBytes.Length == Marshal.SizeOf<ulong>());
        FileStream? dbStream = null, walStream = null;

        try
        {
            dbStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);

            dbStream.SetLength(Headers.BinaryLength + width * height);

            var headers = new Headers(MagicNumber, CurrentVersion, width, height);

            dbStream.Write(Utils.ToSpan(ref headers));

            walStream = new FileStream(
                GetWalFileName(path),
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.Read
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

            Headers headers = default;
            dbStream.ReadExactly(Utils.ToSpan(ref headers));

            if (headers.MagicNumber != MagicNumber)
            {
                throw new InvalidOperationException("Not supported file");
            }

            if (headers.Version != CurrentVersion)
            {
                throw new InvalidOperationException("Version not supported");
            }

            walStream = new FileStream(
                GetWalFileName(path),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read
            );

            return new PixelBattleDatabase(
                dbStream,
                walStream,
                headers.Width,
                headers.Height
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
}
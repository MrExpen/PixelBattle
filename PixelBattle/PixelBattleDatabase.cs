using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using PixelBattle.Structures;

namespace PixelBattle;

public sealed class PixelBattleDatabase : IDisposable, IAsyncDisposable
{
    private static readonly byte[] MagicBytes = "PBDFEXPN"u8.ToArray();
    private static ulong MagicNumber => MemoryMarshal.Read<ulong>(MagicBytes);
    private static readonly int SizeOfRaw = Marshal.SizeOf<DatabaseRecord>();
    private const uint CurrentVersion = 1;

    private readonly uint _width;
    private readonly uint _height;

    private readonly ConcurrentQueue<DatabaseRecord> _concurrentQueue = new();
    
    private readonly FileStream _fileStream;
    private readonly MemoryMappedFile _memoryMappedFile;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly WriteAheadLog _writeAheadLog;

    private PixelBattleDatabase(FileStream fileStream, WriteAheadLog wal, uint width, uint height)
    {
        _fileStream = fileStream;
        _writeAheadLog = wal;
        _width = width;
        _height = height;
        
        _memoryMappedFile = MemoryMappedFile.CreateFromFile(_fileStream, null, 0, MemoryMappedFileAccess.ReadWrite,
            HandleInheritability.None, true);
        _accessor = _memoryMappedFile.CreateViewAccessor(_fileStream.Position,
            _fileStream.Length - _fileStream.Position,
            MemoryMappedFileAccess.ReadWrite);
    }

    public void Dispose()
    {
        _writeAheadLog.Dispose();
        _memoryMappedFile.Dispose();
        _fileStream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _writeAheadLog.Dispose();
        _memoryMappedFile.Dispose();
        await _fileStream.DisposeAsync();
    }

    public static PixelBattleDatabase Create(string path, uint width, uint height)
    {
        Debug.Assert(MagicBytes.Length == Marshal.SizeOf<ulong>());

        var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        var headers = new Headers(MagicNumber, CurrentVersion, width, height);
        Span<byte> buff = stackalloc byte[Marshal.SizeOf<Headers>()];
        MemoryMarshal.Write(buff, headers);
        fs.Write(buff);

        fs.SetLength(width * height * SizeOfRaw + Marshal.SizeOf<Headers>()); // 12 - Version + Width + Height
        return new PixelBattleDatabase(fs, new WriteAheadLog(GetWalFileName(path)), width, height);
    }

    public static PixelBattleDatabase Open(string path)
    {
        Debug.Assert(MagicBytes.Length == Marshal.SizeOf<ulong>());

        var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Span<byte> buff = stackalloc byte[Marshal.SizeOf<Headers>()];
        fs.ReadExactly(buff);
        var headers = MemoryMarshal.Read<Headers>(buff);

        if (headers.MagicNumber != MagicNumber)
        {
            throw new InvalidOperationException("Not supported file");
        }

        if (headers.Version != CurrentVersion)
        {
            throw new InvalidOperationException("Version not supported");
        }

        return new PixelBattleDatabase(fs, new WriteAheadLog(GetWalFileName(path)), headers.Width, headers.Height);
    }

    private static string GetWalFileName(string dbName)
    {
        return dbName + "-wal";
    }
}
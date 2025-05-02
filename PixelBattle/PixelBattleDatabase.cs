using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace PixelBattle;

public sealed class PixelBattleDatabase : IDisposable, IAsyncDisposable
{
    private static readonly byte[] MagicBytes = "PBDFEXPN"u8.ToArray();
    private static readonly long MagicNumber = MemoryMarshal.Read<long>(MagicBytes);
    private const int CurrentVersion = 1;

    private const int BytesForColor = 1;
    private const int PageSize = 4096;

    private readonly FileStream _fileStream;
    private readonly MemoryMappedFile _memoryMappedFile;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly WriteAheadLog _writeAheadLog;

    private PixelBattleDatabase(FileStream fileStream, WriteAheadLog wal, int width, int height)
    {
        _fileStream = fileStream;
        _writeAheadLog = wal;
        _memoryMappedFile = MemoryMappedFile.CreateFromFile(_fileStream, null, 0, MemoryMappedFileAccess.ReadWrite,
            HandleInheritability.None, true);
        _accessor = _memoryMappedFile.CreateViewAccessor(_fileStream.Position,
            _fileStream.Length - _fileStream.Position,
            MemoryMappedFileAccess.ReadWrite);
    }

    public void Set(int width, int height, byte color)
    {
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

    public static PixelBattleDatabase Create(string path, int width, int height)
    {
        Debug.Assert(MagicBytes.Length == Marshal.SizeOf<long>());

        var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        fs.Write(MagicBytes); // Magic number (4 bytes)
        Span<byte> buff = stackalloc byte[4];

        MemoryMarshal.Write(buff, CurrentVersion);
        fs.Write(buff); // Version (4 bytes)

        MemoryMarshal.Write(buff, width);
        fs.Write(buff); // Width (4 bytes)

        MemoryMarshal.Write(buff, height);
        fs.Write(buff); // Height (4 bytes)

        fs.SetLength(width * height * BytesForColor + 12 + MagicBytes.Length); // 12 - Version + Width + Height
        return new PixelBattleDatabase(fs, new WriteAheadLog(GetWalFileName(path)), width, height);
    }

    public static PixelBattleDatabase Open(string path)
    {
        Debug.Assert(MagicBytes.Length == Marshal.SizeOf<long>());

        var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Span<byte> buff = stackalloc byte[MagicBytes.Length + 4];

        fs.ReadExactly(buff);
        var magicNumber = MemoryMarshal.Read<long>(buff[..MagicBytes.Length]);
        if (magicNumber != MagicNumber)
        {
            throw new InvalidOperationException("Not supported file");
        }

        var version = MemoryMarshal.Read<int>(buff[MagicBytes.Length..]);
        if (version != CurrentVersion)
        {
            throw new InvalidOperationException("Version not supported");
        }

        fs.ReadExactly(buff[..8]);
        var width = MemoryMarshal.Read<int>(buff[..4]);
        var height = MemoryMarshal.Read<int>(buff[4..]);

        return new PixelBattleDatabase(fs, new WriteAheadLog(GetWalFileName(path)), width, height);
    }

    private static string GetWalFileName(string dbName)
    {
        return dbName + "-wal";
    }
}
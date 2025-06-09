using System.Diagnostics;
using PixelBattle.Extensions;
using PixelBattle.Structures;
using PixelBattle.WriteAheadLog;

namespace PixelBattle;

public sealed class PixelBattleDatabaseDriver : IDisposable, IAsyncDisposable
{
    public const int ChunkMetadataSize = sizeof(long);
    private const uint CurrentVersion = 1;

    private readonly WalWriter _walWriter;
    private readonly FileStream _dbFileStream;

    public int Width { get; }
    public int Height { get; }
    public int ChunkSize { get; }

    private PixelBattleDatabaseDriver(FileStream dbFileStream, WalWriter walWriter, DbHeaders headers)
    {
        _dbFileStream = dbFileStream;
        _walWriter = walWriter;
        Width = headers.Width;
        Height = headers.Height;
        ChunkSize = headers.ChunkSize;
    }

    public async Task SetAsync(int x, int y, byte color)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);

        await _walWriter.AppendAsync(new PixelColorInfo(x, y, color));
    }

    public static PixelBattleDatabaseDriver Create(string path, int width, int height, int chunkSize)
    {
        if (width * height % chunkSize != 0)
            throw new ArgumentException("size of canvas must devide by chunk size", nameof(chunkSize));

        WalWriter? walWriter = null;
        FileStream? dbStream = null;
        try
        {
            dbStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            var headers = new DbHeaders(DbHeaders.DefaultMagicNumber, CurrentVersion, width, height, chunkSize);

            var headersLength = DbHeaders.BinaryLength + sizeof(uint);
            Span<byte> buffer = stackalloc byte[headersLength];
            var written = headers.WriteWithCrc32(buffer);
            Debug.Assert(written == buffer.Length);

            dbStream.Write(buffer);
            dbStream.SetLength(headersLength + (width * height) + (width * height / chunkSize * ChunkMetadataSize));
            dbStream.Flush(true);

            var walFileName = GetWalFileName(path);
            walWriter = WalWriter.Create(walFileName);

            return new PixelBattleDatabaseDriver(dbStream, walWriter, headers);
        }
        catch
        {
            walWriter?.Dispose();
            dbStream?.Dispose();

            throw;
        }
    }

    public static PixelBattleDatabaseDriver Open()
    {
        throw new NotImplementedException();
    }

    private static string GetWalFileName(string dbName)
    {
        return dbName + "-wal";
    }

    public void Dispose()
    {
        _walWriter.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _walWriter.DisposeAsync();
    }
}
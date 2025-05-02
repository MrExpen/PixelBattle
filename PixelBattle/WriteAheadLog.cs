namespace PixelBattle;

public sealed class WriteAheadLog : IDisposable
{
    private readonly FileStream _fileStream;
    private readonly Lock _lock = new();

    public WriteAheadLog(string path)
    {
        _fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        lock (_lock)
        {
            _fileStream.Write(data);
            _fileStream.Flush(true);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _fileStream.Dispose();
        }
    }
}
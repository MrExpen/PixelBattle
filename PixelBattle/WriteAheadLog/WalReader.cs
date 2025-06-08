using PixelBattle.Structures;

namespace PixelBattle.WriteAheadLog;

public sealed class WalReader : IDisposable, IAsyncDisposable, IAsyncEnumerable<WalRecord>
{
    private readonly FileStream _walStream;
    private bool _locked;

    public AsyncEnumerator GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var alreadyLocked = Interlocked.CompareExchange(ref _locked, true, false);
        if (alreadyLocked)
        {
            throw new SynchronizationLockException();
        }

        return new AsyncEnumerator(this, cancellationToken);
    }

    IAsyncEnumerator<WalRecord> IAsyncEnumerable<WalRecord>.GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        return GetAsyncEnumerator(cancellationToken);
    }

    private WalReader(FileStream walFileStream)
    {
        _walStream = walFileStream;
    }

    public static WalReader Open(string path, bool exclusiveAccess = false)
    {
        FileStream? stream = null;
        try
        {
            stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                exclusiveAccess ? FileShare.Read : FileShare.ReadWrite
            );

            return new WalReader(stream);
        }
        catch
        {
            stream?.Dispose();

            throw;
        }
    }

    public struct AsyncEnumerator : IAsyncEnumerator<WalRecord>
    {
        private readonly WalReader _reader;
        private readonly CancellationToken _cancellationToken;
        private readonly byte[] _buffer;

        public AsyncEnumerator(WalReader reader, CancellationToken cancellationToken)
        {
            _reader = reader;
            _cancellationToken = cancellationToken;
            _buffer = new byte[WalRecord.BinaryLength];
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            if (_reader._walStream.Length - _reader._walStream.Position < _buffer.Length)
            {
                return false;
            }

            await _reader._walStream.ReadExactlyAsync(_buffer, _cancellationToken);

            return WalRecord.TryRead(_buffer, out _current);
            //TODO crc32
        }

        private WalRecord _current;
        public WalRecord Current => _current;

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _reader._locked, false);
            return ValueTask.CompletedTask;
        }
    }

    public void Dispose()
    {
        _walStream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _walStream.DisposeAsync();
    }
}
using System.Threading.Channels;
using PixelBattle.Structures;

namespace PixelBattle;

public class UpdatePublisherWrapper : IAsyncEnumerable<PublishedUpdate>
{
    private readonly UpdatePublisher _updatePublisher;

    public UpdatePublisherWrapper(UpdatePublisher updatePublisher)
    {
        _updatePublisher = updatePublisher;
    }

    public IAsyncEnumerator<PublishedUpdate> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private class AsyncEnumerator : IAsyncEnumerator<PublishedUpdate>
    {
        private readonly UpdatePublisher _updatePublisher;
        private readonly Channel<PublishedUpdate> _channel;

        public AsyncEnumerator(UpdatePublisher updatePublisher, Channel<PublishedUpdate> channel)
        {
            _updatePublisher = updatePublisher;
            _channel = channel;
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            var result = await _channel.Reader.WaitToReadAsync();

            return result && _channel.Reader.TryRead(out _current);
        }

        private PublishedUpdate _current;
        public PublishedUpdate Current => _current;

        public ValueTask DisposeAsync()
        {
            _updatePublisher.Unsubscribe(_channel);
            return ValueTask.CompletedTask;
        }
    }
}
using System.Threading.Channels;
using PixelBattle.Structures;

namespace PixelBattle;

public sealed class UpdatePublisher : IDisposable
{
    private readonly Lock _lock;
    private readonly List<Channel<PublishedUpdate>> _channels;
    private readonly UpdatePublisherWrapper _wrapper;

    public UpdatePublisher()
    {
        _lock = new Lock();
        _channels = [];
        _wrapper = new UpdatePublisherWrapper(this);
    }

    public void Publish(long newChunkVersion, ref WalRecord record)
    {
        lock (_lock)
        {
            foreach (var channel in _channels)
            {
                // TODO use own type instead of Channel because of locking each time
                var success = channel.Writer
                    .TryWrite(new PublishedUpdate(record.Timestamp, newChunkVersion, record.X, record.Y, record.Color));

                if (!success)
                {
                    //TODO log
                }
            }
        }
    }

    public Channel<PublishedUpdate> Subscribe()
    {
        var channel = Channel.CreateUnbounded<PublishedUpdate>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = false,
            SingleWriter = true,
            SingleReader = true,
        });

        lock (_lock)
        {
            _channels.Add(channel);
        }

        return channel;
    }

    public void Unsubscribe(Channel<PublishedUpdate> channel)
    {
        lock (_lock)
        {
            channel.Writer.TryComplete();
            _channels.Remove(channel);
        }
    }

    public IAsyncEnumerable<PublishedUpdate> GetAsyncEnumerable() => _wrapper;

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var channel in _channels)
            {
                channel.Writer.TryComplete();
            }

            _channels.Clear();
        }
    }
}
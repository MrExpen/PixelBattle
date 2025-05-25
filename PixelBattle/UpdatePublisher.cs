using System.Threading.Channels;
using PixelBattle.Structures;

namespace PixelBattle;

public sealed class UpdatePublisher : IDisposable
{
    private const int MaxConsumerLag = 1024;

    private readonly Lock _lock;
    private readonly List<Channel<PublishedUpdate>> _channels;
    private readonly List<Channel<PublishedUpdate>> _completedChannels;
    private readonly UpdatePublisherWrapper _wrapper;

    public UpdatePublisher()
    {
        _lock = new Lock();
        _channels = [];
        _completedChannels = [];
        _wrapper = new UpdatePublisherWrapper(this);
    }

    private void ClearCompleted()
    {
        // Todo start this in background
        lock (_lock)
        {
            foreach (var completedChannel in _completedChannels)
            {
                _channels.Remove(completedChannel);
            }

            _completedChannels.Clear();
        }
    }

    public void Publish(long newChunkVersion, ref WalRecord record)
    {
        lock (_lock)
        {
            foreach (var channel in _channels)
            {
                var success = channel.Writer
                    .TryWrite(new PublishedUpdate(record.Timestamp, newChunkVersion, record.X, record.Y, record.Color));

                if (!success)
                {
                    //TODO remove on success=false
                    channel.Writer.TryComplete();
                    _completedChannels.Add(channel);
                }
            }
        }
    }

    public Channel<PublishedUpdate> Subscribe()
    {
        var channel = Channel.CreateBounded<PublishedUpdate>(new BoundedChannelOptions(MaxConsumerLag)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait,
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
            _completedChannels.Remove(channel);
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
            _completedChannels.Clear();
        }
    }
}
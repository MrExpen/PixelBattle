using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using PixelBattle.Binary;
using PixelBattle.Structures;

namespace PixelBattle.Benchmarks;

[MemoryDiagnoser]
public class NotifyBenchmark : IDisposable
{
    private readonly Channel<WalRecord> _channel;
    private readonly FileStream _readWriteStream;
    private readonly FileStream _readStreamAsync;
    private readonly byte[] _buffer = new byte[WalRecord.BinaryLength * 1000];
    private WalRecord _walRecord;

    [Params(1, 100, 1000)] public int Count { get; set; }

    public NotifyBenchmark()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _channel = System.Threading.Channels.Channel.CreateUnbounded<WalRecord>(
            new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = false,
                SingleReader = true,
                SingleWriter = true
            });
        _readWriteStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096,
            FileOptions.None);
        _readStreamAsync = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096,
            FileOptions.Asynchronous);
        _walRecord = new WalRecord(765467547784, 1, 34, 54, 2);
    }

    [Benchmark]
    public async Task Channel()
    {
        for (int i = 0; i < Count; i++)
        {
            _readWriteStream.Write(Utils.AsSpan(ref _walRecord));
        }

        _readWriteStream.Flush(true);

        for (int i = 0; i < Count; i++)
        {
            _channel.Writer.TryWrite(_walRecord);
        }

        for (int i = 0; i < Count; i++)
        {
            await _channel.Reader.ReadAsync();
        }
    }

    [Benchmark]
    public async Task FileStreamAsync()
    {
        for (int i = 0; i < Count; i++)
        {
            _readWriteStream.Write(Utils.AsSpan(ref _walRecord));
        }

        _readWriteStream.Flush(true);

        await _readStreamAsync.ReadExactlyAsync(_buffer, 0, WalRecord.BinaryLength * Count);

        for (int i = 0; i < Count; i++)
        {
            WalRecord.Read(_buffer.AsSpan(i * WalRecord.BinaryLength, WalRecord.BinaryLength));
        }
    }

    public void Dispose()
    {
        _readStreamAsync.Dispose();
        _readWriteStream.Dispose();
        try
        {
            File.Delete(_readWriteStream.Name);
        }
        catch
        {
            // Ignore
        }
    }
}
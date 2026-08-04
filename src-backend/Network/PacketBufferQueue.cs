using System.Threading.Channels;
using AlbionBot.Infrastructure;

namespace AlbionBot.Network;

public class PacketBufferQueue
{
    private readonly Channel<byte[]> _channel;

    public PacketBufferQueue(int capacity = 10000)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            SingleWriter = false,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest
        };
        _channel = Channel.CreateBounded<byte[]>(options);
    }

    public bool TryEnqueue(byte[] data)
    {
        DebugLogger.Log($"Queue enqueue length={data.Length}");
        return _channel.Writer.TryWrite(data);
    }

    public ValueTask<byte[]> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }

    public IAsyncEnumerable<byte[]> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}

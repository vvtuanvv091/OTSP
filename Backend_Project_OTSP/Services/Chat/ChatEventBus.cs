using System.Threading.Channels;
using Backend_Project_OTSP.Services.Chat;

namespace Backend_Project_OTSP.Services.Chat;

public sealed class ChatEventBus : IChatEventBus
{
    private readonly Channel<ChatMessageCreatedEvent> _channel = Channel.CreateBounded<ChatMessageCreatedEvent>(new BoundedChannelOptions(10000)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });

    public ValueTask PublishAsync(ChatMessageCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(@event, cancellationToken);
    }

    public IAsyncEnumerable<ChatMessageCreatedEvent> ReadEventsAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}


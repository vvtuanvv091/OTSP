namespace PROJECT_OTSP.Services.Events;

public interface IChatEventBus
{
    ValueTask PublishAsync(ChatMessageCreatedEvent @event, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ChatMessageCreatedEvent> ReadEventsAsync(CancellationToken cancellationToken = default);
}


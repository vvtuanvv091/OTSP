using Backend_Project_OTSP.Dtos.Chat;

namespace Backend_Project_OTSP.Services.Chat;

public interface IChatEventBus
{
    ValueTask PublishAsync(ChatMessageCreatedEvent @event, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ChatMessageCreatedEvent> ReadEventsAsync(CancellationToken cancellationToken = default);
}


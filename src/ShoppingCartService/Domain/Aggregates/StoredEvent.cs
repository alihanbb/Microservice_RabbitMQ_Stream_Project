using ShoppingCartService.Domain.Events;

namespace ShoppingCartService.Domain.Aggregates;

public sealed class StoredEvent
{
    public Guid Id { get; init; }
    public Guid AggregateId { get; init; }
    public string AggregateType { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string EventData { get; init; } = string.Empty;
    public int Version { get; init; }
    public DateTime OccurredAt { get; init; }
    public DateTime StoredAt { get; init; }

    public static StoredEvent Create(Guid aggregateId, string aggregateType, DomainEvent @event, string serializedData)
    {
        return new StoredEvent
        {
            Id = @event.EventId,
            AggregateId = aggregateId,
            AggregateType = aggregateType,
            EventType = @event.EventType,
            EventData = serializedData,
            Version = @event.Version,
            OccurredAt = @event.OccurredAt,
            StoredAt = DateTime.UtcNow
        };
    }
}

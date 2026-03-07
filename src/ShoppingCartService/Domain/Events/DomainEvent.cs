namespace ShoppingCartService.Domain.Events;

public abstract record DomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => GetType().Name;
    public int Version { get; init; }
}

public interface ICartEvent
{
    Guid CartId { get; }
}

using ShoppingCartService.Domain.Events;

namespace ShoppingCartService.Domain.Aggregates;

public abstract class AggregateRoot
{
    private readonly List<DomainEvent> _uncommittedEvents = [];

    public Guid Id { get; protected set; }
    public int Version { get; protected set; } = -1;

    public IReadOnlyCollection<DomainEvent> UncommittedEvents => _uncommittedEvents.AsReadOnly();

    protected void AddEvent(DomainEvent @event)
    {
        
        var nextVersion = Version + _uncommittedEvents.Count + 1;
        _uncommittedEvents.Add(@event with { Version = nextVersion });
        Apply(@event);
    }

    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();

    public void LoadFromHistory(IEnumerable<DomainEvent> history)
    {
        foreach (var @event in history)
        {
            Apply(@event);
  
            Version = @event.Version;
        }
    }

    protected abstract void Apply(DomainEvent @event);
}


using ShoppingCartService.Domain.Aggregates;
using ShoppingCartService.Domain.Events;

namespace ShoppingCartService.Application.Interfaces;

public interface IEventStore
{
    Task<IEnumerable<DomainEvent>> GetEventsAsync(Guid aggregateId, CancellationToken cancellationToken = default);
    Task<IEnumerable<DomainEvent>> GetEventsAsync(Guid aggregateId, int fromVersion, CancellationToken cancellationToken = default);
    Task SaveEventsAsync(Guid aggregateId, string aggregateType, IEnumerable<DomainEvent> events, int expectedVersion, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid aggregateId, CancellationToken cancellationToken = default);
}

public interface ICartAggregateRepository
{
    Task<CartAggregate?> GetByIdAsync(Guid cartId, CancellationToken cancellationToken = default);
    Task<CartAggregate?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SaveAsync(CartAggregate cart, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid cartId, CancellationToken cancellationToken = default);
}

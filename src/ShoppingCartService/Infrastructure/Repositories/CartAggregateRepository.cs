using ShoppingCartService.Domain.Aggregates;
using ShoppingCartService.Application.Interfaces;

namespace ShoppingCartService.Infrastructure.Repositories;

public sealed class CartAggregateRepository(IEventStore eventStore) : ICartAggregateRepository
{
    private const string AggregateType = "Cart";

    public async Task<CartAggregate?> GetByIdAsync(Guid cartId, CancellationToken cancellationToken = default)
    {
        var events = await eventStore.GetEventsAsync(cartId, cancellationToken);
        var eventList = events.ToList();

        if (eventList.Count == 0)
            return null;

        return CartAggregate.LoadFromEvents(eventList);
    }

    public async Task<CartAggregate?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cartId = await eventStore.GetCartIdByUserIdAsync(userId, cancellationToken);
        
        if (cartId == null)
            return null;

        return await GetByIdAsync(cartId.Value, cancellationToken);
    }

    public async Task SaveAsync(CartAggregate cart, CancellationToken cancellationToken = default)
    {
        var uncommittedEvents = cart.UncommittedEvents.ToList();
        
        if (uncommittedEvents.Count == 0)
            return;

        await eventStore.SaveEventsAsync(
            cart.Id,
            AggregateType,
            uncommittedEvents,
            cart.Version,
            cancellationToken);

        cart.ClearUncommittedEvents();
    }

    public async Task<bool> ExistsAsync(Guid cartId, CancellationToken cancellationToken = default)
    {
        return await eventStore.ExistsAsync(cartId, cancellationToken);
    }
}

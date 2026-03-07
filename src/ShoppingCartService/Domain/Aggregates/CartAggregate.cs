using ShoppingCartService.Domain.Events;

namespace ShoppingCartService.Domain.Aggregates;

public sealed class CartAggregate : AggregateRoot
{
    public Guid UserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsConfirmed { get; private set; }

    private readonly List<CartItem> _items = [];
    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

    private CartAggregate() { }

    #region Factory Methods

    public static CartAggregate Create(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        var cart = new CartAggregate();
        var @event = new CartCreatedEvent(Guid.NewGuid(), userId, DateTime.UtcNow);
        cart.AddEvent(@event);

        return cart;
    }

    public static CartAggregate LoadFromEvents(IEnumerable<DomainEvent> events)
    {
        var cart = new CartAggregate();
        cart.LoadFromHistory(events);
        return cart;
    }

    #endregion

    #region Commands

    public void AddItem(Guid productId, string productName, string category, int quantity, decimal price)
    {
        EnsureNotConfirmed();

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than 0", nameof(quantity));

        if (price < 0)
            throw new ArgumentException("Price cannot be negative", nameof(price));

        var existingItem = _items.FirstOrDefault(i => i.ProductId == productId);

        if (existingItem != null)
        {
            var @event = new ItemQuantityIncreasedEvent(
                Id,
                productId,
                quantity,
                existingItem.Quantity + quantity);
            AddEvent(@event);
        }
        else
        {
            var @event = new ItemAddedToCartEvent(Id, productId, productName, category, quantity, price);
            AddEvent(@event);
        }
    }

    public void RemoveItem(Guid productId)
    {
        EnsureNotConfirmed();

        var item = _items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new InvalidOperationException($"Item with ProductId {productId} not found in cart");

        var @event = new ItemRemovedFromCartEvent(Id, productId);
        AddEvent(@event);
    }

    public void UpdateItemQuantity(Guid productId, int newQuantity)
    {
        EnsureNotConfirmed();

        if (newQuantity <= 0)
            throw new ArgumentException("Quantity must be greater than 0", nameof(newQuantity));

        var item = _items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new InvalidOperationException($"Item with ProductId {productId} not found in cart");

        var @event = new ItemQuantityUpdatedEvent(Id, productId, item.Quantity, newQuantity);
        AddEvent(@event);
    }

    public void Confirm()
    {
        EnsureNotConfirmed();

        if (_items.Count == 0)
            throw new InvalidOperationException("Cannot confirm an empty cart");

        var @event = new CartConfirmedEvent(
            Id,
            GetTotalAmount(),
            _items.Sum(i => i.Quantity),
            DateTime.UtcNow);
        AddEvent(@event);
    }

    public void Clear()
    {
        EnsureNotConfirmed();

        if (_items.Count == 0)
            return;

        var @event = new CartClearedEvent(Id, _items.Count);
        AddEvent(@event);
    }

    #endregion

    #region Queries

    public decimal GetTotalAmount() => _items.Sum(i => i.GetTotalPrice());

    public int GetTotalItems() => _items.Sum(i => i.Quantity);

    public bool HasItem(Guid productId) => _items.Any(i => i.ProductId == productId);

    #endregion

    #region Event Handlers

    protected override void Apply(DomainEvent @event)
    {
        switch (@event)
        {
            case CartCreatedEvent e:
                ApplyCartCreated(e);
                break;
            case ItemAddedToCartEvent e:
                ApplyItemAdded(e);
                break;
            case ItemQuantityIncreasedEvent e:
                ApplyItemQuantityIncreased(e);
                break;
            case ItemRemovedFromCartEvent e:
                ApplyItemRemoved(e);
                break;
            case ItemQuantityUpdatedEvent e:
                ApplyItemQuantityUpdated(e);
                break;
            case CartConfirmedEvent e:
                ApplyCartConfirmed(e);
                break;
            case CartClearedEvent:
                ApplyCartCleared();
                break;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    private void ApplyCartCreated(CartCreatedEvent @event)
    {
        Id = @event.CartId;
        UserId = @event.UserId;
        CreatedAt = @event.CreatedAt;
        UpdatedAt = @event.CreatedAt;
        IsConfirmed = false;
    }

    private void ApplyItemAdded(ItemAddedToCartEvent @event)
    {
        var item = CartItem.Create(
            @event.ProductId,
            @event.ProductName,
            @event.Category,
            @event.Quantity,
            @event.Price);
        _items.Add(item);
    }

    private void ApplyItemQuantityIncreased(ItemQuantityIncreasedEvent @event)
    {
        var item = _items.First(i => i.ProductId == @event.ProductId);
        item.IncreaseQuantity(@event.AddedQuantity);
    }

    private void ApplyItemRemoved(ItemRemovedFromCartEvent @event)
    {
        var item = _items.First(i => i.ProductId == @event.ProductId);
        _items.Remove(item);
    }

    private void ApplyItemQuantityUpdated(ItemQuantityUpdatedEvent @event)
    {
        var item = _items.First(i => i.ProductId == @event.ProductId);
        item.UpdateQuantity(@event.NewQuantity);
    }

    private void ApplyCartConfirmed(CartConfirmedEvent @event)
    {
        IsConfirmed = true;
    }

    private void ApplyCartCleared()
    {
        _items.Clear();
    }

    #endregion

    #region Helpers

    private void EnsureNotConfirmed()
    {
        if (IsConfirmed)
            throw new InvalidOperationException("Cannot modify a confirmed cart");
    }

    #endregion
}

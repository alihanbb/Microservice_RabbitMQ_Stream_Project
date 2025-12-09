namespace ShoppingCartService.Domain.Entities;

public class Cart
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsConfirmed { get; private set; }

    private readonly List<CartItem> _items = new();
    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

    private Cart() { }

    public static Cart Create(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        return new Cart
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsConfirmed = false
        };
    }

    public static Cart Load(Guid id, Guid userId, DateTime createdAt, DateTime updatedAt, bool isConfirmed, List<CartItem> items)
    {
        return new Cart
        {
            Id = id,
            UserId = userId,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            IsConfirmed = isConfirmed,
            _items = { }
        };
    }

    public void AddItem(CartItem item)
    {
        if (IsConfirmed)
            throw new InvalidOperationException("Cannot modify a confirmed cart");

        var existingItem = _items.FirstOrDefault(i => i.ProductId == item.ProductId);

        if (existingItem != null)
        {
            existingItem.IncreaseQuantity(item.Quantity);
        }
        else
        {
            _items.Add(item);
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveItem(Guid productId)
    {
        if (IsConfirmed)
            throw new InvalidOperationException("Cannot modify a confirmed cart");

        var item = _items.FirstOrDefault(i => i.ProductId == productId);

        if (item == null)
            throw new InvalidOperationException($"Item with ProductId {productId} not found in cart");

        _items.Remove(item);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateItemQuantity(Guid productId, int quantity)
    {
        if (IsConfirmed)
            throw new InvalidOperationException("Cannot modify a confirmed cart");

        var item = _items.FirstOrDefault(i => i.ProductId == productId);

        if (item == null)
            throw new InvalidOperationException($"Item with ProductId {productId} not found in cart");

        item.UpdateQuantity(quantity);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Confirm()
    {
        if (IsConfirmed)
            throw new InvalidOperationException("Cart is already confirmed");

        if (!_items.Any())
            throw new InvalidOperationException("Cannot confirm an empty cart");

        IsConfirmed = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public decimal GetTotalAmount() => _items.Sum(i => i.GetTotalPrice());

    public void LoadItems(List<CartItem> items)
    {
        _items.Clear();
        _items.AddRange(items);
    }
}

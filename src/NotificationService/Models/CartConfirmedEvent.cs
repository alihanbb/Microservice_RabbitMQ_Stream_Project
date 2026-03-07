namespace NotificationService.Models;

/// <summary>
/// Represents a cart confirmation event from the Shopping Cart Service
/// </summary>
public class CartConfirmedEvent
{
    public Guid UserId { get; set; }
    public DateTime ConfirmedAt { get; set; }
    public int ItemCount { get; set; }
    public decimal TotalAmount { get; set; }
    public List<CartItem> Items { get; set; } = [];
}

public class CartItem
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

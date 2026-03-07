namespace ShoppingCartService.Domain.Entities;

/// <summary>
/// Domain entity representing an item in a shopping cart.
/// Enforces invariants: non-empty product ID, positive quantity, non-negative price.
/// </summary>
public class CartItem
{
    private const int MinQuantity = 1;
    private const int MaxQuantity = 10000;
    private const decimal MinPrice = 0.01m;
    private const decimal MaxPrice = 999999.99m;

    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal Price { get; private set; }

    private CartItem() { }

    /// <summary>
    /// Factory method to create a validated CartItem.
    /// </summary>
    public static CartItem Create(Guid productId, string productName, string category, int quantity, decimal price)
    {
        ValidateInputs(productId, productName, quantity, price);

        return new CartItem
        {
            ProductId = productId,
            ProductName = productName,
            Category = category ?? string.Empty,
            Quantity = quantity,
            Price = price
        };
    }

    /// <summary>
    /// Updates quantity with bounds validation.
    /// </summary>
    public void UpdateQuantity(int quantity)
    {
        if (quantity < MinQuantity || quantity > MaxQuantity)
            throw new ArgumentException($"Quantity must be between {MinQuantity} and {MaxQuantity}", nameof(quantity));

        Quantity = quantity;
    }

    /// <summary>
    /// Increases quantity by the specified amount.
    /// </summary>
    public void IncreaseQuantity(int amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero", nameof(amount));

        var newQuantity = Quantity + amount;
        if (newQuantity > MaxQuantity)
            throw new ArgumentException($"Quantity cannot exceed {MaxQuantity}", nameof(amount));

        Quantity = newQuantity;
    }

    /// <summary>
    /// Gets the total price for this line item (Price * Quantity).
    /// </summary>
    public decimal GetTotalPrice() => Price * Quantity;

    private static void ValidateInputs(Guid productId, string productName, int quantity, decimal price)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId cannot be empty", nameof(productId));

        if (string.IsNullOrWhiteSpace(productName))
            throw new ArgumentException("ProductName cannot be empty", nameof(productName));

        if (productName.Length > 200)
            throw new ArgumentException("ProductName cannot exceed 200 characters", nameof(productName));

        if (quantity < MinQuantity || quantity > MaxQuantity)
            throw new ArgumentException($"Quantity must be between {MinQuantity} and {MaxQuantity}", nameof(quantity));

        if (price < MinPrice || price > MaxPrice)
            throw new ArgumentException($"Price must be between {MinPrice} and {MaxPrice}", nameof(price));
    }
}


using System.Text.Json.Serialization;

namespace NotificationService.Models;

public record CartConfirmedEvent(
    [property: JsonPropertyName("CartId")] Guid CartId,
    [property: JsonPropertyName("UserId")] Guid UserId,
    [property: JsonPropertyName("TotalAmount")] decimal TotalAmount,
    [property: JsonPropertyName("TotalItems")] int TotalItems,
    [property: JsonPropertyName("Items")] IReadOnlyCollection<CartItemSnapshot> Items,
    [property: JsonPropertyName("ConfirmedAt")] DateTime ConfirmedAt);

public record CartItemSnapshot(
    [property: JsonPropertyName("ProductId")] Guid ProductId,
    [property: JsonPropertyName("ProductName")] string ProductName,
    [property: JsonPropertyName("Category")] string Category,
    [property: JsonPropertyName("Quantity")] int Quantity,
    [property: JsonPropertyName("Price")] decimal Price);

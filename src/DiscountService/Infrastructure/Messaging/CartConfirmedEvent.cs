using System.Text.Json.Serialization;

namespace DiscountService.Infrastructure.Messaging;

public record CartConfirmedEvent(
    [property: JsonPropertyName("CartId")] Guid CartId,
    [property: JsonPropertyName("UserId")] Guid UserId,
    [property: JsonPropertyName("TotalAmount")] decimal TotalAmount,
    [property: JsonPropertyName("TotalItems")] int TotalItems,
    [property: JsonPropertyName("ConfirmedAt")] DateTime ConfirmedAt);

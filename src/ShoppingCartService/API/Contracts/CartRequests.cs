namespace ShoppingCartService.API.Contracts;
public record AddItemRequest(
    Guid ProductId,
    string ProductName,
    string Category,
    int Quantity,
    decimal Price
);
public record RemoveItemRequest(
   Guid ProductId
);
public record ConfirmCartRequest();

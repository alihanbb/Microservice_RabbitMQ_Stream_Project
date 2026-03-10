using System.ComponentModel.DataAnnotations;

namespace ShoppingCartService.API.Contracts;

public record AddItemRequest(
    [property: Required(ErrorMessage = "ProductId is required")]
    Guid ProductId,
    
    [property: Required(ErrorMessage = "ProductName is required")]
    [property: StringLength(200, MinimumLength = 1, ErrorMessage = "ProductName must be between 1 and 200 characters")]
    string ProductName,
    
    [property: Required(ErrorMessage = "Category is required")]
    [property: StringLength(100, MinimumLength = 1, ErrorMessage = "Category must be between 1 and 100 characters")]
    string Category,
    
    [property: Range(1, 10000, ErrorMessage = "Quantity must be between 1 and 10000")]
    int Quantity,
    
    [property: Range(typeof(decimal), "0.01", "999999.99", ErrorMessage = "Price must be between 0.01 and 999999.99")]
    decimal Price
);

public record RemoveItemRequest(
    [property: Required(ErrorMessage = "ProductId is required")]
    Guid ProductId
);

public record ConfirmCartRequest();

public record UpdateItemQuantityRequest(
    [property: Range(1, 10000, ErrorMessage = "Quantity must be between 1 and 10000")]
    int NewQuantity
);


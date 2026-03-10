using System.ComponentModel.DataAnnotations;

namespace DiscountService.API.Contracts;

public record CalculateDiscountRequest(
    [property: Required(ErrorMessage = "CouponCode is required")]
    string CouponCode,

    [property: Range(0.01, 9999999.99, ErrorMessage = "CartTotal must be positive")]
    decimal CartTotal,

    IEnumerable<CartItemInfo>? Items);

public record CartItemInfo(
    Guid ProductId,
    string ProductName,
    string Category,
    int Quantity,
    decimal Price);

public record CalculateDiscountResponse(
    bool Success,
    string CouponCode,
    decimal OriginalTotal,
    decimal TotalDiscount,
    decimal FinalTotal,
    IEnumerable<AppliedRuleInfo> AppliedRules,
    string? Message);

public record AppliedRuleInfo(
    string RuleName,
    decimal DiscountPercentage,
    decimal DiscountAmount,
    string Priority);

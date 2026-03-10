using System.ComponentModel.DataAnnotations;

namespace DiscountService.API.Contracts;

// --- Response DTOs ---

public record CouponCodeResponse(
    Guid Id,
    string Code,
    string Description,
    bool IsUsed,
    DateTime? UsedAt,
    DateTime? ExpiresAt,
    int MaxUsageCount,
    int CurrentUsageCount,
    bool IsExpired,
    bool IsValid,
    DateTime CreatedDate,
    DateTime UpdatedDate,
    IEnumerable<DiscountRuleResponse> DiscountRules);

// --- Request DTOs ---

public record CreateCouponRequest(
    [property: Required(ErrorMessage = "Code is required")]
    [property: StringLength(50, MinimumLength = 3, ErrorMessage = "Code must be between 3 and 50 characters")]
    string Code,

    [property: StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    string Description,

    DateTime? ExpiresAt,

    [property: Range(1, 100000, ErrorMessage = "MaxUsageCount must be between 1 and 100000")]
    int MaxUsageCount = 1);

public record ValidateCouponRequest(
    [property: Required(ErrorMessage = "Code is required")]
    string Code,

    [property: Range(0.01, 9999999.99, ErrorMessage = "Amount must be positive")]
    decimal Amount);

public record ValidateCouponResponse(
    bool IsValid,
    string Code,
    decimal OriginalAmount,
    decimal DiscountAmount,
    decimal FinalAmount,
    string? Message);

using System.ComponentModel.DataAnnotations;

namespace DiscountService.API.Contracts;

// --- Response DTOs ---

public record DiscountRuleResponse(
    Guid Id,
    string Name,
    string Description,
    decimal DiscountPercentage,
    decimal MinDiscountAmount,
    bool IsActive,
    string Priority,
    DateTime CreatedDate,
    DateTime UpdatedDate);

// --- Request DTOs ---

public record CreateDiscountRuleRequest(
    [property: Required(ErrorMessage = "Name is required")]
    [property: StringLength(200, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 200 characters")]
    string Name,

    [property: StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    string Description,

    [property: Range(0.01, 100, ErrorMessage = "DiscountPercentage must be between 0.01 and 100")]
    decimal DiscountPercentage,

    [property: Range(0, 9999999.99, ErrorMessage = "MinDiscountAmount cannot be negative")]
    decimal MinDiscountAmount,

    [property: Required(ErrorMessage = "Priority is required")]
    string Priority);

public record UpdateDiscountRuleRequest(
    [property: Required(ErrorMessage = "Name is required")]
    [property: StringLength(200, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 200 characters")]
    string Name,

    [property: StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    string Description,

    [property: Range(0.01, 100, ErrorMessage = "DiscountPercentage must be between 0.01 and 100")]
    decimal DiscountPercentage,

    [property: Range(0, 9999999.99, ErrorMessage = "MinDiscountAmount cannot be negative")]
    decimal MinDiscountAmount,

    [property: Required(ErrorMessage = "Priority is required")]
    string Priority);

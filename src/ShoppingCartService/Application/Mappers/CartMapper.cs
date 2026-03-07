using ShoppingCartService.Application.DTOs;
using ShoppingCartService.Domain.Aggregates;
using ShoppingCartService.Domain.Entities;

namespace ShoppingCartService.Application.Mappers;

public static class CartMapper
{
    /// <summary>
    /// Maps from Cart entity state to DTO
    /// </summary>
    public static CartDto ToDtoFromState(this Cart cart)
    {
        return new CartDto(
            Id: cart.Id,
            UserId: cart.UserId,
            CreatedAt: cart.CreatedAt,
            UpdatedAt: cart.UpdatedAt,
            IsConfirmed: cart.IsConfirmed,
            TotalAmount: cart.GetTotalAmount(),
            Items: cart.Items.Select(i => i.ToDto())
        );
    }

    /// <summary>
    /// Maps from CartAggregate (built from events) to DTO
    /// </summary>
    public static CartDto ToDtoFromEvents(CartAggregate cart)
    {
        return new CartDto(
            Id: cart.Id,
            UserId: cart.UserId,
            CreatedAt: cart.CreatedAt,
            UpdatedAt: cart.UpdatedAt,
            IsConfirmed: cart.IsConfirmed,
            TotalAmount: cart.GetTotalAmount(),
            Items: cart.Items.Select(i => i.ToDto())
        );
    }

    /// <summary>
    /// Legacy method for backward compatibility - maps from Cart entity
    /// </summary>
    public static CartDto ToDto(this Cart cart) => cart.ToDtoFromState();

    public static CartItemDto ToDto(this CartItem item)
    {
        return new CartItemDto(
            ProductId: item.ProductId,
            ProductName: item.ProductName,
            Category: item.Category,
            Quantity: item.Quantity,
            Price: item.Price,
            TotalPrice: item.GetTotalPrice()
        );
    }
}


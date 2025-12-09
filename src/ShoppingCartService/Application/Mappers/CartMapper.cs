using ShoppingCartService.Application.DTOs;
using ShoppingCartService.Domain.Entities;

namespace ShoppingCartService.Application.Mappers;

public static class CartMapper
{
    public static CartDto ToDto(this Cart cart)
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

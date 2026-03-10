using ShoppingCartService.API.Contracts;

namespace ShoppingCartService.Application.Commands.UpdateItemQuantity;

public sealed class UpdateItemQuantityRequestValidator : AbstractValidator<UpdateItemQuantityRequest>
{
    public UpdateItemQuantityRequestValidator()
    {
        RuleFor(x => x.NewQuantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than 0")
            .LessThanOrEqualTo(10000).WithMessage("Quantity cannot exceed 10000");
    }
}

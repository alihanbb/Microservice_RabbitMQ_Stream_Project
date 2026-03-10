using DiscountService.API.Contracts;
using DiscountService.API.Filters;
using DiscountService.Application.UseCase;
using Microsoft.AspNetCore.Mvc;

namespace DiscountService.API.Endpoint;

public static class DiscountCalculationEndpoint
{
    public static RouteGroupBuilder MapDiscountCalculationEndpoints(this RouteGroupBuilder group)
    {
        var endpoints = group.MapGroup("discounts")
                             .WithTags("Discount Calculations");

        endpoints.MapPost("calculate", async (
            [FromBody] CalculateDiscountRequest request,
            ICalculateDiscountUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(request.CouponCode, request.CartTotal, cancellationToken);
            if (!result.IsSuccess)
            {
                if (result.Error?.Contains("not found") == true)
                    return Results.NotFound(new { Error = result.Error });

                return Results.BadRequest(new { Error = result.Error });
            }

            var (discount, appliedRules) = result.Value;

            var response = new CalculateDiscountResponse(
                Success: true,
                CouponCode: request.CouponCode,
                OriginalTotal: request.CartTotal,
                TotalDiscount: discount,
                FinalTotal: request.CartTotal - discount,
                AppliedRules: appliedRules,
                Message: discount > 0
                    ? $"Discount applied successfully. You save {discount:C}!"
                    : "No applicable discounts for this order.");

            return Results.Ok(response);
        })
        .AddEndpointFilter<ValidationFilter<CalculateDiscountRequest>>()
        .Produces<CalculateDiscountResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .WithSummary("Sepet içeriğine göre kupon kodu ile indirim hesaplar.");

        return group;
    }
}

using DiscountService.API.Contracts;
using DiscountService.API.Filters;
using DiscountService.Application.UseCase;
using Microsoft.AspNetCore.Mvc;

namespace DiscountService.API.Endpoint;

public static class CouponCodesEndpoint
{
    public static RouteGroupBuilder MapCouponCodesEndpoints(this RouteGroupBuilder group)
    {
        var endpoints = group.MapGroup("couponcodes")
                             .WithTags("Coupon Codes");

        endpoints.MapGet("", async (ICouponCodeUseCases useCases, CancellationToken cancellationToken) =>
        {
            return Results.Ok(await useCases.GetAllAsync(cancellationToken));
        })
        .Produces<IEnumerable<CouponCodeResponse>>(StatusCodes.Status200OK)
        .WithSummary("Tüm kupon kodlarını listeler.");

        endpoints.MapGet("valid", async (ICouponCodeUseCases useCases, CancellationToken cancellationToken) =>
        {
            return Results.Ok(await useCases.GetValidAsync(cancellationToken));
        })
        .Produces<IEnumerable<CouponCodeResponse>>(StatusCodes.Status200OK)
        .WithSummary("Geçerli kupon kodlarını listeler.");

        endpoints.MapGet("expired", async (ICouponCodeUseCases useCases, CancellationToken cancellationToken) =>
        {
            return Results.Ok(await useCases.GetExpiredAsync(cancellationToken));
        })
        .Produces<IEnumerable<CouponCodeResponse>>(StatusCodes.Status200OK)
        .WithSummary("Süresi dolmuş kupon kodlarını listeler.");

        endpoints.MapGet("{code}", async (string code, ICouponCodeUseCases useCases, CancellationToken cancellationToken) =>
        {
            var result = await useCases.GetByCodeAsync(code, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(new { Error = result.Error });
        })
        .Produces<CouponCodeResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithSummary("Belirli bir kupon kodunu getirir.");

        endpoints.MapPost("", async (
            [FromBody] CreateCouponRequest request, 
            ICouponCodeUseCases useCases, 
            CancellationToken cancellationToken) =>
        {
            var result = await useCases.CreateAsync(request, cancellationToken);
            
            if (!result.IsSuccess)
            {
                if (result.Error?.Contains("already exists") == true)
                    return Results.Conflict(new { Error = result.Error });
                return Results.BadRequest(new { Error = result.Error });
            }

            return Results.CreatedAtRoute(
                routeName: null, 
                routeValues: new { code = result.Value?.Code }, 
                value: result.Value);
        })
        .AddEndpointFilter<ValidationFilter<CreateCouponRequest>>()
        .Produces<CouponCodeResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict)
        .WithSummary("Yeni kupon kodu oluşturur.");

        endpoints.MapPost("validate", async (
            [FromBody] ValidateCouponRequest request, 
            ICouponCodeUseCases useCases, 
            CancellationToken cancellationToken) =>
        {
            var result = await useCases.ValidateAsync(request, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(new { Error = result.Error });
        })
        .AddEndpointFilter<ValidationFilter<ValidateCouponRequest>>()
        .Produces<ValidateCouponResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithSummary("Kupon kodunu doğrular ve indirim hesaplar.");

        endpoints.MapPost("{code}/use", async (string code, ICouponCodeUseCases useCases, CancellationToken cancellationToken) =>
        {
            var result = await useCases.UseAsync(code, cancellationToken);
            
            if (!result.IsSuccess)
            {
                if (result.Error?.Contains("not found") == true)
                    return Results.NotFound(new { Error = result.Error });
                return Results.BadRequest(new { Error = result.Error });
            }
            
            return Results.Ok(result.Value);
        })
        .Produces<CouponCodeResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .WithSummary("Kupon kodunu kullanır (kullanım sayısını artırır).");

        endpoints.MapDelete("{code}", async (string code, ICouponCodeUseCases useCases, CancellationToken cancellationToken) =>
        {
            var result = await useCases.DeleteAsync(code, cancellationToken);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(new { Error = result.Error });
        })
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .WithSummary("Kupon kodunu siler.");

        return group;
    }
}

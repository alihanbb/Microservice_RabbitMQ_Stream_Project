using DiscountService.API.Contracts;
using DiscountService.API.Filters;
using DiscountService.Application.UseCase;
using Microsoft.AspNetCore.Mvc;

namespace DiscountService.API.Endpoint;

public static class DiscountRulesEndpoint
{
    public static RouteGroupBuilder MapDiscountRulesEndpoints(this RouteGroupBuilder group)
    {
        var endpoints = group.MapGroup("discount-rules")
                             .WithTags("Discount Rules");

        endpoints.MapGet("", async (IDiscountRuleUseCases useCases, CancellationToken cancellationToken) =>
        {
            return Results.Ok(await useCases.GetAllAsync(cancellationToken));
        })
        .Produces<IEnumerable<DiscountRuleResponse>>(StatusCodes.Status200OK)
        .WithSummary("Tüm indirim kurallarını listeler.");

        endpoints.MapGet("active", async (IDiscountRuleUseCases useCases, CancellationToken cancellationToken) =>
        {
            return Results.Ok(await useCases.GetActiveAsync(cancellationToken));
        })
        .Produces<IEnumerable<DiscountRuleResponse>>(StatusCodes.Status200OK)
        .WithSummary("Aktif indirim kurallarını listeler.");

        endpoints.MapGet("by-priority/{priority}", async (string priority, IDiscountRuleUseCases useCases, CancellationToken cancellationToken) =>
        {
            var result = await useCases.GetByPriorityAsync(priority, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { Error = result.Error });
        })
        .Produces<IEnumerable<DiscountRuleResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .WithSummary("Öncelik seviyesine göre indirim kurallarını listeler.");

        endpoints.MapGet("{id:guid}", async (Guid id, IDiscountRuleUseCases useCases, CancellationToken cancellationToken) =>
        {
            var result = await useCases.GetByIdAsync(id, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(new { Error = result.Error });
        })
        .Produces<DiscountRuleResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithSummary("ID ile indirim kuralı getirir.");

        endpoints.MapPost("", async (
            [FromBody] CreateDiscountRuleRequest request,
            IDiscountRuleUseCases useCases,
            CancellationToken cancellationToken) =>
        {
            var result = await useCases.CreateAsync(request, cancellationToken);
            
            if (!result.IsSuccess)
                return Results.BadRequest(new { Error = result.Error });

            return Results.CreatedAtRoute(
                routeName: null,
                routeValues: new { id = result.Value?.Id },
                value: result.Value);
        })
        .AddEndpointFilter<ValidationFilter<CreateDiscountRuleRequest>>()
        .Produces<DiscountRuleResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .WithSummary("Yeni indirim kuralı oluşturur.");

        endpoints.MapPut("{id:guid}", async (
            Guid id,
            [FromBody] UpdateDiscountRuleRequest request,
            IDiscountRuleUseCases useCases,
            CancellationToken cancellationToken) =>
        {
            var result = await useCases.UpdateAsync(id, request, cancellationToken);
            
            if (!result.IsSuccess)
            {
                if (result.Error?.Contains("not found") == true)
                    return Results.NotFound(new { Error = result.Error });
                return Results.BadRequest(new { Error = result.Error });
            }
            
            return Results.Ok(result.Value);
        })
        .AddEndpointFilter<ValidationFilter<UpdateDiscountRuleRequest>>()
        .Produces<DiscountRuleResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .WithSummary("İndirim kuralını günceller.");

        endpoints.MapPost("{id:guid}/activate", async (Guid id, IDiscountRuleUseCases useCases, CancellationToken cancellationToken) =>
        {
            var result = await useCases.ActivateAsync(id, cancellationToken);
            
            if (!result.IsSuccess)
            {
                if (result.Error?.Contains("not found") == true)
                    return Results.NotFound(new { Error = result.Error });
                return Results.BadRequest(new { Error = result.Error });
            }
            
            return Results.Ok(result.Value);
        })
        .Produces<DiscountRuleResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .WithSummary("İndirim kuralını aktif eder.");

        endpoints.MapPost("{id:guid}/deactivate", async (Guid id, IDiscountRuleUseCases useCases, CancellationToken cancellationToken) =>
        {
            var result = await useCases.DeactivateAsync(id, cancellationToken);
            
            if (!result.IsSuccess)
            {
                if (result.Error?.Contains("not found") == true)
                    return Results.NotFound(new { Error = result.Error });
                return Results.BadRequest(new { Error = result.Error });
            }
            
            return Results.Ok(result.Value);
        })
        .Produces<DiscountRuleResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .WithSummary("İndirim kuralını pasif eder.");

        endpoints.MapDelete("{id:guid}", async (Guid id, IDiscountRuleUseCases useCases, CancellationToken cancellationToken) =>
        {
            var result = await useCases.DeleteAsync(id, cancellationToken);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(new { Error = result.Error });
        })
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .WithSummary("İndirim kuralını siler.");

        return group;
    }
}

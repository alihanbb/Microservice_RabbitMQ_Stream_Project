#region
using Asp.Versioning;
using ShoppingCartService.API.Contracts;
using ShoppingCartService.API.Filters;
using ShoppingCartService.Application.Commands.AddItem;
using ShoppingCartService.Application.Commands.ConfirmCart;
using ShoppingCartService.Application.Commands.RemoveItem;
using ShoppingCartService.Application.Queries.GetCart;
#endregion
namespace ShoppingCartService.API.Endpoints;
public static class CartEndpoints
{
    public static void MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1, 0))
            .HasApiVersion(new ApiVersion(2, 0))
            .ReportApiVersions()
            .Build();

        // V1 Group
        var v1Group = app.MapGroup("/api/v{version:apiVersion}/carts")
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1, 0))
            .WithTags("Shopping Cart V1")
            .WithOpenApi();

        MapV1Endpoints(v1Group);

        // V2 Group
        var v2Group = app.MapGroup("/api/v{version:apiVersion}/carts")
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(2, 0))
            .WithTags("Shopping Cart V2")
            .WithOpenApi()
            .WithRateLimit();

        MapV2Endpoints(v2Group);
    }

    private static void MapV1Endpoints(RouteGroupBuilder group)
    {
        // GET: RateLimit -> Cache -> Performance (group level)
        group.MapGet("/{userId:guid}", GetCart)
            .WithName("GetCart_V1")
            .WithSummary("Get cart details for a user")
            .WithDescription("Retrieves the shopping cart for the specified user ID")
            .WithRateLimit<RelaxedRateLimit>()  // 1. Rate limit (relaxed for reads)
            .WithDistributedCache<ShortDistributedCache>()  // 2. Distributed cache
            .Produces<Application.DTOs.CartDto>(200)
            .Produces(404)
            .ProducesProblem(500);

        // POST: RateLimit -> Idempotency -> Validation
        group.MapPost("/{userId:guid}/items", AddItem)
            .WithName("AddItemToCart_V1")
            .WithSummary("Add an item to the cart")
            .WithDescription("Adds a new item or updates quantity if item already exists")
            .WithRateLimit()                    // 1. Rate limit (default)
            .WithIdempotency()                  // 2. Prevent duplicates
            .WithValidation<AddItemRequest>()   // 3. Validation filter
            .Produces<Application.DTOs.CartDto>(200)
            .ProducesValidationProblem(400)
            .ProducesProblem(500);

        // DELETE: RateLimit only
        group.MapDelete("/{userId:guid}/items/{productId:guid}", RemoveItem)
            .WithName("RemoveItemFromCart_V1")
            .WithSummary("Remove an item from the cart")
            .WithDescription("Removes the specified product from the user's cart")
            .WithRateLimit()                    // 1. Rate limit (default)
            .Produces<Application.DTOs.CartDto>(200)
            .Produces(404)
            .ProducesProblem(500);

        // POST Confirm: RateLimit (strict) -> Idempotency
        group.MapPost("/{userId:guid}/confirm", ConfirmCart)
            .WithName("ConfirmCart_V1")
            .WithSummary("Confirm the cart")
            .WithDescription("Confirms the cart for checkout processing")
            .WithRateLimit<StrictRateLimit>()   // 1. Strict rate limit for sensitive op
            .WithIdempotency()                  // 2. Prevent duplicate confirmations
            .Produces<Application.DTOs.CartDto>(200)
            .Produces(400)
            .Produces(404)
            .ProducesProblem(500);
    }

    private static void MapV2Endpoints(RouteGroupBuilder group)
    {
        // GET: RateLimit -> DistributedCache (medium) -> Performance (group level)
        group.MapGet("/{userId:guid}", GetCartV2)
            .WithName("GetCart_V2")
            .WithSummary("Get cart details for a user (V2)")
            .WithDescription("Retrieves the shopping cart with enhanced metadata for the specified user ID")
            .WithRateLimit<RelaxedRateLimit>()  // 1. Rate limit (relaxed for reads)
            .WithDistributedCache<MediumDistributedCache>()  // 2. Longer distributed cache
            .Produces<CartResponseV2>(200)
            .Produces(404)
            .ProducesProblem(500);

        // POST: RateLimit -> Idempotency -> Validation
        group.MapPost("/{userId:guid}/items", AddItemV2)
            .WithName("AddItemToCart_V2")
            .WithSummary("Add an item to the cart (V2)")
            .WithDescription("Adds a new item or updates quantity if item already exists")
            .WithRateLimit()                    // 1. Rate limit (default)
            .WithIdempotency()                  // 2. Prevent duplicates
            .WithValidation<AddItemRequest>()   // 3. Validation
            .Produces<Application.DTOs.CartDto>(200)
            .ProducesValidationProblem(400)
            .ProducesProblem(500);

        // DELETE: RateLimit only
        group.MapDelete("/{userId:guid}/items/{productId:guid}", RemoveItemV2)
            .WithName("RemoveItemFromCart_V2")
            .WithSummary("Remove an item from the cart (V2)")
            .WithDescription("Removes the specified product from the user's cart")
            .WithRateLimit()                    // 1. Rate limit (default)
            .Produces<Application.DTOs.CartDto>(200)
            .Produces(404)
            .ProducesProblem(500);

        // POST Confirm: RateLimit (strict) -> Idempotency
        group.MapPost("/{userId:guid}/confirm", ConfirmCartV2)
            .WithName("ConfirmCart_V2")
            .WithSummary("Confirm the cart (V2)")
            .WithDescription("Confirms the cart for checkout processing")
            .WithRateLimit<StrictRateLimit>()   // 1. Strict rate limit (first!)
            .WithIdempotency()                  // 2. Idempotency
            .Produces<Application.DTOs.CartDto>(200)
            .Produces(400)
            .Produces(404)
            .ProducesProblem(500);

        // POST Clear: RateLimit -> Idempotency
        group.MapPost("/{userId:guid}/clear", ClearCart)
            .WithName("ClearCart_V2")
            .WithSummary("Clear all items from the cart")
            .WithDescription("Removes all items from the user's shopping cart")
            .WithRateLimit()                    // 1. Rate limit (default)
            .WithIdempotency()                  // 2. Idempotency
            .Produces(204)
            .Produces(404)
            .ProducesProblem(500);
    }

    private static async Task<IResult> GetCart(
        Guid userId,
        GetCartQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetCartQuery(userId);
        var result = await handler.HandleAsync(query, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Data)
            : Results.Problem(result.Error, statusCode: result.StatusCode);
    }

    private static async Task<IResult> GetCartV2(
        Guid userId,
        GetCartQueryHandlerV2 handler,
        CancellationToken cancellationToken)
    {
        var query = new GetCartQuery(userId);
        var result = await handler.HandleAsync(query, cancellationToken);

        if (!result.IsSuccess)
            return Results.Problem(result.Error, statusCode: result.StatusCode);

        var response = new CartResponseV2
        {
            Cart = result.Data!,
            Metadata = new CartMetadata
            {
                ApiVersion = "2.0",
                RequestedAt = DateTime.UtcNow,
                ItemCount = result.Data!.Items?.Count() ?? 0
            }
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> AddItem(
        Guid userId,
        AddItemRequest request,
        AddItemCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new AddItemCommand(
            userId,
            request.ProductId,
            request.ProductName,
            request.Category,
            request.Quantity,
            request.Price
        );

        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Data)
            : Results.Problem(result.Error, statusCode: result.StatusCode);
    }

    private static async Task<IResult> RemoveItem(
        Guid userId,
        Guid productId,
        RemoveItemCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new RemoveItemCommand(userId, productId);
        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Data)
            : Results.Problem(result.Error, statusCode: result.StatusCode);
    }

    private static async Task<IResult> ConfirmCart(
        Guid userId,
        ConfirmCartCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new ConfirmCartCommand(userId);
        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Data)
            : Results.Problem(result.Error, statusCode: result.StatusCode);
    }

    private static async Task<IResult> ClearCart(
        Guid userId,
        Application.Interfaces.ICartRepository repository,
        CancellationToken cancellationToken)
    {
        var cart = await repository.GetByUserIdAsync(userId, cancellationToken);
        
        if (cart is null)
            return Results.NotFound("Cart not found");

        await repository.DeleteAsync(userId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> AddItemV2(
        Guid userId,
        AddItemRequest request,
        AddItemCommandHandlerV2 handler,
        CancellationToken cancellationToken)
    {
        var command = new AddItemCommand(
            userId,
            request.ProductId,
            request.ProductName,
            request.Category,
            request.Quantity,
            request.Price
        );

        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Data)
            : Results.Problem(result.Error, statusCode: result.StatusCode);
    }

    private static async Task<IResult> RemoveItemV2(
        Guid userId,
        Guid productId,
        RemoveItemCommandHandlerV2 handler,
        CancellationToken cancellationToken)
    {
        var command = new RemoveItemCommand(userId, productId);
        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Data)
            : Results.Problem(result.Error, statusCode: result.StatusCode);
    }

    private static async Task<IResult> ConfirmCartV2(
        Guid userId,
        ConfirmCartCommandHandlerV2 handler,
        CancellationToken cancellationToken)
    {
        var command = new ConfirmCartCommand(userId);
        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Data)
            : Results.Problem(result.Error, statusCode: result.StatusCode);
    }
}

public class CartResponseV2
{
    public Application.DTOs.CartDto Cart { get; set; } = default!;
    public CartMetadata Metadata { get; set; } = default!;
}

public class CartMetadata
{
    public string ApiVersion { get; set; } = default!;
    public DateTime RequestedAt { get; set; }
    public int ItemCount { get; set; }
}

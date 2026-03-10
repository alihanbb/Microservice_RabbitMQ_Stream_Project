using ShoppingCartService.API.Contracts;
using ShoppingCartService.API.Filters;
using ShoppingCartService.Application.Commands.AddItem;
using ShoppingCartService.Application.Commands.ConfirmCart;
using ShoppingCartService.Application.Commands.RemoveItem;
using ShoppingCartService.Application.Commands.UpdateItemQuantity;
using ShoppingCartService.Application.Queries.GetCart;

namespace ShoppingCartService.API.Endpoints;

public static class CartEndpoints
{
    public static void MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/carts")
            .WithTags("Shopping Cart")
            .WithOpenApi();

        group.MapGet("/{userId:guid}", GetCart)
            .WithName("GetCart")
            .WithSummary("Get cart details for a user")
            .WithDescription("Retrieves the shopping cart with metadata for the specified user ID")
            .WithRateLimit<RelaxedRateLimit>()
            .WithDistributedCache<MediumDistributedCache>()
            .Produces<CartResponse>(200)
            .Produces(404)
            .ProducesProblem(500);

        group.MapPost("/{userId:guid}/items", AddItem)
            .WithName("AddItemToCart")
            .WithSummary("Add an item to the cart")
            .WithDescription("Adds a new item or updates quantity if item already exists")
            .WithRateLimit()
            .WithIdempotency()
            .WithValidation<AddItemRequest>()
            .Produces<Application.DTOs.CartDto>(200)
            .ProducesValidationProblem(400)
            .ProducesProblem(500);

        group.MapDelete("/{userId:guid}/items/{productId:guid}", RemoveItem)
            .WithName("RemoveItemFromCart")
            .WithSummary("Remove an item from the cart")
            .WithDescription("Removes the specified product from the user's cart")
            .WithRateLimit()
            .Produces<Application.DTOs.CartDto>(200)
            .Produces(404)
            .ProducesProblem(500);

        group.MapPost("/{userId:guid}/confirm", ConfirmCart)
            .WithName("ConfirmCart")
            .WithSummary("Confirm the cart")
            .WithDescription("Confirms the cart for checkout processing and publishes CartConfirmedEvent")
            .WithRateLimit<StrictRateLimit>()
            .WithIdempotency()
            .Produces<Application.DTOs.CartDto>(200)
            .Produces(400)
            .Produces(404)
            .ProducesProblem(500);

        group.MapPost("/{userId:guid}/clear", ClearCart)
            .WithName("ClearCart")
            .WithSummary("Clear all items from the cart")
            .WithDescription("Removes all items from the user's shopping cart")
            .WithRateLimit()
            .WithIdempotency()
            .Produces(204)
            .Produces(404)
            .ProducesProblem(500);

        group.MapPut("/{userId:guid}/items/{productId:guid}", UpdateItemQuantity)
            .WithName("UpdateItemQuantity")
            .WithSummary("Update item quantity in the cart")
            .WithDescription("Updates the quantity of an existing item in the user's cart")
            .WithRateLimit()
            .WithValidation<UpdateItemQuantityRequest>()
            .Produces<Application.DTOs.CartDto>(200)
            .Produces(404)
            .Produces(409)
            .ProducesValidationProblem(400)
            .ProducesProblem(500);
    }

    private static async Task<IResult> GetCart(
        Guid userId,
        GetCartQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetCartQuery(userId);
        var result = await handler.HandleAsync(query, cancellationToken);

        if (!result.IsSuccess)
            return Results.Problem(result.Error, statusCode: result.StatusCode);

        var response = new CartResponse
        {
            Cart = result.Data!,
            Metadata = new CartMetadata
            {
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
            request.Price);

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
        ICartAggregateRepository repository,
        CancellationToken cancellationToken)
    {
        var cart = await repository.GetByUserIdAsync(userId, cancellationToken);

        if (cart is null)
            return Results.NotFound("Cart not found");

        cart.Clear();
        await repository.SaveAsync(cart, cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> UpdateItemQuantity(
        Guid userId,
        Guid productId,
        UpdateItemQuantityRequest request,
        UpdateItemQuantityCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new UpdateItemQuantityCommand(userId, productId, request.NewQuantity);
        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Data)
            : Results.Problem(result.Error, statusCode: result.StatusCode);
    }
}

public class CartResponse
{
    public Application.DTOs.CartDto Cart { get; set; } = default!;
    public CartMetadata Metadata { get; set; } = default!;
}

public class CartMetadata
{
    public DateTime RequestedAt { get; set; }
    public int ItemCount { get; set; }
}

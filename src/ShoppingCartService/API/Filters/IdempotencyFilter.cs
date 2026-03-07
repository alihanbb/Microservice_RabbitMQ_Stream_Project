using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ShoppingCartService.API.Filters;

public sealed class IdempotencyFilter(
    RedisConnectionFactory redis,
    ILogger<IdempotencyFilter> logger) : IEndpointFilter
{
    private const string IdempotencyHeader = "X-Idempotency-Key";
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromHours(24);

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
            return await next(context);

        if (!context.HttpContext.Request.Headers.TryGetValue(IdempotencyHeader, out var idempotencyKey) 
            || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return await next(context);
        }

        var db = redis.GetDatabase();
        var cacheKey = $"idempotency_{idempotencyKey}";

        try
        {
            var cachedResponse = await db.StringGetAsync(cacheKey);
            if (!cachedResponse.IsNull)
            {
                logger.LogInformation("Idempotent request detected: {Key}", idempotencyKey.ToString());
                context.HttpContext.Response.Headers["X-Idempotent-Replayed"] = "true";
                return Results.Ok(JsonSerializer.Deserialize<object>(cachedResponse!));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error reading idempotency cache for key {Key}", idempotencyKey.ToString());
            // Continue without cache on error
        }

        var result = await next(context);
        if (context.HttpContext.Response.StatusCode is >= 200 and < 300)
        {
            try
            {
                await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(result), DefaultExpiration);
                logger.LogDebug("Idempotency key stored: {Key}", idempotencyKey.ToString());
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error storing idempotency cache for key {Key}", idempotencyKey.ToString());
                // Don't fail the request if caching fails
            }
        }

        return result;
    }
}

public static class IdempotencyFilterExtensions
{
    public static RouteHandlerBuilder WithIdempotency(this RouteHandlerBuilder builder)
        => builder.AddEndpointFilter<IdempotencyFilter>();

    public static RouteGroupBuilder WithIdempotency(this RouteGroupBuilder builder)
        => builder.AddEndpointFilter<IdempotencyFilter>();
}


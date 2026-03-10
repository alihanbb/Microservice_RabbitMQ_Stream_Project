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

                var cached = JsonSerializer.Deserialize<CachedIdempotentResponse>(cachedResponse!);
                if (cached != null)
                {
                    context.HttpContext.Response.StatusCode = cached.StatusCode;
                    // Return raw JSON body so status code + body are both preserved
                    return Results.Json(cached.Body, statusCode: cached.StatusCode);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error reading idempotency cache for key {Key}", idempotencyKey.ToString());
        }

        var result = await next(context);

        var statusCode = context.HttpContext.Response.StatusCode;
        if (statusCode is >= 200 and < 300)
        {
            try
            {
                var bodyJson = JsonSerializer.SerializeToNode(result);
                var entry = new CachedIdempotentResponse(statusCode, bodyJson);
                await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(entry), DefaultExpiration);
                logger.LogDebug("Idempotency key stored: {Key}", idempotencyKey.ToString());
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error storing idempotency cache for key {Key}", idempotencyKey.ToString());
            }
        }

        return result;
    }

    private sealed record CachedIdempotentResponse(
        int StatusCode,
        System.Text.Json.Nodes.JsonNode? Body);
}

public static class IdempotencyFilterExtensions
{
    public static RouteHandlerBuilder WithIdempotency(this RouteHandlerBuilder builder)
        => builder.AddEndpointFilter<IdempotencyFilter>();

    public static RouteGroupBuilder WithIdempotency(this RouteGroupBuilder builder)
        => builder.AddEndpointFilter<IdempotencyFilter>();
}

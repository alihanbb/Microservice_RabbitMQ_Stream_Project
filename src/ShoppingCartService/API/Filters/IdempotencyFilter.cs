using Microsoft.Extensions.Caching.Memory;

namespace ShoppingCartService.API.Filters;
public sealed class IdempotencyFilter(IMemoryCache cache, ILogger<IdempotencyFilter> logger) : IEndpointFilter
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

        var cacheKey = $"idempotency_{idempotencyKey}";
        if (cache.TryGetValue(cacheKey, out var cachedResponse))
        {
            logger.LogInformation("Idempotent request detected: {Key}", idempotencyKey.ToString());
            context.HttpContext.Response.Headers["X-Idempotent-Replayed"] = "true";
            return cachedResponse;
        }

        var result = await next(context);
        if (context.HttpContext.Response.StatusCode is >= 200 and < 300)
        {
            cache.Set(cacheKey, result, DefaultExpiration);
            logger.LogDebug("Idempotency key stored: {Key}", idempotencyKey.ToString());
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

namespace ShoppingCartService.API.Filters;

public sealed class DistributedCacheFilter(
    RedisConnectionFactory redis,
    ILogger<DistributedCacheFilter> logger) : IEndpointFilter
{
    private static readonly TimeSpan Expiration = TimeSpan.FromMinutes(5);

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        // Sadece GET istekleri cache'lenir
        if (!HttpMethods.IsGet(context.HttpContext.Request.Method))
            return await next(context);

        var key = $"cache:{context.HttpContext.Request.Path}{context.HttpContext.Request.QueryString}";
        var db = redis.GetDatabase();

        // Cache'de var mı?
        var cached = await db.StringGetAsync(key);
        if (!cached.IsNullOrEmpty)
        {
            logger.LogDebug("Cache HIT: {Key}", key);
            context.HttpContext.Response.Headers["X-Cache"] = "HIT";
            return Results.Ok(JsonSerializer.Deserialize<object>(cached!));
        }

        // Endpoint'i çalıştır
        var result = await next(context);

        // Başarılı response'u cache'le
        if (context.HttpContext.Response.StatusCode == 200 && result != null)
        {
            await db.StringSetAsync(key, JsonSerializer.Serialize(result), Expiration);
            context.HttpContext.Response.Headers["X-Cache"] = "MISS";
        }

        return result;
    }
}

public sealed class DistributedCacheFilter<TConfig> : IEndpointFilter
    where TConfig : IDistributedCacheConfig, new()
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        if (!HttpMethods.IsGet(context.HttpContext.Request.Method))
            return await next(context);

        var config = new TConfig();
        var redis = context.HttpContext.RequestServices.GetRequiredService<RedisConnectionFactory>();
        var key = $"cache:{config.Prefix}:{context.HttpContext.Request.Path}{context.HttpContext.Request.QueryString}";

        var db = redis.GetDatabase();
        var cached = await db.StringGetAsync(key);

        if (!cached.IsNullOrEmpty)
        {
            context.HttpContext.Response.Headers["X-Cache"] = "HIT";
            return Results.Ok(JsonSerializer.Deserialize<object>(cached!));
        }

        var result = await next(context);

        if (context.HttpContext.Response.StatusCode == 200 && result != null)
        {
            await db.StringSetAsync(key, JsonSerializer.Serialize(result), config.Expiration);
            context.HttpContext.Response.Headers["X-Cache"] = "MISS";
        }

        return result;
    }
}

#region Config & Extensions

public interface IDistributedCacheConfig
{
    string Prefix { get; }
    TimeSpan Expiration { get; }
}

public sealed class ShortDistributedCache : IDistributedCacheConfig
{
    public string Prefix => "short";
    public TimeSpan Expiration => TimeSpan.FromMinutes(1);
}

public sealed class MediumDistributedCache : IDistributedCacheConfig
{
    public string Prefix => "medium";
    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}

public sealed class LongDistributedCache : IDistributedCacheConfig
{
    public string Prefix => "long";
    public TimeSpan Expiration => TimeSpan.FromHours(1);
}

public static class DistributedCacheExtensions
{
    public static RouteHandlerBuilder WithDistributedCache(this RouteHandlerBuilder b)
        => b.AddEndpointFilter<DistributedCacheFilter>();

    public static RouteHandlerBuilder WithDistributedCache<T>(this RouteHandlerBuilder b) where T : IDistributedCacheConfig, new()
        => b.AddEndpointFilter<DistributedCacheFilter<T>>();

    public static RouteGroupBuilder WithDistributedCache(this RouteGroupBuilder b)
        => b.AddEndpointFilter<DistributedCacheFilter>();

    public static RouteGroupBuilder WithDistributedCache<T>(this RouteGroupBuilder b) where T : IDistributedCacheConfig, new()
        => b.AddEndpointFilter<DistributedCacheFilter<T>>();
}

#endregion

namespace ShoppingCartService.API.Filters;

public sealed class RateLimitFilter(
    RedisConnectionFactory redis,
    ILogger<RateLimitFilter> logger) : IEndpointFilter
{
    private const int Limit = 100;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var clientId = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        var key = $"rate:{clientId}:{context.HttpContext.Request.Path}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var db = redis.GetDatabase();

        // Eski kayıtları temizle
        await db.SortedSetRemoveRangeByScoreAsync(key, 0, now - (long)Window.TotalSeconds);

        // Mevcut sayıyı al
        var count = await db.SortedSetLengthAsync(key);

        // Limit aşıldı mı?
        if (count >= Limit)
        {
            logger.LogWarning("Rate limit aşıldı: {Client}", clientId);
            SetHeaders(context, Limit, 0);
            return Results.StatusCode(429);
        }

        // Yeni isteği ekle
        await db.SortedSetAddAsync(key, Guid.NewGuid().ToString(), now);
        await db.KeyExpireAsync(key, Window);

        SetHeaders(context, Limit, Limit - count - 1);
        return await next(context);
    }

    private static void SetHeaders(EndpointFilterInvocationContext ctx, int limit, long remaining)
    {
        ctx.HttpContext.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
        ctx.HttpContext.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
    }
}

public sealed class RateLimitFilter<TConfig> : IEndpointFilter
    where TConfig : IRateLimitConfig, new()
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var config = new TConfig();
        var redis = context.HttpContext.RequestServices.GetRequiredService<RedisConnectionFactory>();
        var clientId = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        var key = $"rate:{config.Name}:{clientId}:{context.HttpContext.Request.Path}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var db = redis.GetDatabase();
        await db.SortedSetRemoveRangeByScoreAsync(key, 0, now - (long)config.Window.TotalSeconds);

        var count = await db.SortedSetLengthAsync(key);

        if (count >= config.Limit)
        {
            context.HttpContext.Response.Headers["X-RateLimit-Limit"] = config.Limit.ToString();
            context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = "0";
            return Results.StatusCode(429);
        }

        await db.SortedSetAddAsync(key, Guid.NewGuid().ToString(), now);
        await db.KeyExpireAsync(key, config.Window);

        context.HttpContext.Response.Headers["X-RateLimit-Limit"] = config.Limit.ToString();
        context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = (config.Limit - count - 1).ToString();

        return await next(context);
    }
}

#region Config & Extensions

public interface IRateLimitConfig
{
    string Name { get; }
    int Limit { get; }
    TimeSpan Window { get; }
}

public sealed class StrictRateLimit : IRateLimitConfig
{
    public string Name => "strict";
    public int Limit => 10;
    public TimeSpan Window => TimeSpan.FromMinutes(1);
}

public sealed class RelaxedRateLimit : IRateLimitConfig
{
    public string Name => "relaxed";
    public int Limit => 500;
    public TimeSpan Window => TimeSpan.FromMinutes(1);
}

public static class RateLimitExtensions
{
    public static RouteHandlerBuilder WithRateLimit(this RouteHandlerBuilder b)
        => b.AddEndpointFilter<RateLimitFilter>();

    public static RouteHandlerBuilder WithRateLimit<T>(this RouteHandlerBuilder b) where T : IRateLimitConfig, new()
        => b.AddEndpointFilter<RateLimitFilter<T>>();

    public static RouteGroupBuilder WithRateLimit(this RouteGroupBuilder b)
        => b.AddEndpointFilter<RateLimitFilter>();

    public static RouteGroupBuilder WithRateLimit<T>(this RouteGroupBuilder b) where T : IRateLimitConfig, new()
        => b.AddEndpointFilter<RateLimitFilter<T>>();
}

#endregion

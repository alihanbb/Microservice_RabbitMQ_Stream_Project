using ShoppingCartService.Domain.Events;

namespace ShoppingCartService.Infrastructure.EventStore;

public sealed class EventStoreOptions
{
    public const string SectionName = "EventStore";
    public int ActiveCartTtlDays    { get; set; } = 30;
    public int ConfirmedCartTtlDays { get; set; } = 7;
}

public sealed class RedisEventStore(
    RedisConnectionFactory connectionFactory,
    IOptions<EventStoreOptions> options,
    ILogger<RedisEventStore> logger) : IEventStore
{
    private const string EventStreamPrefix = "events:cart:";
    private const string EventIndexPrefix  = "eventindex:cart:";

    private int ActiveCartTtlSeconds    => options.Value.ActiveCartTtlDays    * 24 * 3600;
    private int ConfirmedCartTtlSeconds => options.Value.ConfirmedCartTtlDays * 24 * 3600;

    private const string AtomicSaveScript = @"
        local streamKey    = KEYS[1]
        local userIndexKey = KEYS[2]

        local expectedVersion = tonumber(ARGV[1])
        local userIndexValue  = ARGV[2]
        local ttl             = tonumber(ARGV[3])
        local numEvents       = tonumber(ARGV[4])

        -- Use the sorted set SCORE as version (avoids JSON parsing in Lua)
        local last = redis.call('ZREVRANGEBYSCORE', streamKey, '+inf', '-inf', 'WITHSCORES', 'LIMIT', '0', '1')
        local currentVersion = -1
        if #last > 0 then
            currentVersion = tonumber(last[2])
        end

        if currentVersion ~= expectedVersion then
            return redis.error_reply('Concurrency conflict: expected ' .. expectedVersion .. ' but found ' .. currentVersion)
        end

        -- ARGV[5+i*2] = score (version), ARGV[6+i*2] = serialized event data
        for i = 0, numEvents - 1 do
            local score = tonumber(ARGV[5 + i * 2])
            local data  = ARGV[6 + i * 2]
            redis.call('ZADD', streamKey, score, data)
        end

        -- Persist user-to-cart index when cart is first created
        if userIndexValue ~= '' then
            redis.call('SET', userIndexKey, userIndexValue)
        end

        -- Set TTL (refreshed on every save for active carts; shorter for confirmed carts)
        if ttl > 0 then
            redis.call('EXPIRE', streamKey, ttl)
        end

        return redis.status_reply('OK')
    ";

    public async Task<IEnumerable<DomainEvent>> GetEventsAsync(Guid aggregateId, CancellationToken cancellationToken = default)
        => await GetEventsAsync(aggregateId, 0, cancellationToken);

    public async Task<IEnumerable<DomainEvent>> GetEventsAsync(Guid aggregateId, int fromVersion, CancellationToken cancellationToken = default)
    {
        var db  = connectionFactory.GetDatabase();
        var key = GetStreamKey(aggregateId);

        var entries = await db.SortedSetRangeByScoreAsync(key, fromVersion);
        var events  = new List<DomainEvent>(entries.Length);

        foreach (var entry in entries)
        {
            if (!entry.HasValue) continue;

            var storedEvent = JsonSerializer.Deserialize<StoredEventData>(entry!);
            if (storedEvent == null) continue;

            var domainEvent = DeserializeEvent(storedEvent);
            if (domainEvent != null)
                events.Add(domainEvent);
        }

        return events;
    }

    public async Task SaveEventsAsync(
        Guid aggregateId,
        string aggregateType,
        IEnumerable<DomainEvent> events,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var db       = connectionFactory.GetDatabase();
        var key      = GetStreamKey(aggregateId);
        var eventList = events.ToList();

        if (eventList.Count == 0)
            return;

        bool isConfirming = eventList.Any(e => e.EventType == nameof(CartConfirmedEvent));
        int  ttl          = isConfirming ? ConfirmedCartTtlSeconds : ActiveCartTtlSeconds;

        var cartCreatedEvent = eventList.OfType<CartCreatedEvent>().FirstOrDefault();
        var userIndexKey     = cartCreatedEvent != null ? GetUserIndexKey(cartCreatedEvent.UserId) : string.Empty;
        var userIndexValue   = cartCreatedEvent != null ? aggregateId.ToString() : string.Empty;

        var args = new List<RedisValue>(4 + eventList.Count * 2)
        {
            expectedVersion,
            userIndexValue,
            ttl,
            eventList.Count
        };

        foreach (var @event in eventList)
        {
            var storedEventData = new StoredEventData
            {
                EventId       = @event.EventId,
                EventType     = @event.EventType,
                EventData     = JsonSerializer.Serialize(@event, @event.GetType()),
                Version       = @event.Version,
                OccurredAt    = @event.OccurredAt,
                AggregateId   = aggregateId,
                AggregateType = aggregateType
            };

            args.Add(@event.Version);                             
            args.Add(JsonSerializer.Serialize(storedEventData));  
        }

        try
        {
            var keys = new RedisKey[]
            {
                key,
                userIndexKey.Length > 0 ? (RedisKey)userIndexKey : (RedisKey)string.Empty
            };

            await db.ScriptEvaluateAsync(AtomicSaveScript, keys, [.. args]);

            logger.LogDebug(
                "Saved {Count} events for aggregate {AggregateId}, new version {Version}",
                eventList.Count, aggregateId, eventList[^1].Version);
        }
        catch (RedisException ex) when (ex.Message.StartsWith("Concurrency conflict"))
        {
            logger.LogWarning(
                "Concurrency conflict for aggregate {AggregateId}: {Message}",
                aggregateId, ex.Message);
            throw new InvalidOperationException(ex.Message, ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving events for aggregate {AggregateId}", aggregateId);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(Guid aggregateId, CancellationToken cancellationToken = default)
    {
        var db  = connectionFactory.GetDatabase();
        var key = GetStreamKey(aggregateId);
        return await db.KeyExistsAsync(key);
    }

    public async Task<Guid?> GetCartIdByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var db           = connectionFactory.GetDatabase();
        var userIndexKey = GetUserIndexKey(userId);
        var cartId       = await db.StringGetAsync(userIndexKey);

        if (cartId.HasValue && Guid.TryParse(cartId!, out var id))
            return id;

        return null;
    }

    private static string GetStreamKey(Guid aggregateId)  => $"{EventStreamPrefix}{aggregateId}";
    private static string GetUserIndexKey(Guid userId)     => $"{EventIndexPrefix}user:{userId}";

    private static DomainEvent? DeserializeEvent(StoredEventData storedEvent)
    {
        var eventType = EventTypeRegistry.GetEventType(storedEvent.EventType);
        if (eventType == null)
            return null;

        return JsonSerializer.Deserialize(storedEvent.EventData, eventType) as DomainEvent;
    }

    private sealed class StoredEventData
    {
        public Guid     EventId       { get; set; }
        public Guid     AggregateId   { get; set; }
        public string   AggregateType { get; set; } = string.Empty;
        public string   EventType     { get; set; } = string.Empty;
        public string   EventData     { get; set; } = string.Empty;
        public int      Version       { get; set; }
        public DateTime OccurredAt    { get; set; }
    }

    private static class EventTypeRegistry
    {
        private static readonly Dictionary<string, Type> EventTypes;

        static EventTypeRegistry()
        {
            EventTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(DomainEvent).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
                .ToDictionary(t => t.Name, t => t);
        }

        public static Type? GetEventType(string eventTypeName)
            => EventTypes.TryGetValue(eventTypeName, out var type) ? type : null;
    }
}

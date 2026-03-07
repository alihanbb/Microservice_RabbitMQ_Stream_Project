using ShoppingCartService.Domain.Events;

namespace ShoppingCartService.Infrastructure.EventStore;

public sealed class RedisEventStore(RedisConnectionFactory connectionFactory, ILogger<RedisEventStore> logger) : IEventStore
{
    private const string EventStreamPrefix = "events:cart:";
    private const string EventIndexPrefix = "eventindex:cart:";
    
    // Lua script for atomic check-and-set: checks version and saves events atomically
    private const string CheckAndSetLuaScript = @"
        local key = KEYS[1]
        local expectedVersion = tonumber(ARGV[1])
        local lastEntry = redis.call('ZRANGE', key, -1, -1)
        local currentVersion = -1
        
        if #lastEntry > 0 then
            local decoded = cjson.decode(lastEntry[1])
            currentVersion = tonumber(decoded.Version)
        end
        
        if currentVersion ~= expectedVersion then
            return {err = 'Concurrency conflict'}
        end
        
        return {ok = 'Version check passed'}
    ";

    public async Task<IEnumerable<DomainEvent>> GetEventsAsync(Guid aggregateId, CancellationToken cancellationToken = default)
    {
        return await GetEventsAsync(aggregateId, 0, cancellationToken);
    }

    public async Task<IEnumerable<DomainEvent>> GetEventsAsync(Guid aggregateId, int fromVersion, CancellationToken cancellationToken = default)
    {
        var db = connectionFactory.GetDatabase();
        var key = GetStreamKey(aggregateId);

        var entries = await db.SortedSetRangeByScoreAsync(key, fromVersion);
        var events = new List<DomainEvent>();

        foreach (var entry in entries)
        {
            if (entry.HasValue)
            {
                var storedEvent = JsonSerializer.Deserialize<StoredEventData>(entry!);
                if (storedEvent != null)
                {
                    var domainEvent = DeserializeEvent(storedEvent);
                    if (domainEvent != null)
                        events.Add(domainEvent);
                }
            }
        }

        return events;
    }

    public async Task SaveEventsAsync(Guid aggregateId, string aggregateType, IEnumerable<DomainEvent> events, int expectedVersion, CancellationToken cancellationToken = default)
    {
        var db = connectionFactory.GetDatabase();
        var server = connectionFactory.GetServer();
        var key = GetStreamKey(aggregateId);

        try
        {
            // Execute Lua script for atomic version check
            var script = LuaScript.Prepare(CheckAndSetLuaScript);
            var result = await db.ScriptEvaluateAsync(script, [new RedisKey(key)], [expectedVersion]);

            if (result.IsNull || result.Type == ResultType.Error)
            {
                logger.LogWarning("Concurrency conflict detected for aggregate {AggregateId}", aggregateId);
                throw new InvalidOperationException($"Concurrency conflict. Expected version {expectedVersion}");
            }

            var transaction = db.CreateTransaction();
            var version = expectedVersion;

            foreach (var @event in events)
            {
                version++;
                var storedEventData = new StoredEventData
                {
                    EventId = @event.EventId,
                    EventType = @event.EventType,
                    EventData = JsonSerializer.Serialize(@event, @event.GetType()),
                    Version = version,
                    OccurredAt = @event.OccurredAt,
                    AggregateId = aggregateId,
                    AggregateType = aggregateType
                };

                var serialized = JsonSerializer.Serialize(storedEventData);
                transaction.SortedSetAddAsync(key, serialized, version);
            }

            // Store user-to-cart mapping
            var userIdEvent = events.OfType<CartCreatedEvent>().FirstOrDefault();
            if (userIdEvent != null)
            {
                var userIndexKey = GetUserIndexKey(userIdEvent.UserId);
                transaction.StringSetAsync(userIndexKey, aggregateId.ToString());
            }

            var committed = await transaction.ExecuteAsync();
            if (!committed)
            {
                logger.LogError("Failed to save events to event store for aggregate {AggregateId}", aggregateId);
                throw new InvalidOperationException("Failed to save events to event store");
            }

            logger.LogDebug("Events saved successfully for aggregate {AggregateId} with version {Version}", aggregateId, version);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving events for aggregate {AggregateId}", aggregateId);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(Guid aggregateId, CancellationToken cancellationToken = default)
    {
        var db = connectionFactory.GetDatabase();
        var key = GetStreamKey(aggregateId);
        return await db.KeyExistsAsync(key);
    }

    public async Task<Guid?> GetCartIdByUserIdAsync(Guid userId)
    {
        var db = connectionFactory.GetDatabase();
        var userIndexKey = GetUserIndexKey(userId);
        var cartId = await db.StringGetAsync(userIndexKey);

        if (cartId.HasValue && Guid.TryParse(cartId!, out var id))
            return id;

        return null;
    }

    private static async Task<int> GetCurrentVersionAsync(IDatabase db, string key)
    {
        var lastEntry = await db.SortedSetRangeByRankAsync(key, -1, -1);
        if (lastEntry.Length == 0)
            return -1;

        var storedEvent = JsonSerializer.Deserialize<StoredEventData>(lastEntry[0]!);
        return storedEvent?.Version ?? -1;
    }

    private static string GetStreamKey(Guid aggregateId) => $"{EventStreamPrefix}{aggregateId}";
    private static string GetUserIndexKey(Guid userId) => $"{EventIndexPrefix}user:{userId}";

    private static DomainEvent? DeserializeEvent(StoredEventData storedEvent)
    {
        var eventType = EventTypeRegistry.GetEventType(storedEvent.EventType);

        if (eventType == null)
        {
            return null;
        }

        return JsonSerializer.Deserialize(storedEvent.EventData, eventType) as DomainEvent;
    }

    private sealed class StoredEventData
    {
        public Guid EventId { get; set; }
        public Guid AggregateId { get; set; }
        public string AggregateType { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string EventData { get; set; } = string.Empty;
        public int Version { get; set; }
        public DateTime OccurredAt { get; set; }
    }

    /// <summary>
    /// Static registry for event types with auto-discovery via reflection
    /// </summary>
    private static class EventTypeRegistry
    {
        private static readonly Dictionary<string, Type> EventTypes = new();

        static EventTypeRegistry()
        {
            RegisterEventTypes();
        }

        private static void RegisterEventTypes()
        {
            // Auto-discover all DomainEvent types
            var domainEventType = typeof(DomainEvent);
            var eventTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => domainEventType.IsAssignableFrom(p) && p.IsClass && !p.IsAbstract);

            foreach (var type in eventTypes)
            {
                EventTypes[type.Name] = type;
            }
        }

        public static Type? GetEventType(string eventTypeName)
        {
            return EventTypes.TryGetValue(eventTypeName, out var type) ? type : null;
        }
    }

# Microservice RabbitMQ Stream Project — Mimari ve Teknik Dokümantasyon

> **Proje Dizini:** `Microservice_RabbitMQ_Stream_Project`  
> **Teknoloji Yığını:** .NET 9 · RabbitMQ Super Streams · Redis Event Sourcing · CQRS · SQL Server · PostgreSQL

---

## İçindekiler

1. [Genel Mimari](#1-genel-mimari)
2. [Servisler ve Sorumluluklar](#2-servisler-ve-sorumluluklar)
3. [Altyapı (Docker Compose)](#3-altyapı-docker-compose)
4. [Event Sourcing — Redis Event Store](#4-event-sourcing--redis-event-store)
5. [CQRS Deseni](#5-cqrs-deseni)
6. [Domain Events (Alan Olayları)](#6-domain-events-alan-olayları)
7. [Integration Events ve RabbitMQ Super Streams](#7-integration-events-ve-rabbitmq-super-streams)
8. [Tam Mesaj Akışı: Sepet Onaylama](#8-tam-mesaj-akışı-sepet-onaylama)
9. [Outbox Deseni — Mevcut Yaklaşım ve Sınırları](#9-outbox-deseni--mevcut-yaklaşım-ve-sınırları)
10. [Idempotency (Tekrar İşlem Güvenliği)](#10-idempotency-tekrar-i̇şlem-güvenliği)
11. [API Endpoints](#11-api-endpoints)
12. [Güvenlik ve Konfigürasyon](#12-güvenlik-ve-konfigürasyon)

---

## 1. Genel Mimari

```
┌─────────────────────────────────────────────────────────────────┐
│                        CLIENT (HTTP)                            │
└──────────────────────────┬──────────────────────────────────────┘
                           │ REST API
                           ▼
┌──────────────────────────────────────────────────────────────────┐
│                    ShoppingCartService                           │
│  ┌──────────┐  ┌──────────────┐  ┌────────────────────────────┐  │
│  │ Minimal  │  │    CQRS      │  │   Domain (Event Sourcing)  │  │
│  │   API    │→ │  Handlers    │→ │   CartAggregate            │  │
│  │Endpoints │  │(Command/Query│  │   AggregateRoot            │  │
│  └──────────┘  └──────────────┘  └──────────┬─────────────────┘  │
│                                             │ Domain Events      │
│  ┌──────────────────────────────────────────▼─────────────────┐  │
│  │             Redis Event Store (sorted set)                 │  │
│  │  key: events:cart:{cartId}  score: version  value: JSON    │  │
│  └────────────────────────────────────────────────────────────┘  │
│                             │ CartConfirmedIntegrationEvent      │
│  ┌──────────────────────────▼─────────────────────────────────┐  │
│  │         RabbitMQStreamPublisher (Super Stream)             │  │
│  └──────────────────────────┬─────────────────────────────────┘  │
└─────────────────────────────┼───────────────────────────────────┘
                              │ Binary Stream Protocol (port 5552)
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│         RabbitMQ Super Stream: "shopping-cart-events"           │
│  ┌──────────────────┬──────────────────┬────────────────────┐   │
│  │   Partition 0    │   Partition 1    │    Partition 2     │   │
│  │ (routing hash 0) │ (routing hash 1) │  (routing hash 2)  │   │
│  └────────┬─────────┴────────┬─────────┴──────────┬─────────┘   │
└───────────┼──────────────────┼────────────────────┼─────────────┘
            │                  │                    │
    ┌───────▼──────┐   ┌───────▼──────┐             │ (aynı stream)
    │DiscountService│  │Notification  │◄────────────┘
    │BackgroundSvc │   │Service       │
    │(EF Core +    │   │BackgroundSvc │
    │ SQL Server)  │   │(IMemoryCache │
    └──────────────┘   │ idempotency) │
                       └──────────────┘
```

---

## 2. Servisler ve Sorumluluklar

### 2.1 ShoppingCartService

**Teknoloji:** ASP.NET Core 9 Minimal API · Redis (Event Sourcing) · RabbitMQ.Stream.Client

**Sorumluluk:** Kullanıcı alışveriş sepeti yaşam döngüsünü yönetir. CQRS + Event Sourcing uygular. Sepet onaylandığında tüm sisteme `CartConfirmedIntegrationEvent` yayınlar.

**Katmanlar:**
```
ShoppingCartService/
├── API/
│   ├── Endpoints/          # Minimal API endpoint tanımları
│   ├── Contracts/          # Request / Response DTO'ları
│   └── Filters/            # IdempotencyFilter, ValidationFilter, RateLimitFilter
├── Application/
│   ├── Commands/           # AddItem, RemoveItem, UpdateItemQuantity, ConfirmCart, ClearCart
│   ├── Queries/            # GetCart
│   ├── Events/             # CartConfirmedIntegrationEvent (mesajlaşma için)
│   ├── Common/
│   │   ├── Handlers/       # CommandHandlerBase, QueryHandlerBase
│   │   ├── Exceptions/     # CartDomainException hiyerarşisi
│   │   └── Result.cs       # Result<T> sarıcı
│   ├── DTOs/               # CartDto, CartItemDto
│   └── Interfaces/         # IEventStore, ICartAggregateRepository, IRabbitMQStreamPublisher
├── Domain/
│   ├── Aggregates/         # CartAggregate, AggregateRoot, CartItem
│   └── Events/             # DomainEvent (temel), CartEvents (7 olay türü)
└── Infrastructure/
    ├── EventStore/         # RedisEventStore, EventStoreOptions
    ├── Repositories/       # CartAggregateRepository
    ├── Messaging/          # RabbitMQStreamPublisher, RabbitMQInitializationService
    └── Seed/               # CartSeedData (geliştirme verileri)
```

---

### 2.2 DiscountService

**Teknoloji:** ASP.NET Core 9 Minimal API · Entity Framework Core 9 · SQL Server · RabbitMQ.Stream.Client

**Sorumluluk:** İndirim kuralları ve kupon kodları yönetimi. `CartConfirmedIntegrationEvent` alarak indirim analitiği yapar.

**Katmanlar:**
```
DiscountService/
├── API/Endpoint/           # DiscountRule, CouponCode, DiscountCalculation endpointleri
├── Application/UseCase/    # CalculateDiscount, CouponCode, DiscountRule use case'leri
├── Domain/
│   ├── Entities/           # DiscountRule, CouponCode, Priority
│   └── Repositories/       # Interface tanımları (IDiscountRuleRepository, ICouponCodeRepository)
└── Infrastructure/
    ├── Messaging/          # CartEventConsumer (BackgroundService — Super Stream tüketici)
    ├── Persistence/        # DiscountDbContext (EF Core — SQL Server)
    ├── Repositories/       # EF Core implementasyonları
    └── Seed/               # DiscountRule ve CouponCode seed verileri
```

---

### 2.3 NotificationService

**Teknoloji:** .NET 9 Worker Service · RabbitMQ.Stream.Client · IMemoryCache (idempotency)

**Sorumluluk:** `CartConfirmedIntegrationEvent` alarak kullanıcıya bildirim gönderir. Şu an log tabanlı (`LogNotificationSender`); e-posta/SMS/push için `INotificationSender` arayüzü ile genişletilebilir.

**Katmanlar:**
```
NotificationService/
├── Application/
│   ├── Interfaces/         # INotificationSender
│   └── Services/           # LogNotificationSender
├── Infrastructure/
│   └── Messaging/          # CartNotificationConsumer (BackgroundService — Super Stream tüketici)
└── Models/                 # CartConfirmedEvent, CartItemSnapshot (deserialization)
```

---

## 3. Altyapı (Docker Compose)

```yaml
# docker/docker-compose.yml — tüm bağımlılıklar
```

| Servis | İmaj | Port(lar) | Amaç |
|--------|------|-----------|-------|
| `rabbitmq` | `rabbitmq:3-management` | 5672 (AMQP), **5552 (Stream)**, 15672 (UI) | Super Stream mesajlaşma |
| `redis-master` | `redis:7-alpine` | 6379 | Event Store (birincil) |
| `redis-slave` | `redis:7-alpine` | 6380 | Replikasyon (okuma) |
| `redis-sentinel` | `redis:7-alpine` | 26379 | HA — otomatik failover |
| `sqlserver` | `mssql/server:2022` | 1433 | DiscountService kalıcı depolama |
| `postgresql` | `postgres:16-alpine` | 5432 | Yedek / ileride kullanım |
| `azurite` | `azure-storage/azurite` | 10000-10002 | Azure depolama emülatörü |

### Redis HA Mimarisi

```
┌─────────────────┐     replikasyon     ┌─────────────────┐
│  redis-master   │ ─────────────────►  │   redis-slave   │
│  :6379 (R/W)    │                     │   :6380 (R)     │
└────────┬────────┘                     └────────┬────────┘
         │                                       │
         └───────────────┬───────────────────────┘
                         ▼
               ┌──────────────────┐
               │  redis-sentinel  │  izleme + failover yönetimi
               │  :26379          │
               └──────────────────┘
```

### RabbitMQ Eklentileri

Docker Compose başlatıldığında otomatik olarak etkinleştirilir:

```bash
rabbitmq-plugins enable rabbitmq_stream rabbitmq_stream_management
```

- `rabbitmq_stream`: Binary stream protokolünü açar (port 5552)
- `rabbitmq_stream_management`: Yönetim UI'ına stream istatistikleri ekler

---

## 4. Event Sourcing — Redis Event Store

### 4.1 Temel Fikir

Geleneksel CRUD'da yalnızca **son durum** saklanır. Event Sourcing'de ise nesnenin tüm **tarihçesi** olay dizisi olarak saklanır. Mevcut durum, tüm olaylar sırayla uygulanarak yeniden türetilir.

```
[CartCreated] → [ItemAdded(Elma)] → [ItemAdded(Armut)] → [ItemRemoved(Elma)] → [CartConfirmed]
                                                                                      ↑
                                                            Son durumu hesaplamak için tümü oynatılır
```

### 4.2 Redis Veri Yapısı

Event Store olarak Redis **Sorted Set** kullanılır:

```
Key:    events:cart:{cartId}         → Olayların tutulduğu stream
Score:  version numarası (0, 1, 2…)  → Sıralama + versiyon kontrolü için
Value:  JSON (StoredEventData)        → Serileştirilmiş olay içeriği
```

**Kullanıcı → Sepet İndeksi:**
```
Key:    eventindex:cart:user:{userId}  → String (Redis string)
Value:  cartId (Guid string)
```

Bu indeks sepet ilk oluşturulduğunda (`CartCreatedEvent`) tek seferlik yazılır; `GetByUserIdAsync` sorgusunda kullanıcıdan sepete hızlı erişim sağlar.

### 4.3 StoredEventData Yapısı

Her olay şu formatta saklanır:

```json
{
  "EventId":       "3f7a1c2b-...",
  "AggregateId":   "abc12345-...",
  "AggregateType": "Cart",
  "EventType":     "ItemAddedToCartEvent",
  "EventData":     "{\"CartId\":\"abc...\",\"ProductId\":\"...\",\"Quantity\":2}",
  "Version":       2,
  "OccurredAt":    "2026-03-09T12:00:00Z"
}
```

`EventData` alanı olayın kendi tipine uygun şekilde serileştirilir (`JsonSerializer.Serialize(@event, @event.GetType())`). Deserializasyon sırasında `EventTypeRegistry` yansıma (reflection) ile tip adından somut tipi bulur.

### 4.4 Atomik Lua Scripti — Eşzamanlılık Kontrolü

Redis işlemleri atomik yapılmaması halinde race condition (yarış durumu) yaratır:

```
❌ Tehlikeli yaklaşım:
   1. Versiyon oku
   2. [başka bir istek aynı anda yazabilir!]
   3. Olayları yaz
```

Bu proje **tek bir Lua scripti** ile tüm işlemi atomik hale getirir:

```lua
-- KEYS[1] = events:cart:{cartId}
-- KEYS[2] = eventindex:cart:user:{userId}  (yalnızca ilk oluşturmada)
-- ARGV[1] = beklenen versiyon
-- ARGV[2] = kullanıcı indeks değeri (cartId string)
-- ARGV[3] = TTL (saniye)
-- ARGV[4] = olay sayısı
-- ARGV[5..] = [score1, data1, score2, data2, ...]

local last = redis.call('ZREVRANGEBYSCORE', streamKey, '+inf', '-inf', 'WITHSCORES', 'LIMIT', '0', '1')
local currentVersion = -1
if #last > 0 then
    currentVersion = tonumber(last[2])  -- SCORE = version (JSON parse yok!)
end

if currentVersion ~= expectedVersion then
    return redis.error_reply('Concurrency conflict: expected X but found Y')
end

-- Olayları yaz (ZADD score=version, value=JSON)
for i = 0, numEvents - 1 do
    redis.call('ZADD', streamKey, score, data)
end

-- Kullanıcı indeksini yaz (yalnızca CartCreatedEvent için)
if userIndexValue ~= '' then
    redis.call('SET', userIndexKey, userIndexValue)
end

-- TTL ayarla
redis.call('EXPIRE', streamKey, ttl)
```

**Kritik nokta:** Versiyon kontrolü için `ZREVRANGEBYSCORE WITHSCORES` kullanılır; Score **doğrudan versiyon numarasıdır**. Bu sayede JSON parse edilmeden versiyon karşılaştırması yapılır.

### 4.5 TTL Stratejisi

| Durum | TTL | Konfigürasyon |
|-------|-----|---------------|
| Aktif sepet | 30 gün | `appsettings.json → EventStore.ActiveCartTtlDays` |
| Onaylanmış sepet | 7 gün | `appsettings.json → EventStore.ConfirmedCartTtlDays` |

`CartConfirmedEvent` bulunduğunda kısa TTL uygulanır; aksi takdirde aktif TTL yenilenir.

### 4.6 AggregateRoot — Versiyon Yönetimi

```csharp
public abstract class AggregateRoot
{
    public int Version { get; protected set; } = -1;  // -1: hiç kaydedilmemiş
    private readonly List<DomainEvent> _uncommittedEvents = [];

    protected void AddEvent(DomainEvent @event)
    {
        // Versiyon: kalıcı versiyon + bekleyen olay sayısı + 1
        var nextVersion = Version + _uncommittedEvents.Count + 1;
        _uncommittedEvents.Add(@event with { Version = nextVersion });
        Apply(@event);  // Anında durumu güncelle
    }
}
```

`Version = -1` ile başlar. İlk olay `Version = 0` alır. Redis'ten yüklenirken `Version = en_son_olay.Version` olur. Repository kaydederken `expectedVersion = aggregate.Version` Lua scriptine gönderilir.

---

## 5. CQRS Deseni

### 5.1 Komutlar (Commands) — Durum Değiştirme

```
HTTP Request
    │
    ▼
Endpoint (CartEndpoints.cs)
    │  Command nesnesi oluşturur
    ▼
CommandHandlerBase<TCommand, TResult>
    │  try/catch ile hata yönetimi
    ▼
XxxCommandHandler.ExecuteAsync()
    │  Repository'den aggregate yükle
    │  Aggregate metodunu çağır (iş mantığı)
    │  Repository'ye kaydet (olaylar Redis'e yazılır)
    │  [gerekirse] Integration event yayınla
    ▼
Result<CartDto>  →  HTTP response (200/400/404/409/500)
```

### 5.2 Hata Yönetimi — Result Deseni

`CommandHandlerBase` üç durum için farklı HTTP kodu üretir:

```csharp
catch (CartDomainException ex)       → Result.Failure(msg, ex.StatusCode)   // 404, 409
catch (InvalidOperationException ex) → Result.Failure(msg, 400)
catch (Exception ex)                 → Result.Failure("unexpected", 500)
```

### 5.3 Exception Hiyerarşisi

```
CartDomainException (StatusCode taşır)
├── CartNotFoundException       → 404
├── CartItemNotFoundException   → 404  (sepette ürün yok)
└── CartAlreadyConfirmedException → 409 (onaylanmış sepeti değiştirme)
```

### 5.4 Sorgular (Queries) — Durum Okuma

```csharp
// GetCartQueryHandler: Repository'den aggregate yükle → DTO'ya dönüştür
var cart = await _repository.GetByUserIdAsync(query.UserId, ct);
return CartMapper.ToDto(cart);
```

Sorgular aggregate üzerinde hiçbir olay oluşturmaz; Redis'ten mevcut olay dizisini okur ve son durumu yeniden türetir.

---

## 6. Domain Events (Alan Olayları)

Domain eventler **geçmişte yaşanmış, değiştirilemez olgulardır**. Tüm eventler `DomainEvent` temel record'undan türer:

```csharp
public abstract record DomainEvent
{
    public Guid     EventId    { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string   EventType  => GetType().Name;    // otomatik tip adı
    public int      Version    { get; init; }
}
```

### 6.1 CartAggregate Event Türleri

| Event | Tetikleyici | Etki |
|-------|-------------|------|
| `CartCreatedEvent` | `CartAggregate.Create(userId)` | `Id`, `UserId`, `CreatedAt` set edilir |
| `ItemAddedToCartEvent` | `AddItem(...)` — yeni ürün | `_items` listesine eklenir |
| `ItemQuantityIncreasedEvent` | `AddItem(...)` — mevcut ürün | Mevcut ürünün miktarı artırılır |
| `ItemRemovedFromCartEvent` | `RemoveItem(productId)` | `_items` listesinden çıkarılır |
| `ItemQuantityUpdatedEvent` | `UpdateItemQuantity(productId, qty)` | Mevcut ürün miktarı güncellenir |
| `CartConfirmedEvent` | `Confirm()` | `IsConfirmed = true` |
| `CartClearedEvent` | `Clear()` | `_items.Clear()` |

### 6.2 Event'ten Duruma — Apply Mekanizması

```csharp
// CartAggregate'in command metodunda:
public void AddItem(Guid productId, ...)
{
    EnsureNotConfirmed();                    // 1. İş kuralı kontrolü
    var @event = new ItemAddedToCartEvent(…);
    AddEvent(@event);                        // 2. Olayı oluştur + Apply çağır
}

// AggregateRoot.AddEvent():
_uncommittedEvents.Add(@event with { Version = nextVersion });
Apply(@event);                               // 3. Durumu anında güncelle

// CartAggregate.Apply():
protected override void Apply(DomainEvent @event)
{
    switch (@event)
    {
        case ItemAddedToCartEvent e: ApplyItemAdded(e); break;
        // ...
    }
    UpdatedAt = DateTime.UtcNow;
}

private void ApplyItemAdded(ItemAddedToCartEvent @event)
{
    _items.Add(CartItem.Create(@event.ProductId, @event.ProductName, ...));
}
```

### 6.3 Tarihçeden Yükleme (Event Replay)

```csharp
// CartAggregateRepository.GetByIdAsync():
var events = await eventStore.GetEventsAsync(cartId);
return CartAggregate.LoadFromEvents(events);

// CartAggregate.LoadFromEvents():
public static CartAggregate LoadFromEvents(IEnumerable<DomainEvent> events)
{
    var cart = new CartAggregate();
    cart.LoadFromHistory(events);   // AggregateRoot.LoadFromHistory()
    return cart;
}

// AggregateRoot.LoadFromHistory():
foreach (var @event in history)
{
    Apply(@event);           // Her olayı uygula (state rebuild)
    Version = @event.Version; // Versiyonu güncelle
}
```

---

## 7. Integration Events ve RabbitMQ Super Streams

### 7.1 Domain Event vs Integration Event Farkı

| | Domain Event | Integration Event |
|--|---|---|
| **Kapsam** | Tek servis içi | Servisler arası |
| **Zenginlik** | Minimal (sadece ne oldu) | Zengin (tüketicinin ihtiyacı olan her şey) |
| **Taşıma** | Redis event store | RabbitMQ Super Stream |
| **Örnek** | `CartConfirmedEvent(CartId, TotalAmount, TotalItems, ConfirmedAt)` | `CartConfirmedIntegrationEvent(CartId, UserId, TotalAmount, TotalItems, Items[], ConfirmedAt)` |

`CartConfirmedEvent` (domain) `IsConfirmed = true` yapmak için gerekli minimum bilgiyi taşır. `CartConfirmedIntegrationEvent` ise tüketicilerin (DiscountService, NotificationService) ihtiyacı olan `UserId` ve `Items[]` snapshot'ını da içerir.

### 7.2 RabbitMQ Super Streams

Super Stream, bir **mantıksal stream'in fiziksel partisyonlara bölünmesidir**. Kafka'daki topic-partition mimarisinin RabbitMQ karşılığıdır.

```
"shopping-cart-events" (Super Stream)
├── shopping-cart-events-0   (Partition 0 — gerçek queue)
├── shopping-cart-events-1   (Partition 1 — gerçek queue)
└── shopping-cart-events-2   (Partition 2 — gerçek queue)
```

**Avantajları:**
- **Paralel tüketim:** Her tüketici tüm partisyonları okur (fan-out)
- **Ölçeklenebilirlik:** Partisyon sayısı artırılarak yük dağıtılır
- **Sıralama garantisi:** Aynı routing key her zaman aynı partisyona gider
- **Binary protokol:** AMQP 0-9-1'den daha hızlı (port 5552)

### 7.3 Routing — Partisyon Seçimi

Mesaj hangi partisyona gideceği `MessageId` (routing key) ile belirlenir:

```csharp
// RabbitMQStreamPublisher.cs
var producerConfig = new ProducerConfig(_streamSystem, _streamName)
{
    SuperStreamConfig = new SuperStreamConfig
    {
        Routing = message => message.Properties.MessageId?.ToString()
                             ?? Guid.NewGuid().ToString()
    }
};

// Yayınlama sırasında:
var message = new Message(body)
{
    Properties = new Properties
    {
        MessageId = cart.Id.ToString()   // CartId routing key olarak kullanılır
    }
};
```

Aynı `CartId`'ye ait mesajlar her zaman aynı partisyona gider. Bu, partisyon bazında sıralı işleme garantisi sağlar.

### 7.4 Super Stream Oluşturma

```csharp
await _streamSystem.CreateSuperStream(
    new PartitionsSuperStreamSpec("shopping-cart-events", partitions: 3)
);
```

`PartitionsSuperStreamSpec` constructor'a partisyon sayısı alır (readonly property, sonradan değiştirilemez).

### 7.5 Tüketici Yapılandırması

Her consumer **tüm partisyonları** aynı anda dinler (`IsSuperStream = true`):

```csharp
// DiscountService ve NotificationService (aynı pattern)
_consumer = await Consumer.Create(new ConsumerConfig(_streamSystem, streamName)
{
    IsSuperStream = true,
    OffsetSpec = new OffsetTypeNext(),   // Yalnızca yeni mesajlardan başla
    MessageHandler = async (stream, _, _, message) =>
    {
        await ProcessMessageAsync(message, stream);
    }
});
```

`OffsetTypeNext()`: Consumer başladıktan sonra gelen mesajları okur; geçmiş mesajları atlayarak işleme başlar.

### 7.6 Mesaj Deserialization

```csharp
// message.Data.Contents bir ReadOnlySequence<byte>'tır
var contents = message.Data.Contents;
var json = Encoding.UTF8.GetString(
    contents.IsSingleSegment
        ? contents.First.Span                                      // hızlı yol (tek segment)
        : System.Buffers.BuffersExtensions.ToArray(contents));     // çok-segment fallback

var cartEvent = JsonSerializer.Deserialize<CartConfirmedEvent>(json);
```

### 7.7 Thread Safety — Publisher

İki paralel istek aynı anda publisher'ı ilk kez kullanmaya çalışırsa double-init sorununu `SemaphoreSlim` önler:

```csharp
private readonly SemaphoreSlim _initLock = new(1, 1);

public async Task PublishAsync<T>(T @event, ...)
{
    if (!_initialized || _producer is null)   // 1. Hızlı kontrol (lock olmadan)
    {
        await _initLock.WaitAsync(ct);
        try
        {
            if (!_initialized || _producer is null)  // 2. Lock sonrası tekrar kontrol
                await InitializeAsync(ct);
        }
        finally { _initLock.Release(); }
    }
    // Yayınla...
}
```

---

## 8. Tam Mesaj Akışı: Sepet Onaylama

Sistemin nasıl çalıştığını adım adım izleyelim:

```
1. CLIENT → POST /api/carts/{userId}/confirm
   Header: X-Idempotency-Key: abc-123

2. IdempotencyFilter
   → Redis'te "idempotency_abc-123" anahtarı var mı?
   → HAYIR: devam et

3. ConfirmCartCommandHandler.ExecuteAsync()
   a. repository.GetByUserIdAsync(userId)
      → eventStore.GetCartIdByUserIdAsync(userId)
        → Redis: GET eventindex:cart:user:{userId}  →  cartId
      → eventStore.GetEventsAsync(cartId)
        → Redis: ZRANGEBYSCORE events:cart:{cartId} 0 +inf
        → [CartCreated, ItemAdded, ItemAdded] olaylarını al
      → CartAggregate.LoadFromEvents(events)
        → Her olayı Apply() ile uygula → mevcut durum rebuild edilir

   b. cart.Confirm()
      → EnsureNotConfirmed() — daha önce onaylanmış mı?
      → _items boş mu kontrol
      → CartConfirmedEvent(CartId, TotalAmount, TotalItems, ConfirmedAt) oluştur
      → AddEvent(event)
        → Version: 2 → 3
        → _uncommittedEvents'e ekle
        → Apply(CartConfirmedEvent) → IsConfirmed = true

   c. repository.SaveAsync(cart)
      → cart.UncommittedEvents: [CartConfirmedEvent{Version=3}]
      → eventStore.SaveEventsAsync(cartId, "Cart", events, expectedVersion=2)
        → Lua scripti çalıştır:
          KEYS: ["events:cart:{cartId}", ""]
          ARGV: [2, "", 604800, 1, 3, "{...CartConfirmedEvent JSON...}"]
          ─────────────────────────────────
          1. ZREVRANGEBYSCORE events:cart:{cartId} +inf -inf WITHSCORES LIMIT 0 1
             → currentVersion = 2
          2. 2 == 2 (beklenen == mevcut)? EVET
          3. ZADD events:cart:{cartId} 3 "{...}"
          4. EXPIRE events:cart:{cartId} 604800  (7 gün — onaylanmış sepet)
          ─────────────────────────────────
          → OK döner
      → cart.ClearUncommittedEvents()

   d. CartConfirmedIntegrationEvent oluştur
      (CartId, UserId, TotalAmount, TotalItems, Items[snapshot], ConfirmedAt)

   e. streamPublisher.PublishAsync(integrationEvent, routingKey=cartId.ToString())
      → JSON serialize
      → Message(body) { Properties = { MessageId = cartId } }
      → producer.Send(message)
        → RabbitMQ: cartId hash'ine göre partisyon seçilir (örn: Partition 1)

4. IdempotencyFilter (sonuç)
   → HTTP 200 OK alındı
   → Redis: SET idempotency_abc-123 {StatusCode:200, Body:{...}} EX 86400

5. CLIENT ← HTTP 200 OK  {cart: {...}}

────────────────────────────────────────────────────────────────
  AYNI ANDA — RabbitMQ Partition 1'deki mesaj tüketicilere gider
────────────────────────────────────────────────────────────────

6a. DiscountService.CartEventConsumer.ProcessMessageAsync()
    → Mesajı deserialize et: CartConfirmedEvent{CartId, UserId, TotalAmount, ...}
    → İndirim analitiği (loglama / kupon kontrolü)

6b. NotificationService.CartNotificationConsumer.ProcessMessageAsync()
    → Mesajı deserialize et: CartConfirmedEvent{...}
    → Idempotency: IMemoryCache'de "notified:cart:{cartId}" var mı?
       HAYIR: işle + cache'e ekle (1440 dk TTL)
    → INotificationSender.SendAsync(cartEvent)
       → LogNotificationSender: detaylı log yaz
       [TODO: e-posta/SMS/push için başka implementasyon bağlanabilir]
```

---

## 9. Outbox Deseni — Mevcut Yaklaşım ve Sınırları

### 9.1 Outbox Nedir?

Geleneksel outbox deseni şöyle çalışır:

```
1. Veritabanı işlemi BAŞLA
2. Domain durumu güncelle
3. Outbox tablosuna "gönderilecek mesaj" yaz
4. İşlem COMMIT
5. Ayrı bir worker outbox'ı okur → mesajı broker'a gönderir → outbox kaydını siler
```

Bu sayede veritabanı işlemi + mesaj yayınlama atomik hale gelir.

### 9.2 Bu Projedeki Yaklaşım

Bu projede **klasik outbox tablosu yoktur**. Bunun yerine:

```
1. Redis atomic Lua scripti → event'i stream'e yazar (atomik)
2. ConfirmCartCommandHandler → repository.SaveAsync() tamamlandıktan SONRA streamPublisher.PublishAsync() çağrılır
3. Yayınlama başarısız olursa: hata loglanır, cart zaten confirmed durumunda kalır
```

```csharp
// ConfirmCartCommandHandler.cs
await repository.SaveAsync(cart, cancellationToken);        // ← Redis'e yazıldı

try
{
    await streamPublisher.PublishAsync(integrationEvent, ...); // ← RabbitMQ'ya gönder
}
catch (Exception ex)
{
    // ❗ Yayınlama başarısız → hata loglanır AMA cart confirmed kalır
    Logger.LogError(ex, "Failed to publish CartConfirmedIntegrationEvent...");
}
```

### 9.3 Mevcut Yaklaşımın Sınırı

Redis'e yazma başarılı → RabbitMQ'ya gönderme başarısız olursa:
- Sepet Redis'te "onaylanmış" olarak kalır ✓
- DiscountService ve NotificationService bu olayı **asla almaz** ✗
- Mesaj kaybolur, otomatik yeniden deneme olmaz

**Gerçek Outbox İmplementasyonu için:**
- Redis event store'a "pending_integration_events" listesi eklenebilir
- Ayrı bir `IntegrationEventPublisher` background service bu listeyi okuyup yayınlayabilir
- Başarılı yayınlama sonrası liste temizlenir

---

## 10. Idempotency (Tekrar İşlem Güvenliği)

### 10.1 ShoppingCartService — Redis Tabanlı

Tüm mutasyon endpointleri `X-Idempotency-Key` header'ını destekler:

```http
POST /api/carts/{userId}/confirm
X-Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
```

`IdempotencyFilter` Redis'te yanıtı `{StatusCode, Body}` çifti olarak 24 saat saklar:

```
İlk istek:
  Redis: GET idempotency_{key}  →  (nil)
  İşlemi gerçekleştir
  Redis: SET idempotency_{key} {"StatusCode":200,"Body":{...}} EX 86400

Tekrar istek (aynı key):
  Redis: GET idempotency_{key}  →  {"StatusCode":200,"Body":{...}}
  Response header: X-Idempotent-Replayed: true
  HTTP: 200 OK  (orijinal yanıt)   ← status code KORUNUR
```

**Kritik:** Önceki implementasyonda tekrarlanan yanıt her zaman `200 OK` dönüyordu. Mevcut versiyon `CachedIdempotentResponse(StatusCode, Body)` ile orijinal HTTP kodunu korur.

### 10.2 NotificationService — IMemoryCache Tabanlı

Aynı `CartId` için iki kez bildirim gönderilmesini engeller:

```csharp
var cacheKey = $"notified:cart:{cartEvent.CartId}";
if (_cache.TryGetValue(cacheKey, out _))
{
    _logger.LogDebug("Duplicate message skipped");
    return;
}
_cache.Set(cacheKey, true, TimeSpan.FromMinutes(1440)); // 24 saat
```

> **Not:** In-memory cache yeniden başlatmada temizlenir. Dağıtık ortamda Redis tabanlı idempotency daha güvenilirdir.

---

## 11. API Endpoints

Tüm endpointler `/api/carts` grubu altındadır.

| Method | Path | Açıklama | Filtreler |
|--------|------|----------|-----------|
| `GET` | `/api/carts/{userId}` | Sepet detayı | Rate limit (gevşek), Dağıtık cache |
| `POST` | `/api/carts/{userId}/items` | Ürün ekle | Rate limit, Idempotency, Validation |
| `DELETE` | `/api/carts/{userId}/items/{productId}` | Ürün sil | Rate limit |
| `PUT` | `/api/carts/{userId}/items/{productId}` | Ürün miktarı güncelle | Rate limit, Validation |
| `POST` | `/api/carts/{userId}/confirm` | Sepeti onayla | Rate limit (katı), Idempotency |
| `POST` | `/api/carts/{userId}/clear` | Sepeti temizle | Rate limit, Idempotency |

### Rate Limit Profilleri

| Profil | Limit |
|--------|-------|
| Varsayılan | 100 istek/dk |
| Katı (`StrictRateLimit`) | 10 istek/dk |
| Gevşek (`RelaxedRateLimit`) | 500 istek/dk |
| Burst | 20 istek/10 sn |

---

## 12. Güvenlik ve Konfigürasyon

### 12.1 Ortam Bazlı Konfigürasyon

```
appsettings.json              ← placeholder (commit edilir)
appsettings.Development.json  ← gerçek dev credentials (git-ignored)
```

**`.gitignore`'da:**
```
appsettings.Development.json
docker/.env
```

### 12.2 Önemli Konfigürasyon Anahtarları (ShoppingCartService)

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379,password=...,abortConnect=false"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 5552,
    "Username": "guest",
    "Password": "...",
    "StreamName": "shopping-cart-events",
    "Partitions": 3
  },
  "EventStore": {
    "ActiveCartTtlDays": 30,
    "ConfirmedCartTtlDays": 7
  },
  "RateLimiting": {
    "DefaultRequestsPerMinute": 100,
    "StrictRequestsPerMinute": 10
  },
  "DistributedCache": {
    "ShortExpirationMinutes": 1,
    "MediumExpirationMinutes": 10
  }
}
```

### 12.3 Docker Ortam Değişkenleri

`docker/.env` dosyası (git'e commit edilmez):

```env
RABBITMQ_USER=admin
RABBITMQ_PASS=güçlüşifre
REDIS_PASSWORD=güçlüşifre
SA_PASSWORD=SqlServer@2024
POSTGRES_PASSWORD=güçlüşifre
```

---

## Özet: Kritik Tasarım Kararları

| Karar | Neden |
|-------|-------|
| Event Sourcing (Redis Sorted Set) | Tam denetim izi, zamana yolculuk, durum yeniden türetme |
| Lua atomik script | Check-then-act race condition'ını sıfırlar |
| Score = Version numarası | Lua'da JSON parse gerektirmez → hızlı, basit |
| Super Stream (3 partisyon) | Fan-out: tüm tüketiciler tüm mesajları alır; paralel işleme |
| CartId = routing key | Aynı sepete ait mesajlar sıralı işlenir |
| Domain Event ≠ Integration Event | Domain eventler minimal; integration eventler tüketiciye göre zenginleştirilir |
| Redis Sentinel (1 master + 1 slave) | Event Store yüksek erişilebilirliği |
| `X-Idempotency-Key` (Redis backed) | Ağ hatası sonrası istemci güvenle tekrar deneyebilir |
| `INotificationSender` arayüzü | E-posta/SMS/push kanalı DI ile değiştirilebilir, test edilebilir |

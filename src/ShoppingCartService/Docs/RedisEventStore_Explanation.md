# RedisEventStore — Mimari ve Teknik Dokümantasyon

> **İlgili Sınıf:** `ShoppingCartService.Infrastructure.EventStore.RedisEventStore`  
> **Teknoloji Yığını:** Redis (Sorted Set, String) · Lua Scripting · Event Sourcing · C# Reflection

---

## İçindekiler

1. [Genel Bakış ve Amaç](#1-genel-bakış-ve-amaç)
2. [Redis Veri Yapıları](#2-redis-veri-yapıları)
3. [Atomik İşlem (Lua Scripting)](#3-atomik-işlem-lua-scripting)
4. [Konfigürasyon ve TTL Yönetimi](#4-konfigürasyon-ve-ttl-yönetimi)
5. [Dinamik Tip Çözümleme (EventTypeRegistry)](#5-dinamik-tip-çözümleme-eventtyperegistry)
6. [CQRS ve Aggregate ile Entegrasyon](#6-cqrs-ve-aggregate-ile-entegrasyon)
7. [Özet ve Kazanımlar](#7-özet-ve-kazanımlar)

---

## 1. Genel Bakış ve Amaç

`RedisEventStore`, **ShoppingCartService** (Alışveriş Sepeti Servisi) içerisinde Event Sourcing altyapısının temelini oluşturur. 

Geleneksel veritabanı CRUD işlemlerinde yalnızca nesnenin **son durumu (state)** saklanır. `RedisEventStore` ile uygulanan Event Sourcing yaklaşımında ise, sepetin varoluşundan itibaren geçirdiği tüm olayların (`CartCreatedEvent`, `ItemAddedEvent`, vb.) kronolojik bir tarihçesi tutulur. Mevcut duruma ulaşmak için bu olaylar sırayla yeniden oynatılır (replay).

```
[CartCreated] → [ItemAdded(A)] → [ItemAdded(B)] → [ItemRemoved(B)] → [CartConfirmed]
                                                                          ↑
                                                        Tüm olaylar replay edilerek son durum bulunur
```

---

## 2. Redis Veri Yapıları

Performans ve tutarlılık sağlamak amacıyla iki farklı Redis veri yapısı bir arada kullanılır:

### 2.1 Sorted Set (Sıralı Küme) — Event Stream
Sepet olayları için ayrılan ana kayıttır.

- **Key:** `events:cart:{aggregateId}`
- **Score:** Olayın versiyon numarası (`0, 1, 2...`)
- **Value:** Serileştirilmiş JSON içeriği (`StoredEventData`)

*Neden Sorted Set?* Yeni eklenen olaylar otomatik olarak versiyon sırasına göre dizilir. Ayrıca `ZREVRANGEBYSCORE` komutu ile en yüksek (son) versiyon numarası logaritmik zamanda (çok hızlı) bulunabilir.

### 2.2 String — User-to-Cart Index
Bir kullanıcının aktif sepetini sorgularken Event Sourcing yapısını tüm sepetler üzerinde taramamak için oluşturulmuştur.

- **Key:** `eventindex:cart:user:{userId}`
- **Value:** `cartId` (Guid formatında)

Sepet ilk kez oluşturulduğunda (`CartCreatedEvent` geldiğinde) buraya indeks yazılır.

---

## 3. Atomik İşlem (Lua Scripting)

Event Sourcing'in en kritik problemi **Concurrency (Eşzamanlılık)** yönetimidir. İki farklı isteğin (örneğin kullanıcının aynı anda hem web'ten hem mobilden düğmeye basması) aynı sepet için işlem yapmaya çalışması veri tutarsızlığına yol açabilir (Race Condition).

Bunu önlemek için Redis içerisinde **tek bir atomik ağ adımında** koşan özel bir Lua Script (`AtomicSaveScript`) kullanılır:

```lua
-- ARGV[1]: Beklenen Versiyon
-- ARGV[2]: User Index (sadece yeni sepetlerde dolu)
-- ARGV[3]: TTL Süresi

-- 1. Son versiyonu hızlıca al (JSON parse etmeden, sadece SCORE okunur)
local last = redis.call('ZREVRANGEBYSCORE', streamKey, '+inf', '-inf', 'WITHSCORES', 'LIMIT', '0', '1')
local currentVersion = -1
if #last > 0 then
    currentVersion = tonumber(last[2])
end

-- 2. Concurrency (Optimistic Locking) Kontrolü
if currentVersion ~= expectedVersion then
    return redis.error_reply('Concurrency conflict: expected ' .. expectedVersion .. ' but found ' .. currentVersion)
end

-- 3. Olayları Ekle
for i = 0, numEvents - 1 do
    redis.call('ZADD', streamKey, score, data)
end

-- 4. User İndeksini Kaydet
if userIndexValue ~= '' then
    redis.call('SET', userIndexKey, userIndexValue)
end

-- 5. TTL Güncelle
if ttl > 0 then
    redis.call('EXPIRE', streamKey, ttl)
end
```

**Kritik Nokta:** `ZREVRANGEBYSCORE` kullanılarak versiyon numarası JSON dosyasının içinden okunmak yerine direkt index'ten (Score'dan) alınır. Bu "Deserialization" yükünü Redis üzerinden alarak büyük performans kazancı sağlar.

---

## 4. Konfigürasyon ve TTL Yönetimi

Sepet verilerinin veritabanını şişirmesini veya arka planda temizlik işlerine (Cron Job) ihtiyaç duymasını engellemek için doğrudan Redis TTL (Time-To-Live) mekanizması kullanılır.

`appsettings.json` içerisindeki `EventStore` sekmesinden beslenen strateji:

| Durum | Varsayılan TTL | Konfigürasyon Anahtarı |
|-------|----------------|------------------------|
| **Aktif Sepet** | 30 Gün | `ActiveCartTtlDays` |
| **Onaylanmış Sepet** | 7 Gün | `ConfirmedCartTtlDays` |

Eğer kaydı yapılan olaylar içinde `CartConfirmedEvent` bulunuyorsa (sipariş tamamlanmışsa), Lua script'e kısa olan TTL gönderilir. Aksi takdirde, aktif sepet kullanımda olduğu her kayıtta (örneğin sepete yeni ürün eklendiğinde) TTL süresi 30 gün olarak yenilenir.

---

## 5. Dinamik Tip Çözümleme (EventTypeRegistry)

Kaydedilen olayın string/JSON formatından C# class instance'ına (nesnesine) geri çevrilmesi (Deserialization) kritik bir aşamadır:

```json
{
  "EventId": "...",
  "EventType": "ItemAddedToCartEvent",
  "EventData": "{ \"ProductId\": \"...\", \"Quantity\": 2 }"
}
```

Bu JSON string'i alındığında uygulamanın hangi `Type`'a deserialize edeceğini bilmesi gerekir. Bunun için thread-safe, static yapıda bir `EventTypeRegistry` geliştirilmiştir:

- Uygulama başlangıcında **Reflection** (Yansıma) kullanarak assembly ortamını tarar.
- İçerisinde `DomainEvent` mirasçısı (kalıtım alan) olan tüm somut (concrete) sınıfları bulur.
- Dictionary (`Dictionary<string, Type>`) içerisine kaydeder.
- Sistem JSON'dan okuduğunda, `EventType` alanındaki metni kullanarak Dictionary'den ilgili C# tipini bularak doğru Type cast'i gerçekleştirir. Yeni oluşturulan event'leri de otomatik tespit edebildiği için genişlemeye çok uygundur.

---

## 6. CQRS ve Aggregate ile Entegrasyon

Sistemin geri kalanı (API Controller, Command Handler veya Aggregate nesnesi) Redis'in detayları ile uğraşmaz, her şey Dependency Injection ve EventStore Interface'i aracılığı ile yapılır:

```
┌─────────────────┐       ┌──────────────────────┐       ┌────────────────────────┐
│ Command Handler │ ───►  │ CartAggregate (Root) │ ───►  │ RedisEventStore (Save) │
└─────────────────┘       └──────────────────────┘       └────────────────────────┘
         ▲                                                           ▼
         │                                                      Redis Server
         │                                                      (Lua Script)
┌─────────────────┐       ┌──────────────────────┐                   ▲
│ Get Cart Query  │ ◄───  │ CartAggregate (Load) │ ◄─────────────────┘
└─────────────────┘       └──────────────────────┘       (GetEventsAsync)
```

1. **Komut Akışı (Write):** İlgili `CommandHandler` nesneyi işlemeye başladığında ve "Sepet Onayla" aksiyonunu gerçekleştirdiğinde, Aggregate internal listesinde olayları biriktirir (`UncommittedEvents`). En son adımda repository `SaveEventsAsync(...)` diyerek bu olayları ve mevcut versiyonu Redis'e gönderir.
2. **Sorgu Akışı (Read-Hydration):** Kullanıcı sepeti listelemeyi istediğinde, `GetEventsAsync(...)` çağrısı ile tüm olaylar Redis'ten listelenir. Bu listelenen nesnelerin hepsi sırasıyla boş bir `CartAggregate`'in `.Apply(event)` metoduna gönderilerek sepetin nihai durumu hesaplanır.

---

## 7. Özet ve Kazanımlar

- **Zaman Kazancı & Performans:** SQL'deki "Row/Table Lock" veya Entity'deki uzun "Update" işlemlerinin aksine, EventStore mimarisi "Append-Only" yani "Sadece Ekle" üzerine kuruludur ve son derece hızlıdır.
- **Güvenli Eşzamanlılık:** Race condition'lardan doğacak hatalar Redis Lua Script ile bertaraf edilmiştir.
- **Otomatik Ölçeklenebilirlik ve Temizlik:** Disk dolması riskine karşı TTL mekanizması tam performanslı ve iş yüksüz koruma sağlar. Standart bir Background Worker (hangfire vb.) silme ihtiyacını sıfırlamıştır.
- **Tarihsel İzlenebilirlik:** Sepetin yaşam döngüsündeki (ne eklendi, ne zaman çıkarıldı) her şey %100 oranında takip edilebilir, analiz ve hata ayıklamalar için eşsiz bir log ortamı sunar.

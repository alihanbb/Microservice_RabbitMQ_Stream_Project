# RabbitMQ Stream Microservices Projesi - Yapı Analizi

## 📋 Proje Genel Bilgisi

**Proje Adı:** Microservice_RabbitMQ_Stream_Project  
**Teknoloji Stack:** .NET 8/9, RabbitMQ Stream, Docker  
**Çerçeve:** Microservices Architecture  

---

## 🏗️ Servisler (Services)

### 1. **ShoppingCartService** (Ana Servis)
- **Framework:** ASP.NET Core Web API (net9.0)
- **Mimarı:** Clean Architecture (API, Application, Domain, Infrastructure)
- **Özellikler:**
  - API Versioning (Asp.Versioning)
  - Swagger/OpenAPI dokumentasyonu
  - FluentValidation
  - StackExchange.Redis entegrasyonu
  - Global Exception Handling
  - Correlation ID & Request Logging Middleware
  - Endpoints-based API yapısı

**Klasör Yapısı:**
```
ShoppingCartService/
├── API/                      # API katmanı
│   ├── Configuration/
│   ├── Contracts/           # Request/Response DTOs
│   ├── Endpoints/           # Endpoint tanımlamaları
│   ├── Filters/             # Validation filters
│   └── Middleware/          # Custom middleware
├── Application/             # Business logic
├── Domain/                  # Domain entities & rules
├── Infrastructure/          # Data access & external services
├── Extensions/              # Service collection extensions
├── Program.cs
└── appsettings*.json
```

### 2. **DiscountService**
- **Framework:** ASP.NET Core Web API (net9.0)
- **Mimarı:** Clean Architecture (API, Application, Domain, Infrastructure)
- **Özellikler:**
  - Entity Framework Core (SQL Server)
  - Repository pattern
  - Use Cases (CQRS benzeri)
  - Seed data desteği
  - Validation filters

**Klasör Yapısı:**
```
DiscountService/
├── API/
│   ├── Contracts/
│   ├── Endpoint/
│   ├── Filters/
│   └── MiddleWare/
├── Application/
│   ├── Response/
│   └── UseCase/
├── Domain/
│   ├── Entities/
│   ├── Exceptions/
│   └── Repositories/
├── Infrastructure/
│   ├── Persistence/       # DbContext
│   ├── Repositories/
│   └── Seed/
└── Program.cs
```

### 3. **BackupService** & **BackupServices**
- **Framework:** Azure Functions Worker (net8.0 / net9.0)
- **Amaç:** Veri backup işlemleri
- **Özellikleri:** Azure Functions, Application Insights

### 4. **NotificationService**
- **Framework:** Azure Functions Worker (net9.0)
- **Amaç:** Event-driven notification hizmeti
- **Özellikleri:** Azure Functions, RabbitMQ Stream entegrasyonu

---

## 🐳 Docker Ekosistemi

**Docker Compose Hizmetleri:**

| Hizmet | İmaj | Port(lar) | Amaç |
|--------|------|----------|------|
| **rabbitmq** | rabbitmq:3-management | 5672, 5552, 15672 | Message Broker (Stream + AMQP) |
| **azurite** | mcr.microsoft.com/azure-storage/azurite | 10000-10002 | Azure Storage emulator |
| **sqlserver** | mssql/server:2022-latest | 1433 | Ana SQL Server |
| **sqlserver-backup** | mssql/server:2022-latest | 1434 | Backup SQL Server |
| **redis-master** | redis:7-alpine | 6379 | Redis Primary |
| **redis-slave** | redis:7-alpine | 6380 | Redis Replica |
| **redis-sentinel** | redis:7-alpine | 26379 | Redis Sentinel |
| **postgresql** | postgres:16-alpine | 5432 | Ana PostgreSQL |
| **postgresql-backup** | postgres:16-alpine | 5433 | Backup PostgreSQL |

**Network:** microservices-network (bridge driver)

---

## 📦 Temel Bağımlılıklar

### Ortak:
- ✅ Microsoft.AspNetCore.OpenApi
- ✅ FluentValidation
- ✅ Entity Framework Core

### ShoppingCartService:
- ✅ Swashbuckle.AspNetCore (Swagger UI)
- ✅ Asp.Versioning.Http & Mvc.ApiExplorer
- ✅ StackExchange.Redis

### DiscountService:
- ✅ Microsoft.EntityFrameworkCore.SqlServer
- ✅ Microsoft.EntityFrameworkCore.Tools

### Azure Services:
- ✅ Microsoft.Azure.Functions.Worker
- ✅ Microsoft.Azure.Functions.Worker.ApplicationInsights

---

## 🔗 İletişim Akışı

```
ShoppingCartService
    ↓
  RabbitMQ Stream (5552)
    ↓
  NotificationService (Event consumer)
  BackupService (Event consumer)
  DiscountService (Event consumer)
```

---

## 🗄️ Veri Depolama

- **SQL Server:** ShoppingCart, Discount data
- **PostgreSQL:** Alternatif veri depolama
- **Redis:** Caching (Master-Slave-Sentinel setup)
- **Azurite:** Azure Storage emulation

---

## 📂 Proje Dosyası Yapısı

```
Microservice_RabbitMQ_Stream_Project/
├── .github/                    # GitHub Actions workflows
├── .vs/                        # Visual Studio cache
├── docker/
│   ├── docker-compose.yml     # Konteyner konfigürasyonu
│   └── .dockerignore
├── src/
│   ├── ShoppingCartService/   # Ana Web API
│   ├── DiscountService/       # İndirim servisi
│   ├── BackupService/         # Azure Functions (v4)
│   ├── BackupServices/        # Azure Functions (v4)
│   └── NotificationService/   # Azure Functions (v4)
├── Microservice_RabbitMQ_Stream_Project.slnx  # Çözüm dosyası
├── .gitignore
├── .gitattributes
└── README / docs (varsa)
```

---

## 🚀 Başlatma & Çalıştırma

### Containerları Başlat:
```bash
docker-compose -f docker/docker-compose.yml up -d
```

### Servisler:
```bash
# ShoppingCartService
cd src/ShoppingCartService && dotnet run

# DiscountService
cd src/DiscountService && dotnet run

# Azure Functions
func start --csharp (BackupService / NotificationService içinden)
```

### Erişim Noktaları:
- **RabbitMQ Management:** http://localhost:15672 (guest/guest)
- **ShoppingCartService API:** https://localhost:7xxx (swagger)
- **DiscountService API:** https://localhost:7yyy (swagger)
- **PostgreSQL:** localhost:5432
- **SQL Server:** localhost:1433

---

## 📝 Notlar

1. **API Versioning:** ShoppingCartService v1 support'lu
2. **Clean Architecture:** DiscountService tam implementasyon, ShoppingCartService temel yapı
3. **Event-Driven:** RabbitMQ Stream üzerinden asenkron iletişim
4. **Backup:** SQL Server + PostgreSQL dual setup
5. **Caching:** Redis with Sentinel HA configuration

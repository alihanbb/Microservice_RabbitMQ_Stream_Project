# Microservice RabbitMQ Stream Project

A robust, event-driven microservices architecture built with **.NET 9**, demonstrating advanced patterns such as **CQRS**, **Event Sourcing** with **Redis**, and high-performance messaging using **RabbitMQ Super Streams**.

## 🚀 Key Features

*   **.NET 9 & Minimal APIs:** Building lightning-fast HTTP endpoints using the latest ASP.NET Core framework.
*   **Event Sourcing (Redis):** Instead of saving just the final state, every change in the Shopping Cart is saved as an event in a Redis Sorted Set. This allows for perfect audit trails, debugging, and state reconstruction.
*   **CQRS (Command Query Responsibility Segregation):** Strict separation between read (Query) and write (Command) operations for scale and clarity.
*   **RabbitMQ Super Streams:** Kafka-like partition-based messaging for massive throughput, parallel processing, and guaranteed ordering for integration events.
*   **Idempotency & Rate Limiting:** Built-in safeguards against duplicate requests (network retries) and API abuse.
*   **Clean Architecture:** Domain-Driven Design (DDD) principles with clear boundaries between API, Application, Domain, and Infrastructure layers.
*   **Observability:** Unified logging and telemetry using **Serilog**, **OpenTelemetry**, and **Prometheus**.

## 🏗️ Architecture & Services

The heavily decoupled architecture consists of the following primary microservices:

### 1. 🛡️ Bff.Gateway (YARP)
*   **Tech:** YARP (Yet Another Reverse Proxy)
*   **Role:** Single entry point for the frontend/mobile clients. Routes traffic to internal services, handles cross-cutting concerns like observability and potentially authentication.
*   **Routes:**
    *   `/api/carts/**` -> `ShoppingCartService`
    *   `/api/discounts/**` -> `DiscountService`

### 2. 🛒 ShoppingCartService (Core API)
*   **Tech:** ASP.NET Core Minimal API, Redis Event Store
*   **Role:** Manages the lifecycle of user shopping carts via CQRS and Event Sourcing.
*   **Action:** When a cart is successfully checked out/confirmed, it publishes a rich `CartConfirmedIntegrationEvent` to the RabbitMQ Super Stream.

### 3. 💸 DiscountService (Consumer/API)
*   **Tech:** ASP.NET Core Minimal API, EF Core 9, SQL Server
*   **Role:** Manages discount rules and coupon codes. Listens to cart confirmation events for analytics and processing.

### 4. 🔔 NotificationService (Consumer)
*   **Tech:** .NET Worker Service, In-Memory Cache (Idempotency)
*   **Role:** Listens to the same stream to send out necessary notifications (Email, SMS, Push) ensuring users are notified exactly once per action.

---

> For an in-depth dive into the technical implementation, patterns (Lua scripts), and data flows, please read the [Architecture Documentation](ARCHITECTURE.md).

---

## 🛠️ Infrastructure Stack (Docker Compose)

The entire environment runs via Docker Compose, provisioning all necessary databases and brokers.

| Service | Image | Ports | Purpose |
| :--- | :--- | :--- | :--- |
| **RabbitMQ** | `rabbitmq:3-management` | `5672`, `15672` (UI), **`5552` (Stream)** | Message Broker (Super Streams Enabled) |
| **Redis Master**| `redis:7-alpine` | `6379` | Event Store Primary Node |
| **Redis Slave** | `redis:7-alpine` | `6380` | High-Availability Read Replica |
| **Redis Sentinel**| `redis:7-alpine` | `26379` | Monitor & Automatic Failover |
| **SQL Server** | `mssql/server:2022` | `1433` | Persistence for DiscountService |
| **PostgreSQL** | `postgres:16-alpine` | `5432` | Persistence for future usage |
| **Azurite** | `azure-storage/azurite` | `10000-10002` | Local Azure Storage Emulator |
| **Prometheus** | `prom/prometheus` | `9090` | Metrics Collection |

## ⚙️ Getting Started

### Prerequisites
*   [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
*   [Docker Desktop](https://www.docker.com/products/docker-desktop/)
*   An IDE (Visual Studio, Rider, VS Code)

### 1. Clone & Setup Environment
```bash
cp .env.example docker/.env
```

### 2. Start Infrastructure
```bash
docker-compose -f docker/docker-compose.yml up -d
```

### 3. Run the Microservices
You can run the solution via your IDE using the `.slnx` file or start them individually:

```bash
# Gateway (Entry Point)
dotnet run --project src/Bff.Gateway

# Internal Services
dotnet run --project src/ShoppingCartService
dotnet run --project src/DiscountService
dotnet run --project src/NotificationService
```

## 📡 API Endpoints (via Gateway)

### Shopping Cart (`/api/carts`)
| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/api/carts/{userId}` | Retrieve current state |
| `POST` | `/api/carts/{userId}/items` | Add item (Idempotent) |
| `PUT` | `/api/carts/{userId}/items/{id}`| Update quantity |
| `DELETE` | `/api/carts/{userId}/items/{id}`| Remove item |
| `POST` | `/api/carts/{userId}/confirm` | Confirm (Triggers RabbitMQ Event) |

### Discounts (`/api/discounts`)
| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/api/discounts/rules` | List all rules |
| `POST` | `/api/discounts/calculate` | Calculate discount for cart |

## 📊 Observability
Each service is equipped with:
- **Serilog:** Structured logging to Console/File.
- **OpenTelemetry:** Distributed tracing and metrics.
- **Health Checks:** `/health` endpoint for monitoring status.
- **Prometheus:** `/metrics` endpoint for scraping stats.

## 🤝 Contributing
Feel free to open issues or submit Pull Requests for improvements.

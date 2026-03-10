# Microservice RabbitMQ Stream Project

A robust, event-driven microservices architecture built with **.NET 9**, demonstrating advanced patterns such as **CQRS**, **Event Sourcing** with **Redis**, and high-performance messaging using **RabbitMQ Super Streams**.

## 🚀 Key Features

*   **.NET 9 & Minimal APIs:** Building lightning-fast HTTP endpoints using the latest ASP.NET Core framework.
*   **Event Sourcing (Redis):** Instead of saving just the final state, every change in the Shopping Cart is saved as an event in a Redis Sorted Set. This allows for perfect audit trails, debugging, and state reconstruction.
*   **CQRS (Command Query Responsibility Segregation):** Strict separation between read (Query) and write (Command) operations for scale and clarity.
*   **RabbitMQ Super Streams:** Kafka-like partition-based messaging for massive throughput, parallel processing, and guaranteed ordering for integration events.
*   **Idempotency & Rate Limiting:** Built-in safeguards against duplicate requests (network retries) and API abuse.
*   **Clean Architecture:** Domain-Driven Design (DDD) principles with clear boundaries between API, Application, Domain, and Infrastructure layers.

## 🏗️ Architecture & Services

The heavily decoupled architecture consists of three primary microservices:

### 1. 🛒 ShoppingCartService (Core API)
*   **Tech:** ASP.NET Core Minimal API, Redis Event Store
*   **Role:** Manages the lifecycle of user shopping carts via CQRS and Event Sourcing.
*   **Action:** When a cart is successfully checked out/confirmed, it publishes a rich `CartConfirmedIntegrationEvent` to the RabbitMQ Super Stream.

### 2. 💸 DiscountService (Consumer)
*   **Tech:** ASP.NET Core Background Service, EF Core 9, SQL Server
*   **Role:** Listens to cart confirmation events. Manages discount rules and coupon codes.

### 3. 🔔 NotificationService (Consumer)
*   **Tech:** .NET Worker Service, In-Memory Cache (Idempotency)
*   **Role:** Listens to the same stream to send out necessary notifications (Email, SMS, Push) ensuring users are notified exactly once per action.

---

> For an in-depth dive into the technical implementation, patterns (Lua scripts), and data flows, please read the [Architecture Documentation](ARCHITECTURE.md).

---

## 🛠️ Infrastructure Stack (Docker Compose)

The entire environment runs seamlessly via Docker Compose, provisioning all necessary databases and brokers out-of-the-box.

| Service | Image | Ports | Purpose |
| :--- | :--- | :--- | :--- |
| **RabbitMQ** | `rabbitmq:3-management` | `5672`, `15672` (UI), **`5552` (Stream)** | Message Broker (Super Streams Enabled) |
| **Redis Master** | `redis:7-alpine` | `6379` | Event Store Primary Node |
| **Redis Slave** | `redis:7-alpine` | `6380` | High-Availability Read Replica |
| **Redis Sentinel**| `redis:7-alpine` | `26379` | Monitor & Automatic Failover |
| **SQL Server** | `mssql/server:2022` | `1433` | Persistence for DiscountService |
| **Azurite** | `azure-storage/azurite` | `10000-10002` | Local Azure Storage Emulator (Prepared) |

## ⚙️ Getting Started

### Prerequisites
*   [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
*   [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or equivalent container runtime)
*   An IDE (Visual Studio, Rider, VS Code)

### 1. Clone & Setup Environment
Ensure your configurations are set. You may need to copy `.env.example` to `docker/.env` and update the passwords.

```bash
cp .env.example docker/.env
```

### 2. Start Infrastructure
Navigate to the root directory and spin up the Docker containers in detached mode:

```bash
docker-compose -f docker/docker-compose.yml up -d
```

### 3. Run the Microservices
You can run the solution via your IDE using the `.sln` file or start them individually via the .NET CLI:

```bash
# Terminal 1: ShoppingCartService
dotnet run --project src/ShoppingCartService

# Terminal 2: DiscountService
dotnet run --project src/DiscountService

# Terminal 3: NotificationService
dotnet run --project src/NotificationService
```

## 📡 API Endpoints (ShoppingCartService)

The main interaction point is the `ShoppingCartService` API (`/api/carts`).

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/api/carts/{userId}` | Retrieve the current state of a user's cart |
| `POST` | `/api/carts/{userId}/items` | Add a new item to the cart (Idempotent) |
| `PUT` | `/api/carts/{userId}/items/{productId}` | Update the quantity of a specific item |
| `DELETE` | `/api/carts/{userId}/items/{productId}`| Remove an item from the cart |
| `POST` | `/api/carts/{userId}/confirm` | Confirm checkout (Triggers RabbitMQ Event) |
| `POST` | `/api/carts/{userId}/clear` | Empty the cart |

*Note: Mutating endpoints (`POST`, `PUT`, `DELETE`) require the `X-Idempotency-Key` header (e.g., a simple GUID) to prevent duplicate processing during network retries.*

## 🔒 Configuration

Crucial settings are located in `appsettings.json` of each project.
Override secrets in an `appsettings.Development.json` (git-ignored) or via environment variables.

```json
// Example: ShoppingCartService Event Store Rules
"EventStore": {
  "ActiveCartTtlDays": 30,
  "ConfirmedCartTtlDays": 7
}
```

## 📚 Project Structure Highlights

```text
Microservice_RabbitMQ_Stream_Project/
├── docker/
│   └── docker-compose.yml       # Complete local infrastructure
├── src/
│   ├── ShoppingCartService/     # CQRS, Redis Event Store, Minimal API
│   ├── DiscountService/         # Background Consumer, EF Core, SQL Server
│   └── NotificationService/     # Worker Service Consumer
├── ARCHITECTURE.md              # Deep structural documentation
└── README.md                    # You are here
```

## 🤝 Contributing
Feel free to open issues or submit Pull Requests for improvements regarding standard DDD patterns, stream processing optimizations, or overall code health.

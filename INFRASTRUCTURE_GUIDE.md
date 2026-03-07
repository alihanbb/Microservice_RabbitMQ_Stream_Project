# Infrastructure Configuration Guide

## Overview

This document describes the infrastructure services configured in `docker-compose.yml` and their usage in the microservices project.

## Infrastructure Services

### 1. RabbitMQ Stream Broker
- **Container**: rabbitmq:3-management
- **Purpose**: Event streaming and message broker for inter-service communication
- **Ports**:
  - 5672: AMQP protocol
  - 5552: RabbitMQ Stream protocol (used by ShoppingCartService)
  - 15672: Management UI (http://localhost:15672)
- **Configuration**: Stream plugin enabled via docker-compose command
- **Usage**: ShoppingCartService publishes cart events via `RabbitMQStreamPublisher`

### 2. SQL Server (Primary + Backup)
- **Containers**: 
  - sqlserver (primary) on port 1433
  - sqlserver-backup (replica) on port 1434
- **Purpose**: Primary transactional database for DiscountService and other services
- **Backup Location**: `/var/opt/mssql/backup` volume

#### Usage in Services:
- **DiscountService**: Stores discount rules and coupon codes using Entity Framework Core
- **BackupServices**: Backs up SQL Server databases to PostgreSQL for disaster recovery

### 3. PostgreSQL (Primary + Backup)
- **Containers**:
  - postgresql (primary) on port 5432
  - postgresql-backup (replica) on port 5433
- **Purpose**: Backup destination and archive storage for SQL Server data
- **Backup Storage**: `/var/lib/postgresql/backup` volume

#### Usage in Services:
- **BackupServices**: Primary backup destination for SQL Server database backups
  - Receives full database backups from SQL Server
  - Stores backup metadata in `backup_metadata` table
  - Provides database table archives

#### Configuration:
```yaml
postgresql:
  environment:
    POSTGRES_DB: maindb
    POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-PostgresPass123}
  
postgresql-backup:
  environment:
    POSTGRES_DB: backupdb
    POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-PostgresPass123}
```

#### Connection String:
```
Server=postgresql;Port=5432;User Id=postgres;Password=PostgresPass123;Database=maindb;
```

#### Backup Connection String:
```
Server=postgresql-backup;Port=5432;User Id=postgres;Password=PostgresPass123;Database=backupdb;
```

### 4. Redis (Master + Slave with Sentinel)
- **Containers**:
  - redis-master (primary) on port 6379
  - redis-slave (replica) on port 6380
  - redis-sentinel (monitoring) on port 26379
- **Purpose**: Distributed caching and session storage with high availability

#### Usage in Services:
- **ShoppingCartService**:
  - Caches cart data for performance optimization
  - Event sourcing event store
  - Distributed caching for GET requests

#### Redis Configuration:
```yaml
redis-master:
  command: redis-server --appendonly yes --requirepass ${REDIS_PASSWORD:-RedisPass123}
  
redis-slave:
  command: redis-server --appendonly yes --requirepass ${REDIS_PASSWORD:-RedisPass123} 
                        --masterauth ${REDIS_PASSWORD:-RedisPass123} 
                        --replicaof redis-master 6379
```

#### Connection String:
```
localhost:6379,password=RedisPass123
```

### 5. Redis Sentinel Configuration

Redis Sentinel is configured for automatic failover and monitoring.

#### Sentinel Configuration:
```
sentinel monitor mymaster redis-master 6379 1
sentinel auth-pass mymaster RedisPass123
sentinel down-after-milliseconds mymaster 5000
sentinel failover-timeout mymaster 60000
sentinel parallel-syncs mymaster 1
```

#### Sentinel Features:
1. **Monitoring**: Continuously monitors master-slave health
2. **Failover**: Automatically promotes slave to master if master fails
3. **High Availability**: Ensures Redis availability even during node failures
4. **Configuration Propagation**: Notifies clients of configuration changes

#### How to Use Sentinel in Your Application:

**Option 1: Manual Sentinel Connection (Recommended for .NET)**

```csharp
// Manual connection to sentinel endpoint to discover master
var sentinelEndpoint = new[] { "sentinel-host:26379" };
var masterName = "mymaster";

// Query sentinel to get current master
var sentinelConnection = ConnectionMultiplexer.Connect(
    new ConfigurationOptions
    {
        EndPoints = { sentinelEndpoint[0] },
        TieBreaker = ""
    }
);

// Get master endpoint from sentinel
var masterEndpoint = sentinelConnection.GetServer(sentinelEndpoint[0])
    .Execute("SENTINEL", "masters")[0];
```

**Option 2: Using StackExchange.Redis with Sentinel Support**

```csharp
// Enable Sentinel support in connection string
var options = ConfigurationOptions.Parse(
    "mymaster,sentinelPort=26379,sentinel=redis-sentinel:26379"
);
var connection = await ConnectionMultiplexer.ConnectAsync(options);
```

#### Implementing Failover Handling:

```csharp
// Implement retry logic with exponential backoff
var retryPolicy = Policy
    .Handle<RedisConnectionException>()
    .Or<TimeoutException>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        onRetry: (outcome, timespan, retryCount, context) =>
        {
            _logger.LogWarning($"Redis connection failed, attempt {retryCount}");
        }
    );

// Use policy in cache operations
await retryPolicy.ExecuteAsync(async () =>
{
    return await _redisConnection.GetDatabase().StringGetAsync(cacheKey);
});
```

#### Monitoring Sentinel Health:

```bash
# Monitor sentinel health from container
docker exec redis-sentinel redis-cli -p 26379 SENTINEL MASTERS

# Check slave status
docker exec redis-sentinel redis-cli -p 26379 SENTINEL SLAVES mymaster

# Get current master
docker exec redis-sentinel redis-cli -p 26379 SENTINEL GET-MASTER-ADDR-BY-NAME mymaster
```

### 6. Azure Storage Emulator (Azurite)
- **Container**: mcr.microsoft.com/azure-storage/azurite
- **Purpose**: Local Azure Storage emulation for development
- **Ports**:
  - 10000: Blob service
  - 10001: Queue service
  - 10002: Table service
- **Usage**: NotificationService can use Azure Storage for queues and blobs in development

## Service Communication Flow

```
┌──────────────────────────────────────────────────────────────────┐
│                     ShoppingCartService                          │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │ Endpoints                                                     │ │
│  └────────────┬────────────────────────────────────────────────┘ │
│               │                                                    │
│      ┌────────▼────────┐                                          │
│      │ Redis Cache     │◄─────── Distributed Caching             │
│      │ (6379/6380)     │         Event Sourcing                  │
│      └────────────────┘                                          │
│               │                                                    │
│      ┌────────▼─────────────┐                                    │
│      │ RabbitMQ Stream      │◄─────── Event Publishing           │
│      │ (5552)               │         (CartEvents)               │
│      └─────────────────────┘                                     │
│               │                                                    │
└───────────────┼────────────────────────────────────────────────────┘
                │
        ┌───────▼────────────┐
        │ NotificationService│
        │ (Event Consumer)   │
        └────────────────────┘
                │
        ┌───────▼──────────────┐
        │ Azurite Queue        │
        │ (Development Only)   │
        └──────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│                     DiscountService                              │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │ API Controllers                                               │ │
│  └────────────┬────────────────────────────────────────────────┘ │
│               │                                                    │
│      ┌────────▼──────────────┐                                   │
│      │ SQL Server (1433)     │◄─────── Entity Framework Core      │
│      │ Discount Rules        │         ORM                        │
│      │ Coupon Codes          │                                    │
│      └───────────────────────┘                                   │
└──────────────────────────────────────────────────────────────────┘
                │
        ┌───────▼─────────────┐
        │ BackupServices      │
        │ (Scheduled Backups) │
        └────────┬────────────┘
                │
        ┌───────▼──────────────────┐
        │ PostgreSQL (5432/5433)   │
        │ Backup Archive           │
        └──────────────────────────┘
```

## Environment Configuration

### Required Environment Variables

```bash
# SQL Server credentials
SA_PASSWORD=YourStrong@Passw0rd

# Redis password
REDIS_PASSWORD=RedisPass123

# PostgreSQL password
POSTGRES_PASSWORD=PostgresPass123
```

### Service Connection Strings

#### ShoppingCartService
```
Redis: localhost:6379,password=RedisPass123
RabbitMQ:
  Host: localhost
  Port: 5552
  Username: guest
  Password: guest
  StreamName: shopping-cart-events
```

#### DiscountService
```
SqlConnection: Server=sqlserver;Database=DiscountDB;User Id=sa;Password=YourStrong@Passw0rd;
```

#### BackupServices
```
SqlServer: Server=sqlserver,1433;User Id=sa;Password=YourStrong@Passw0rd;
PostgreSQL: Server=postgresql-backup;User Id=postgres;Password=PostgresPass123;Database=backupdb;
```

## Running the Infrastructure

### Start All Services
```bash
docker-compose up -d
```

### Health Checks
```bash
# Check RabbitMQ
curl -u guest:guest http://localhost:15672/api/overview

# Check SQL Server
docker exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P YourStrong@Passw0rd -C -Q "SELECT 1"

# Check PostgreSQL
docker exec postgresql pg_isready -U postgres

# Check Redis
docker exec redis-master redis-cli -a RedisPass123 PING

# Check Sentinel
docker exec redis-sentinel redis-cli -p 26379 PING
```

### Monitoring

#### RabbitMQ Management UI
- URL: http://localhost:15672
- Username: guest
- Password: guest

#### SQL Server Management
```bash
# Connect via Docker exec
docker exec -it sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P YourStrong@Passw0rd
```

#### PostgreSQL Management
```bash
# Connect via psql
docker exec -it postgresql psql -U postgres

# Query backup metadata
SELECT * FROM backup_metadata ORDER BY created_at DESC LIMIT 10;
```

#### Redis Monitoring
```bash
# Connect to master
docker exec -it redis-master redis-cli -a RedisPass123

# Check replication status
INFO replication

# Monitor Sentinel
docker exec -it redis-sentinel redis-cli -p 26379 SENTINEL MASTERS
```

## Disaster Recovery Procedures

### 1. SQL Server to PostgreSQL Backup

The BackupServices function performs regular backups:

```csharp
var backupService = new DatabaseBackupService(logger, sqlConnStr, pgConnStr);
var result = await backupService.BackupSqlServerToDatabaseAsync("DiscountDB");

// result.Status: Success, PartialSuccess, or Failed
// result.TotalRows: Total rows backed up
// result.FailedTables: List of failed tables with error messages
```

### 2. Restore from PostgreSQL Backup

To restore data from PostgreSQL backup:

```sql
-- Connect to PostgreSQL
SELECT * FROM DiscountDB_CouponCodes; -- View backed-up data

-- Export to SQL Server or other format
COPY DiscountDB_CouponCodes TO STDOUT CSV;
```

### 3. Redis Failover

Sentinel automatically promotes slave to master when master fails:

```bash
# Monitor failover
docker exec redis-sentinel redis-cli -p 26379 SENTINEL SLAVES mymaster

# Manually trigger failover (for testing)
docker exec redis-sentinel redis-cli -p 26379 SENTINEL FAILOVER mymaster
```

### 4. SQL Server Replication

Enable SQL Server replication between primary and backup instances:

```sql
-- On primary instance
EXEC sp_replicamode 'DiscountDB', 'REPLICA'

-- Configure backup instance as subscriber
EXEC sp_add_subscription @publication = 'DiscountDB_Publication',
    @subscriber = 'sqlserver-backup',
    @subscriber_db = 'DiscountDB'
```

## Future Enhancements

### Recommended Improvements:

1. **PostgreSQL Integration**: Consider using PostgreSQL as primary database for some services (e.g., NotificationService) to reduce dependency on SQL Server

2. **Sentinel-Based Failover**: Implement StackExchange.Redis with Sentinel support for automatic master discovery and failover

3. **Replication**: Enable SQL Server replication between primary and backup instances for hot standby

4. **Monitoring Stack**: Add Prometheus + Grafana for comprehensive infrastructure monitoring

5. **Backup Automation**: Implement scheduled backup jobs using Azure Functions with timer triggers

6. **Data Consistency**: Implement distributed transactions using Saga pattern for cross-service consistency

## Troubleshooting

### Redis Connection Issues
```bash
# Check if Redis is accepting connections
docker exec redis-master redis-cli -a RedisPass123 PING

# Check port forwarding
netstat -an | grep 6379
```

### PostgreSQL Backup Failures
```bash
# Check PostgreSQL logs
docker logs postgresql

# Verify disk space
docker exec postgresql df -h

# Check table creation
docker exec -it postgresql psql -U postgres -c "SELECT * FROM information_schema.tables"
```

### SQL Server Connection Issues
```bash
# Check SQL Server connectivity
docker exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -Q "SELECT @@VERSION"

# Check SQL Server logs
docker logs sqlserver | tail -100
```

## Performance Optimization

### Redis Caching Strategy
- Cache cart data with 5-minute TTL
- Cache GET requests with 10-minute TTL
- Invalidate cache on POST/PUT/DELETE operations

### Database Indexes
```sql
-- Recommended indexes for SQL Server
CREATE INDEX idx_coupon_code ON CouponCodes(Code);
CREATE INDEX idx_discount_rule_active ON DiscountRules(IsActive, StartDate);
```

### Backup Optimization
- Run backups during off-peak hours
- Use incremental backups for large databases
- Archive old backups to separate storage
- Test restore procedures regularly

---

**Last Updated**: 2024
**Maintained By**: Microservices Team

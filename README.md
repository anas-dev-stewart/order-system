# Simple Order System

A microservices learning project built in two stages:

1. **Core assignment** (`docs/Assignment.md`) — two independent ASP.NET Core
   Web API services (Product Service, Order Service), each with its own SQL
   Server database, plus an Angular front end, containerized with Docker
   Compose.
2. **Advanced assignment** (`docs/team_assignment_advanced_dotnet10_order_processing.md`)
   — turns order placement into an **event-driven workflow**: MassTransit +
   **Azure Service Bus**, a separate Order Worker service, a simulated
   Pricing Service, and reliability patterns (retries, idempotency,
   transactional outbox).

New to .NET? Read [`HANDBOOK.md`](./HANDBOOK.md) first — it explains every
concept here (DI, async/await, EF Core, MassTransit, Docker...) with
direct comparisons to Spring Boot.

## Architecture

```
                         Angular App (:4200)
                          |            |
                          v            v
                  Product Service  Order Service (:5002)
                    (:5001)             |   ^
                       ^                |   |
                       |         1. save Order (Submitted)
                       |         2. SEND OrderSubmitted      --\
                       |            (transactional outbox)      |
                       |                                        v
                       |                            Azure Service Bus
                       |                           (order-submitted-queue)
                       |                                        |
                       |                        3. consume OrderSubmitted
                       |                                        v
                       |<----- 4. GET/reduce stock ----  Order Worker
                       |                                        |
                       |                        5. POST /api/pricing/quote
                       |                                        v
                       |                              Pricing Service (:5003)
                       |                                        |
                       |                        6. SEND OrderApproved
                       |                           or OrderRejected
                       |                                        v
                       |                            Azure Service Bus
                       |                     (order-approved/-rejected-queue)
                       |                                        |
                       |                        7. consume -> update Order
                       \---------------------------------------/
                            (Order Service owns OrderDb; Worker never
                             writes to it directly)

  ProductDb  <---  Product Service        OrderDb  <---  Order Service
         (SQL Server, two logical databases, one instance)
```

**Why the order flow is asynchronous:** `POST /api/orders` saves the order
as `Submitted` and returns immediately — it does NOT wait for stock
validation or pricing. A background worker does that work and the order
moves to `Approved` or `Rejected` a moment later. The client polls
`GET /api/orders/{id}` (or in a real product, gets pushed a notification)
to see the final result. This keeps the API fast and decoupled from how
slow or flaky the Pricing Service might be.

**Service boundaries** (each owns exactly one thing):

| Service | Owns | Talks to |
|---|---|---|
| Product Service | `ProductDb` | nothing else (pure data owner) |
| Order Service | `OrderDb`, sending `OrderSubmitted`, consuming `OrderApproved`/`OrderRejected` | Azure Service Bus only |
| Order Worker | nothing shared — its own local SQLite "inbox" file for dedupe | Product Service (HTTP), Pricing Service (HTTP), Azure Service Bus |
| Pricing Service | nothing (stateless) | nothing — simulated external dependency |

**Why Send, not Publish:** this project's Azure Service Bus namespace is
**Basic tier** (cheapest, no monthly-ish Standard/Premium cost) — Basic
tier supports queues only, not topics/subscriptions, so MassTransit's
usual `Publish` fan-out (which needs a topic) isn't available. Every event
here has exactly one consumer anyway, so each one is `Send` directly to a
named queue (`order-submitted-queue`, `order-approved-queue`,
`order-rejected-queue`) — plain point-to-point messaging. See
[`HANDBOOK.md`](./HANDBOOK.md#8-masstransit-vs-spring-amqp--spring-cloud-stream)
for the Send-vs-Publish reasoning in depth.

The Worker **never touches OrderDb or ProductDb directly** — it only calls
Product Service's HTTP API and publishes events. This is deliberate: only
one service writes to each database, which is what keeps microservices
independently deployable. See [`HANDBOOK.md`](./HANDBOOK.md#why-doesnt-the-worker-just-update-orderdb-directly)
for the full reasoning.

## Prerequisites

- .NET SDK (10.x) — `dotnet --version`
- Node.js 20+ and npm — `node --version`
- Docker Desktop (the easiest way to run everything)
- An Azure Service Bus namespace (Basic tier is enough) — see
  [Setting up Azure Service Bus](#setting-up-azure-service-bus) below if
  you don't have one yet
- A local SQL Server instance if you want to run the APIs with `dotnet run`
  instead of Docker — `sqlcmd -S localhost -E -Q "SELECT 1"`
- The `dotnet-ef` global tool: `dotnet tool install --global dotnet-ef`

## Setting up Azure Service Bus

1. Create a namespace: Azure Portal → Create a resource → **Service Bus**
   → Basic tier is fine (queues only, no topics — this project only uses
   Send/Consume, never Publish, so that's not a limitation here; see
   [HANDBOOK.md §8](./HANDBOOK.md#8-masstransit-vs-spring-amqp--spring-cloud-stream)).
   Or via CLI:
   ```bash
   az servicebus namespace create --resource-group <rg> --name <namespace> --sku Basic --location <region>
   ```
2. Create the three queues MassTransit needs (it would also auto-create
   them on first run, since `RootManageSharedAccessKey` has Manage rights
   — this just makes it explicit):
   ```bash
   az servicebus queue create --resource-group <rg> --namespace-name <namespace> --name order-submitted-queue
   az servicebus queue create --resource-group <rg> --namespace-name <namespace> --name order-approved-queue
   az servicebus queue create --resource-group <rg> --namespace-name <namespace> --name order-rejected-queue
   ```
3. Grab the connection string: namespace → **Shared access policies** →
   `RootManageSharedAccessKey` → copy the **Primary Connection String**.
   Or via CLI:
   ```bash
   az servicebus namespace authorization-rule keys list \
     --resource-group <rg> --namespace-name <namespace> \
     --name RootManageSharedAccessKey --query primaryConnectionString -o tsv
   ```
4. Create a `.env` file next to `docker-compose.yml` (gitignored — never
   commit this):
   ```
   SERVICEBUS_CONNECTION_STRING=Endpoint=sb://<namespace>.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=...
   ```
   For running the APIs directly with `dotnet run` instead of Docker, set
   the same value as the `ServiceBus:ConnectionString` config key (e.g.
   via `dotnet user-secrets` or the `ServiceBus__ConnectionString`
   environment variable) in both `OrderService.Api` and `OrderService.Worker`.

## Quick start: everything in Docker Compose (recommended)

This is the easiest way to see the whole event-driven flow working — no
local SQL Server, .NET SDK, or Node install needed. You do still need an
Azure Service Bus connection string in a `.env` file (see
[Setting up Azure Service Bus](#setting-up-azure-service-bus) above).

> **On the Stewart corporate network**: `docker-ca-certs/` contains the
> corporate TLS-inspection root CAs (Netskope + Stewart root CAs — public
> certificates only, no private keys). Each Dockerfile installs them into
> the container's trust store before `dotnet restore` / `npm ci`. If
> you're building this from a different network, these certs are harmless
> no-ops and can be left as-is.

```bash
docker compose up --build
```

This builds and starts six containers (the broker itself is Azure Service
Bus, not a container):

| Container | URL | Role |
|---|---|---|
| `sqlserver` | localhost:1433 | hosts `ProductDb` and `OrderDb` |
| `product-service` | http://localhost:5001 | Product Service API |
| `order-service` | http://localhost:5002 | Order Service API |
| `pricing-service` | http://localhost:5003 | simulated Pricing API |
| `order-worker` | *(no HTTP port — background consumer)* | processes orders |
| `web-app` | http://localhost:4200 | Angular front end |

Both API containers run EF Core migrations automatically on startup — no
manual `dotnet ef database update` step required in Docker.

```bash
docker compose up --build      # build images and start everything, logs attached
docker compose up -d --build   # same, but detached (runs in the background)
docker compose down            # stop and remove containers (keeps DB/worker volumes)
docker compose down -v         # also wipes SQL Server data and the worker's inbox
docker compose logs -f order-worker        # watch an order get processed live
docker compose up -d --scale order-worker=3   # run 3 worker instances; Service Bus
                                               # spreads OrderSubmitted across them
```

The default SA password is `Your_password123!` (set in `docker-compose.yml`).
Override it with a `.env` file: `SA_PASSWORD=<your-password>`.

### Try it

```bash
# 1. Create a product
curl -X POST http://localhost:5001/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Widget","price":50,"stockQuantity":20}'

# 2. Place an order — returns immediately with status "Submitted" (0)
curl -X POST http://localhost:5002/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerName":"Ali","productId":1,"quantity":2}'

# 3. Check it a couple of seconds later — status is now "Approved" (1),
#    with a UnitPrice/TotalPrice filled in by the worker + Pricing Service
curl http://localhost:5002/api/orders/1

# 4. Try ordering more than the remaining stock — it comes back "Rejected" (2)
curl -X POST http://localhost:5002/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerName":"Bob","productId":1,"quantity":99999}'

# 5. Retry the SAME logical request safely with an idempotency key —
#    the second call returns the first order instead of creating a duplicate
curl -X POST http://localhost:5002/api/orders \
  -H "Content-Type: application/json" -H "Idempotency-Key: demo-1" \
  -d '{"customerName":"Carl","productId":1,"quantity":1}'
curl -X POST http://localhost:5002/api/orders \
  -H "Content-Type: application/json" -H "Idempotency-Key: demo-1" \
  -d '{"customerName":"Carl","productId":1,"quantity":1}'
```

Watch it happen live: `docker compose logs -f order-worker` while you run
step 2, or open your Service Bus namespace in the Azure Portal →
**Queues** to see message counts update on `order-submitted-queue` /
`order-approved-queue` / `order-rejected-queue` in real time.

## Running locally without Docker (for active development)

With a local SQL Server running and `.env`'s connection string also set as
`ServiceBus:ConnectionString` (user-secrets or an environment variable)
for `OrderService.Api` and `OrderService.Worker`:

### 1. Set up the databases

```bash
cd product-service/ProductService.Api
dotnet restore
dotnet ef database update

cd ../../order-service/OrderService.Api
dotnet restore
dotnet ef database update
```

If you change a model, add a new migration before updating the database:
`dotnet ef migrations add <MigrationName>` then `dotnet ef database update`.

### 2. Run every service (five terminals)

```bash
cd product-service/ProductService.Api   && dotnet run   # :5001
cd pricing-service/PricingService.Api   && dotnet run   # :5003
cd order-service/OrderService.Api       && dotnet run   # :5002
cd order-service/OrderService.Worker    && dotnet run   # consumer, no HTTP port
cd web-app && npm install && npm start                  # :4200
```

Start Product Service and Pricing Service before the Worker — Order
Service and the Worker both connect to Azure Service Bus independently on
startup, no local broker needed.

## Run the tests

```bash
cd product-service && dotnet test
cd order-service && dotnet test OrderService.Tests
cd order-service && dotnet test OrderService.Worker.Tests
```

Covers, among others:
- Order creation saves `Submitted` and sends `OrderSubmitted`
  (`OrderProcessingServiceTests`)
- An `Idempotency-Key` retry returns the original order instead of a
  duplicate (`OrderProcessingServiceTests`)
- `OrderApproved`/`OrderRejected` events update the order's status, and a
  **duplicate** `OrderApproved` is ignored rather than overwriting a
  settled price (`OrderStatusConsumersTests`)
- The worker approves/rejects based on stock and pricing, skips a stock
  check entirely for a product that doesn't exist (`OrderSubmittedConsumerTests`)
- A **redelivered** `OrderSubmitted` message is detected and skipped
  before it reduces stock a second time (`OrderSubmittedConsumerTests`)
- A transient Product Service failure faults the message (so MassTransit
  retries) instead of being treated as a permanent rejection
  (`OrderSubmittedConsumerTests`)

## API testing with Postman

Import `postman/order-system.postman_collection.json`. Collection
variables `productBaseUrl` / `orderBaseUrl` / `pricingBaseUrl` default to
`:5001` / `:5002` / `:5003`.

## Reliability patterns used here

| Requirement | Where | How |
|---|---|---|
| Retries on failure | `order-service/OrderService.Api/Program.cs`, `OrderService.Worker/Program.cs` | `UseMessageRetry` — exponential backoff, 5 attempts, before a message is dead-lettered |
| Duplicate message ≠ duplicate work | `OrderService.Worker/Consumers/OrderSubmittedConsumer.cs` | checks a local "processed messages" table (the **inbox** pattern) before reducing stock |
| Duplicate client request ≠ duplicate order | `OrderService.Api/Controllers/OrdersController.cs` + `OrderProcessingService.cs` | `Idempotency-Key` header, unique DB index |
| Order saved ⟺ event published, atomically | `OrderService.Api/Services/OrderProcessingService.cs`, `OrderDbContext.cs` | MassTransit's EF Core **transactional outbox** |
| Concurrency limits | `OrderService.Worker/Program.cs`, `Clients/ConcurrencyLimitingHandler.cs` | `ConcurrentMessageLimit` on the Service Bus receive endpoint; a semaphore-based `DelegatingHandler` on outbound HTTP calls |
| HTTP timeouts + circuit breaker | `OrderService.Worker/Program.cs` | `AddStandardResilienceHandler()` on the Pricing Service client |
| Connection pooling | everywhere HTTP is called | `IHttpClientFactory` typed clients — never `new HttpClient()` |

`HANDBOOK.md` explains **why** each of these exists and what breaks without it.

## Project layout

```
/order-system
  docker-compose.yml
  HANDBOOK.md                       # start here if you're new to .NET
  /product-service
    ProductService.Api              # Controllers, Services, Data, Models, Dtos, Dockerfile
    ProductService.Tests
  /order-service
    OrderSystem.Contracts           # shared event contracts (OrderSubmitted/Approved/Rejected)
    OrderService.Api                # Controllers, Services, Data, Consumers (status updates), Dockerfile
    OrderService.Worker             # consumes OrderSubmitted, calls Product+Pricing, Dockerfile
    OrderService.Tests              # Order API + its consumers
    OrderService.Worker.Tests       # Worker's consumer
  /pricing-service
    PricingService.Api              # simulated external dependency, Dockerfile
  /web-app                          # Angular app (standalone components), Dockerfile + nginx.conf
  /postman                          # Exported Postman collection
  README.md
```

## Demo script (~5 minutes)

1. Open the Products page, add a new product.
2. Open the Orders page, place an order against that product. It shows as
   "Submitted".
3. Refresh a moment later — it's moved to "Approved", with a price filled
   in by the Pricing Service.
4. Place an order with a quantity larger than the remaining stock — it
   moves to "Rejected" instead, with a reason.
5. `docker compose logs -f order-worker` while placing an order, to watch
   the stock check and pricing call happen in real time.
6. Open the Service Bus namespace in the Azure Portal → **Queues** to see
   `order-submitted-queue` take a message and drain it in real time.

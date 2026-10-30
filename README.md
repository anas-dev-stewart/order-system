# Simple Order System

A small microservices learning project: two independent ASP.NET Core Web API
services (Product Service, Order Service), each with its own SQL Server
database, plus an Angular front end. Built from `Assignment.md`.

## Architecture

```
 Angular App (http://localhost:4200)
     |                  |
     v                  v
 Product Service    Order Service  ---- HTTP ---->  Product Service
 (localhost:5001)   (localhost:5002)
     |                  |
     v                  v
  ProductDb          OrderDb          (local MS SQL Server)
```

- Product Service owns `ProductDb`. Order Service owns `OrderDb`. Neither
  service has a connection string to the other's database.
- Order Service only knows about Product Service through
  `ProductService:BaseUrl` in its `appsettings.json` — it calls it over HTTP
  via a typed `HttpClient` (`IProductServiceClient` / `ProductServiceClient`).
- Each service can be started independently with `dotnet run`.

## Prerequisites

- .NET SDK (10.x) — `dotnet --version`
- Node.js 20+ and npm — `node --version`
- A local SQL Server instance reachable as `localhost` with Windows
  authentication (Developer/Express edition is fine) — `sqlcmd -S localhost -E -Q "SELECT 1"`
- The `dotnet-ef` global tool: `dotnet tool install --global dotnet-ef`
  (skip if `dotnet ef --version` already works)

## 1. Set up the databases

Each service manages its own database via EF Core migrations — no manual
SQL needed. From a clean checkout, restore packages before running any
`dotnet ef` command (the EF CLI tool does not restore automatically):

```bash
cd product-service/ProductService.Api
dotnet restore
dotnet ef database update

cd ../../order-service/OrderService.Api
dotnet restore
dotnet ef database update
```

This creates `ProductDb` and `OrderDb` on your local SQL Server instance
(connection strings are in each project's `appsettings.json`, pointing at
`Server=localhost` with a trusted/Windows-auth connection).

If you ever change a model, add a new migration before updating the
database:

```bash
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

## 2. Run the backend services

Open two terminals.

**Terminal 1 — Product Service (http://localhost:5001):**

```bash
cd product-service/ProductService.Api
dotnet run
```

**Terminal 2 — Order Service (http://localhost:5002):**

```bash
cd order-service/OrderService.Api
dotnet run
```

Order Service calls Product Service at the URL configured under
`ProductService:BaseUrl` in `order-service/OrderService.Api/appsettings.json`
(defaults to `http://localhost:5001`). Start Product Service first, or
Order Service will return `503 Service Unavailable` on order placement
until Product Service is reachable.

## 3. Run the Angular app

```bash
cd web-app
npm install   # first time only
npm start     # same as: ng serve
```

Open http://localhost:4200. The app talks to both backend services directly
from the browser (CORS is enabled on both APIs for `http://localhost:4200`).

- **Products** page: list, add, edit, delete products; search by name;
  sort by price (click the Price header); paginated 5 rows at a time.
- **Orders** page: place an order against a product and see the order list
  update, with the product's stock reduced on the Products page.

## 4. Run the tests

```bash
cd product-service
dotnet test

cd ../order-service
dotnet test
```

Covers (among others):
- Creating a product (`ProductCatalogServiceTests.CreateAsync_...`)
- Order rejected when stock is insufficient
  (`OrderProcessingServiceTests.PlaceOrderAsync_ThrowsInsufficientStock_...`)
- Stock reduced when an order is placed
  (`OrderProcessingServiceTests.PlaceOrderAsync_ReducesStockAndSavesOrder_...`)

## 5. API testing with Postman

Import `postman/order-system.postman_collection.json` into Postman. It
covers every endpoint of both services, including the validation/error
cases (400, 404, 409, 503). Collection variables `productBaseUrl` and
`orderBaseUrl` default to `http://localhost:5001` / `http://localhost:5002`.

## 6. Run everything with Docker Compose

No local SQL Server, .NET SDK, or Node install needed for this path — only
Docker Desktop (with the Docker CLI and Docker Compose, which it bundles).

> **On the Stewart corporate network**: `docker-ca-certs/` contains the
> corporate TLS-inspection root CAs (Netskope + Stewart root CAs — public
> certificates only, no private keys). Each Dockerfile installs them into
> the container's trust store before `dotnet restore` / `npm ci`, otherwise
> those commands fail with `UntrustedRoot` / "TLS: server certificate not
> trusted" because the container doesn't know about the corporate proxy
> that intercepts outbound HTTPS. If you're building this from a different
> network, these certs are harmless no-ops and can be left as-is.

From the repo root:

```bash
docker compose up --build
```

This builds and starts four containers:

- `sqlserver` — SQL Server 2022 (Linux container), with a named volume
  (`mssql-data`) so data survives a restart. Both `ProductDb` and `OrderDb`
  live on this one instance, matching the spec's "two databases on one
  local SQL Server" (section 4) — the services still never cross into
  each other's database.
- `product-service` — built from `product-service/ProductService.Api/Dockerfile`,
  published and run on `:8080` inside the container, mapped to
  **http://localhost:5001**.
- `order-service` — built from `order-service/OrderService.Api/Dockerfile`,
  mapped to **http://localhost:5002**. Reaches Product Service over the
  Docker network at `http://product-service:8080` (set via the
  `ProductService__BaseUrl` environment variable in `docker-compose.yml`
  — no code change, same config-driven pattern as `appsettings.json`).
- `web-app` — Angular app built and served by nginx, mapped to
  **http://localhost:4200**.

Both API containers run EF Core migrations automatically on startup
(`Database.Migrate()`, with a short retry loop in case SQL Server reports
"healthy" a few seconds before it actually accepts connections) — no
manual `dotnet ef database update` step required in Docker.

Useful commands:

```bash
docker compose up --build      # build images and start everything, logs attached
docker compose up -d --build   # same, but detached (runs in the background)
docker compose down            # stop and remove containers (keeps the DB volume)
docker compose down -v         # also wipes the SQL Server data volume
docker compose logs -f order-service   # tail one service's logs
```

The default SA password is `Your_password123!` (set in `docker-compose.yml`).
Override it by creating a `.env` file next to `docker-compose.yml` with
`SA_PASSWORD=<your-password>` — Compose picks it up automatically.

## Project layout

```
/order-system
  docker-compose.yml         # Runs all 4 containers (SQL Server + both APIs + Angular)
  /product-service
    ProductService.Api      # Controllers, Services, Data (DbContext), Models, Dtos, Dockerfile
    ProductService.Tests    # xUnit tests
  /order-service
    OrderService.Api        # Controllers, Services, Data, Models, Dtos, Clients (Product HTTP client), Dockerfile
    OrderService.Tests      # xUnit tests
  /web-app                  # Angular app (standalone components), Dockerfile + nginx.conf
  /postman                  # Exported Postman collection
  README.md
```

## Demo script (~5 minutes)

1. Open the Products page, add a new product.
2. Open the Orders page, place an order against that product.
3. Go back to Products — stock has been reduced by the order quantity.
4. Place an order with a quantity larger than the remaining stock — see
   the readable error and confirm no order was created.

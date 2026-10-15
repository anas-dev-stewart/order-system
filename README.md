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
SQL needed. From a clean checkout:

```bash
cd product-service/ProductService.Api
dotnet ef database update

cd ../../order-service/OrderService.Api
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

## Project layout

```
/order-system
  /product-service
    ProductService.Api      # Controllers, Services, Data (DbContext), Models, Dtos
    ProductService.Tests    # xUnit tests
  /order-service
    OrderService.Api        # Controllers, Services, Data, Models, Dtos, Clients (Product HTTP client)
    OrderService.Tests      # xUnit tests
  /web-app                  # Angular app (standalone components)
  /postman                  # Exported Postman collection
  README.md
```

## Demo script (~5 minutes)

1. Open the Products page, add a new product.
2. Open the Orders page, place an order against that product.
3. Go back to Products — stock has been reduced by the order quantity.
4. Place an order with a quantity larger than the remaining stock — see
   the readable error and confirm no order was created.

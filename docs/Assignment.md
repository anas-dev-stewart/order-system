# Spec: Simple Order System (.NET Microservices + Angular)

| | |
|---|---|
| **Status** | Ready to build |
| **Time box** | 3 hours total |
| **Quality bar** | Working and demo-able. Does not need to be perfect. All acceptance criteria must pass. |
| **Purpose** | Learning project for a team moving from Java to .NET |

---

## 1. Overview

Build a small system made of two independent .NET Web API microservices (Product Service and Order Service) and one Angular web app. Each service owns its own database on the locally installed MS SQL Server. The services communicate only through HTTP APIs.

## 2. Goals

- Learn C#, ASP.NET Core Web API, EF Core and xUnit.
- Learn how microservices are separated (own code, own database, communicate over HTTP).
- Learn how an Angular app consumes backend APIs.

## 3. Scope

### In scope
- Product Service (CRUD products, stock reduction)
- Order Service (place and view orders, calls Product Service)
- Angular app with a Products page and an Orders page
- A few unit tests (xUnit)
- Postman collection for API testing
- README with run instructions

### Out of scope
- Authentication and authorization
- API gateway, message brokers, Docker, cloud deployment
- Multi-product orders, payments, customers table
- Caching, advanced logging, server-side pagination/sorting/search (the frontend versions are bonus items, see section 14)

## 4. Technical Constraints

| Area | Requirement |
|---|---|
| Backend | Latest LTS version of modern .NET (not legacy .NET Framework), ASP.NET Core Web API, C# |
| Data access | Entity Framework Core with migrations |
| Database | Local MS SQL Server. Two databases: `ProductDb` and `OrderDb` |
| Service communication | `HttpClient` (typed client or `IHttpClientFactory`) |
| Frontend | Current stable Angular, standalone components, Angular `HttpClient` |
| Tests | xUnit (plus Moq or NSubstitute if needed) |
| API testing | Postman (or Insomnia / Bruno) |
| Config | `appsettings.json` for connection strings and the Product Service base URL |
| Source control | Git, commit regularly |

## 5. Architecture

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

Rules:
- A service must never connect to the other service's database.
- The Order Service only knows the Product Service through its base URL in configuration.
- Each service is its own solution/project and can be started independently.

Suggested folder structure:

```
/order-system
  /product-service
    ProductService.Api
    ProductService.Tests
  /order-service
    OrderService.Api
    OrderService.Tests
  /web-app            (Angular)
  /postman            (exported collection)
  README.md
```

Inside each API project: `Controllers`, `Services` (business logic), `Data` (DbContext), `Models`, `Dtos`.

## 6. Data Model

### Product (ProductDb)

| Field | Type | Rules |
|---|---|---|
| Id | int | Primary key, auto-generated |
| Name | string | Required, max 100 characters |
| Price | decimal(18,2) | Required, greater than 0 |
| StockQuantity | int | Required, 0 or more |

### Order (OrderDb)

| Field | Type | Rules |
|---|---|---|
| Id | int | Primary key, auto-generated |
| CustomerName | string | Required, max 100 characters |
| ProductId | int | Required, must exist in Product Service |
| Quantity | int | Required, greater than 0 |
| UnitPrice | decimal(18,2) | Copied from product at order time |
| TotalPrice | decimal(18,2) | UnitPrice x Quantity |
| CreatedAtUtc | datetime | Set by the server |

## 7. API Contracts

### Product Service (`http://localhost:5001`)

| Method | Route | Description | Success | Errors |
|---|---|---|---|---|
| GET | `/api/products` | List all products | 200 | |
| GET | `/api/products/{id}` | Get one product | 200 | 404 |
| POST | `/api/products` | Create product | 201 | 400 |
| PUT | `/api/products/{id}` | Update product | 200 | 400, 404 |
| DELETE | `/api/products/{id}` | Delete product | 204 | 404 |
| POST | `/api/products/{id}/reduce-stock` | Body: `{ "quantity": 2 }`. Reduces stock | 200 | 400, 404, 409 (insufficient stock) |

### Order Service (`http://localhost:5002`)

| Method | Route | Description | Success | Errors |
|---|---|---|---|---|
| POST | `/api/orders` | Body: `{ "customerName": "Ali", "productId": 1, "quantity": 2 }` | 201 | 400, 404 (product not found), 409 (insufficient stock), 503 (Product Service unavailable) |
| GET | `/api/orders` | List all orders | 200 | |
| GET | `/api/orders/{id}` | Get one order | 200 | 404 |

Errors return JSON with a clear `message` field.

## 8. Functional Requirements

### Product Service
- **FR-P1** The system shall allow creating a product with a name, price and stock quantity.
- **FR-P2** The system shall return the list of all products and a single product by id.
- **FR-P3** The system shall allow updating and deleting a product.
- **FR-P4** The system shall validate product input (see data model rules).
- **FR-P5** The system shall provide an endpoint to reduce a product's stock and reject the request if stock is insufficient.

### Order Service
- **FR-O1** The system shall allow placing an order for one product with a quantity and customer name.
- **FR-O2** When an order is placed, the Order Service shall call the Product Service to fetch the product and reduce its stock.
- **FR-O3** The system shall store the unit price and total price at the time of the order.
- **FR-O4** The system shall return the list of all orders and a single order by id.
- **FR-O5** The system shall return a clear error if the product does not exist, stock is insufficient, or the Product Service is unreachable.

### Angular App
- **FR-A1** The Products page shall list products in a table (name, price, stock).
- **FR-A2** The Products page shall allow adding, editing and deleting a product.
- **FR-A3** The Orders page shall allow placing an order by choosing a product, entering a quantity and customer name.
- **FR-A4** The Orders page shall list existing orders.
- **FR-A5** The app shall show a readable error message when an API call fails.

## 9. Non-Functional Requirements

- **NFR-1** Each service starts independently with `dotnet run` and the Angular app with `ng serve`.
- **NFR-2** Database tables are created through EF Core migrations (`dotnet ef database update`).
- **NFR-3** Connection strings and the Product Service URL are in `appsettings.json`, not hard-coded.
- **NFR-4** CORS is enabled so the Angular app (`http://localhost:4200`) can call both services.
- **NFR-5** Use dependency injection. Controllers stay thin; business logic lives in services.
- **NFR-6** Use correct HTTP status codes (see API contracts).

## 10. Acceptance Criteria

Format: Given / When / Then. Each criterion must pass for the project to be accepted.

### Product Service

| ID | Criteria |
|---|---|
| **AC-P1** | **Given** the Product Service is running, **when** I POST a valid product (name "Laptop", price 1200, stock 10), **then** I get 201 and the response contains the new id. |
| **AC-P2** | **Given** products exist, **when** I GET `/api/products`, **then** I get 200 and a list containing all products. |
| **AC-P3** | **Given** a product with id 1 exists, **when** I GET `/api/products/1`, **then** I get 200 and its details. |
| **AC-P4** | **Given** no product with id 999 exists, **when** I GET `/api/products/999`, **then** I get 404. |
| **AC-P5** | **Given** I POST a product with an empty name, or a price of 0 or less, or negative stock, **then** I get 400 and the product is not saved. |
| **AC-P6** | **Given** a product exists, **when** I PUT updated values, **then** I get 200 and a later GET shows the new values. |
| **AC-P7** | **Given** a product exists, **when** I DELETE it, **then** I get 204 and a later GET returns 404. |
| **AC-P8** | **Given** a product has stock 10, **when** I POST reduce-stock with quantity 3, **then** I get 200 and the stock becomes 7. |
| **AC-P9** | **Given** a product has stock 2, **when** I POST reduce-stock with quantity 5, **then** I get 409 and the stock stays 2. |

### Order Service

| ID | Criteria |
|---|---|
| **AC-O1** | **Given** product 1 has stock 10 and price 100, **when** I POST an order for quantity 2, **then** I get 201, the order has TotalPrice 200, and product 1 stock is now 8. |
| **AC-O2** | **Given** product 1 has stock 1, **when** I POST an order for quantity 5, **then** I get 409, no order is saved, and the stock is unchanged. |
| **AC-O3** | **Given** product 999 does not exist, **when** I POST an order for it, **then** I get 404 and no order is saved. |
| **AC-O4** | **Given** the Product Service is stopped, **when** I POST an order, **then** I get 503 with a clear message and no order is saved. |
| **AC-O5** | **Given** I POST an order with quantity 0 or an empty customer name, **then** I get 400. |
| **AC-O6** | **Given** orders exist, **when** I GET `/api/orders`, **then** I get 200 and all orders. |
| **AC-O7** | **Given** order 1 exists, **when** I GET `/api/orders/1`, **then** I get 200; for a missing id I get 404. |
| **AC-O8** | **Given** both databases exist, **then** `ProductDb` and `OrderDb` are separate and the Order Service has no connection string to `ProductDb`. |

### Angular App

| ID | Criteria |
|---|---|
| **AC-A1** | **Given** products exist, **when** I open the Products page, **then** I see them in a table with name, price and stock. |
| **AC-A2** | **When** I add, edit or delete a product on the Products page, **then** the table updates and the change persists after refresh. |
| **AC-A3** | **When** I place an order from the Orders page with valid input, **then** the order appears in the orders list and the product's stock on the Products page is reduced. |
| **AC-A4** | **When** I try to order more than the available stock, **then** I see an error message and no order is created. |

### Tests and Tooling

| ID | Criteria |
|---|---|
| **AC-T1** | There are at least 3 passing xUnit tests: (1) creating a product, (2) order rejected when stock is insufficient, (3) stock reduced when an order is placed. `dotnet test` passes. |
| **AC-T2** | A Postman collection (exported JSON) is in `/postman` and covers all endpoints of both services. |
| **AC-T3** | The README explains how to set up the databases, run migrations, start each service and start the Angular app. |
| **AC-T4** | Following the README from a clean checkout, the system runs end to end. |

## 11. Definition of Done

- [ ] All acceptance criteria above pass.
- [ ] All three parts (Product Service, Order Service, Angular app) run together locally.
- [ ] `dotnet test` passes.
- [ ] Postman collection and README are committed to Git.
- [ ] A short demo (5 minutes) can be given showing: add product, place order, stock reduced, insufficient-stock error.
- [ ] (Bonus, optional) Extra-point criteria in section 14 pass: pagination, price sorting, and search on the Products page.

## 12. Time Plan (3 hours)

| Time | Task |
|---|---|
| 0:00 - 0:15 | Setup: create solutions, install tools, create databases, commit this spec to the repo |
| 0:15 - 0:55 | Product Service: model, DbContext, migration, endpoints, validation (AC-P1 to AC-P9) |
| 0:55 - 1:35 | Order Service: model, migration, typed HttpClient to Product Service, endpoints (AC-O1 to AC-O8) |
| 1:35 - 1:55 | Unit tests (AC-T1) |
| 1:55 - 2:40 | Angular app: Products page and Orders page (AC-A1 to AC-A4) |
| 2:40 - 3:00 | Postman collection, README, end-to-end check, demo (AC-T2 to AC-T4) |

If time runs short, cut in this order: Angular edit/delete screens first, then extra validation. Never cut the core order flow (AC-O1, AC-O2) or the unit tests.

## 13. Suggested Approach for Building with Claude

1. Give Claude this whole spec file as context at the start.
2. Work one part at a time, in the order of the time plan. Ask Claude to implement only that part and its acceptance criteria.
3. After each part, run it and test it in Postman before moving on.
4. When something fails, paste the error and the relevant acceptance criterion back to Claude.
5. At the end, ask Claude to check every acceptance criterion against the code and list any gaps.

## 14. Bonus Requirements (Extra Points)

These are optional and earn extra points. Start them only after all core acceptance criteria (sections 10 and 11) pass. They apply to the **Products page in the Angular app** and are done on the frontend (client-side) using the product list already returned by `GET /api/products`. No API changes are needed.

### Requirements

- **FR-B1 Pagination** The Products page shall show products in pages of 5 rows, with controls to move between pages.
- **FR-B2 Sort by price** Clicking the Price column header shall sort the products by price.
- **FR-B3 Search** A search bar shall be shown above the products table. Entering a keyword shall filter the products in the table against that keyword.

### Rules

- Search matches the product name, is case-insensitive, and matches any part of the name ("contains").
- Search and sorting apply to the full product list, not only the current page. Pagination is applied last, on the filtered and sorted result.
- Changing the search keyword or the sort order returns the table to page 1.
- The sort and search must still work after a product is added, edited or deleted.

### Acceptance Criteria

| ID | Criteria |
|---|---|
| **AC-B1** | **Given** 12 products exist, **when** I open the Products page, **then** the table shows 5 products on page 1 and page controls are visible. |
| **AC-B2** | **Given** 12 products exist, **when** I go to the next page, **then** the next 5 products are shown, and on the last page the remaining 2 are shown. |
| **AC-B3** | **Given** I am on the first page, **then** the Previous control is disabled; **given** I am on the last page, **then** the Next control is disabled. |
| **AC-B4** | **Given** the products table is shown, **then** the current position is displayed (for example "Page 1 of 3"). |
| **AC-B5** | **When** I click the Price column header once, **then** all products are sorted by price from lowest to highest and the header shows an up arrow. |
| **AC-B6** | **When** I click the Price column header a second time, **then** all products are sorted from highest to lowest and the header shows a down arrow. |
| **AC-B7** | **Given** the products are sorted by price, **then** the sort applies across all pages (page 1 shows the lowest prices, the last page shows the highest), not only within the visible page. |
| **AC-B8** | **Given** a search bar is shown above the table, **when** I type "lap", **then** only products whose name contains "lap" (any letter case) are shown, for example "Laptop". |
| **AC-B9** | **Given** a keyword is entered, **then** pagination reflects only the matching products (page count and page numbers update) and the table shows page 1. |
| **AC-B10** | **When** I clear the search bar, **then** all products are shown again. |
| **AC-B11** | **When** no product matches the keyword, **then** the table shows a "No products found" message. |
| **AC-B12** | **Given** I have entered a keyword and sorted by price, **then** the table shows the matching products in price order, split into pages of 5. |

### Time Guidance

Allow about 20 to 30 minutes for the bonus items. If they cannot fit inside the 3 hours without risking the core criteria, skip them. A working core is worth more than a half-finished bonus.

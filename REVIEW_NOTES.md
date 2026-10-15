# Review call notes — Simple Order System

15 minutes, so lead with the demo, keep explanations short, have answers
ready for the "why did you do X" questions below.

## 1. Quick demo flow (~3 min)

1. `dotnet run` in both `product-service/ProductService.Api` and
   `order-service/OrderService.Api`, then `npm start` in `web-app`.
2. Products page: add a product, edit it, show it persists on refresh.
3. Orders page: place an order → point out stock dropped on Products page.
4. Try to order more than stock → show the readable error, no order created.
5. (If asked) stop Product Service, try to place an order → 503, not a crash.
6. Open Postman collection, run one request from each folder.
7. `dotnet test` in both services → 5 passing tests.

## 2. What was built, one sentence each

- **Product Service** (`:5001`) — owns `ProductDb`. CRUD + a `reduce-stock`
  endpoint that's the only way stock changes.
- **Order Service** (`:5002`) — owns `OrderDb`. Places orders by calling
  Product Service over HTTP; never touches `ProductDb` directly.
- **Angular app** (`:4200`) — Products page (CRUD + bonus search/sort/
  pagination) and Orders page, talking to both APIs directly.

## 3. Talking points — be ready to explain *why*, not just *what*

| Decision | Why |
|---|---|
| Two separate databases, no shared connection string | That's the whole point of microservices — each service owns its data; nothing else is allowed to write to it directly |
| Order Service only knows Product Service's base URL (config), not its DB | Services integrate through contracts (HTTP API), not shared schema |
| Typed `HttpClient` (`ProductServiceClient` : `IProductServiceClient`) | Registered via DI (`AddHttpClient<...>`), reused across requests, connection pooling handled by the framework — not `new HttpClient()` per call |
| Controllers are thin, logic lives in `Services/` | Keeps HTTP concerns (status codes, routing) separate from business rules (stock checks, price calc) — easier to unit test the logic without spinning up a server |
| DTOs separate from EF models | API shape can change independently of the database schema |
| Custom exceptions (`ProductNotFoundException`, `InsufficientStockException`, `ProductServiceUnavailableException`) caught in the controller | One place per service maps domain failures to HTTP status codes (404/409/503) instead of scattering `if` checks everywhere |
| `[ApiController]` + data annotations (`[Required]`, `[Range]`) | Framework auto-validates and returns 400 before the action method runs — no manual validation boilerplate |
| In-memory EF Core provider in tests | Tests run fast and don't need a real SQL Server, while the app itself always talks to real SQL Server via the SQL Server provider |

## 4. .NET ↔ Java mapping (useful if asked to compare)

| .NET / ASP.NET Core | Java / Spring equivalent |
|---|---|
| `[ApiController]`, `[HttpGet]` | `@RestController`, `@GetMapping` |
| `DbContext` / EF Core | `EntityManager` / Hibernate (JPA) |
| `dotnet ef migrations` | Flyway / Liquibase migrations |
| Built-in DI container (`builder.Services.AddScoped<...>`) | Spring's `ApplicationContext` / `@Autowired` |
| `IHttpClientFactory` / typed `HttpClient` | Spring's `RestTemplate` / `WebClient` |
| `appsettings.json` | `application.properties` / `application.yml` |
| xUnit + Moq | JUnit + Mockito |
| `record` DTOs | Java `record` (since Java 16) |

## 5. Known gaps / deliberate scope cuts (own them, don't get caught off guard)

- No auth — explicitly out of scope per the spec.
- No API gateway / message broker — explicitly out of scope; direct HTTP
  calls are intentional for a system this size.
- Delete confirmation uses a plain `window.confirm()` — fine for scope,
  would use a proper modal component in a larger app.
- Only 5 xUnit tests (spec required 3 minimum) — covers the riskiest
  logic (stock check, order placement), not exhaustive coverage.

## 6. If they ask "what would you do differently with more time"

- Add integration tests that spin up both services together (WebApplicationFactory)
  to test the real HTTP call between them, not just mocked.
- Add a correlation ID / basic structured logging across the service boundary.
- Angular: extract the error-handling into a shared interceptor instead of
  repeating `extractError` in both page components.

# Handbook: .NET for Spring Boot Developers

You know Spring Boot. This document maps everything in this repo to the
Spring Boot equivalent you already understand, then goes deeper on the
parts that don't have a clean 1:1 mapping (MassTransit, the outbox
pattern, EF Core). Read top to bottom once, then use it as a reference.

## Contents

1. [Project structure: solutions vs. Maven modules](#1-project-structure-solutions-vs-maven-modules)
2. [The host and startup: Program.cs vs. Application.java](#2-the-host-and-startup-programcs-vs-applicationjava)
3. [Dependency injection and lifetimes](#3-dependency-injection-and-lifetimes)
4. [Controllers, DTOs, and validation](#4-controllers-dtos-and-validation)
5. [EF Core vs. JPA/Hibernate](#5-ef-core-vs-jpahibernate)
6. [async/await vs. CompletableFuture](#6-asyncawait-vs-completablefuture)
7. [HttpClient, IHttpClientFactory, and connection pooling](#7-httpclient-ihttpclientfactory-and-connection-pooling)
8. [MassTransit vs. Spring AMQP / Spring Cloud Stream](#8-masstransit-vs-spring-amqp--spring-cloud-stream)
9. [The transactional outbox pattern](#9-the-transactional-outbox-pattern)
10. [Idempotency: two different problems with two different fixes](#10-idempotency-two-different-problems-with-two-different-fixes)
11. [Retries, redelivery, and the error queue](#11-retries-redelivery-and-the-error-queue)
12. [Concurrency: consumers and outbound HTTP](#12-concurrency-consumers-and-outbound-http)
13. [Docker and Docker Compose](#13-docker-and-docker-compose)
14. [Trace an order end to end](#14-trace-an-order-end-to-end)
15. [Failure scenarios (interview/review prep)](#15-failure-scenarios-interviewreview-prep)
16. [How to run everything](#16-how-to-run-everything)

---

## 1. Project structure: solutions vs. Maven modules

| Spring Boot / Maven | .NET |
|---|---|
| multi-module Maven/Gradle project | `.slnx` solution file listing `.csproj` projects |
| `pom.xml` per module | `.csproj` per project |
| a module's `<dependency>` | a project's `<PackageReference>` (NuGet) or `<ProjectReference>` (another project in the solution) |
| `mvn install` / `./gradlew build` | `dotnet build` |
| `mvn test` | `dotnet test` |

This repo isn't one solution — it's **three independent microservices**,
each a mini "multi-module project" of its own:

```
order-service/OrderService.slnx
  OrderService.Api            <- the HTTP API (like a Spring Boot web module)
  OrderService.Worker         <- a background consumer (like a @Component
                                  with @RabbitListener, but its own process)
  OrderSystem.Contracts       <- shared "API" module: just the event record
                                  types, nothing else. Both Api and Worker
                                  reference it, the same way two Maven
                                  modules both depend on a shared -api jar.
  OrderService.Tests          <- xUnit tests for Api
  OrderService.Worker.Tests   <- xUnit tests for Worker

product-service/ProductService.slnx   <- entirely separate solution
pricing-service/PricingService.slnx   <- entirely separate solution
```

**Why Contracts is its own tiny project instead of OrderService.Worker
just referencing OrderService.Api:** that would make the Worker depend on
the API's controllers, EF DbContext, everything — the opposite of a
microservice boundary. The Contracts project has exactly the three event
record types (`OrderSubmitted`, `OrderApproved`, `OrderRejected`) and
nothing else, the same discipline you'd apply to a shared Avro/protobuf
schema module in a Kafka-based Spring system.

There's no single `docker-compose.yml`-equivalent "parent pom" — each
`.slnx` only groups projects that are *literally the same deployable
service*.

## 2. The host and startup: Program.cs vs. Application.java

Spring Boot's `@SpringBootApplication` + `SpringApplication.run(...)`
scans for components and auto-configures beans. ASP.NET Core has no
scanning step — **everything is registered explicitly**, top to bottom,
in `Program.cs`. There's no "magic"; reading `Program.cs` top-to-bottom
*is* reading the entire application's wiring.

```csharp
// order-service/OrderService.Api/Program.cs (abridged)
var builder = WebApplication.CreateBuilder(args);   // like SpringApplication.run() starting

builder.Services.AddControllers();                   // like @EnableWebMvc / @RestController scanning
builder.Services.AddDbContext<OrderDbContext>(...);   // like @EnableJpaRepositories + DataSource bean
builder.Services.AddScoped<IOrderService, ...>();     // like @Service / @Component registration
builder.Services.AddMassTransit(x => { ... });        // like @EnableRabbit + listener container config

var app = builder.Build();     // ApplicationContext is now built — no more registering beans after this
app.UseCors(...);              // like a Spring Security/Web filter chain entry
app.MapControllers();          // like DispatcherServlet wiring up @RequestMapping
app.Run();                     // blocks, like SpringApplication.run() returning only on shutdown
```

`appsettings.json` is `application.yml`/`application.properties`.
`appsettings.Development.json` overlays on top of `appsettings.json` the
same way `application-dev.yml` overlays the base file when the `dev`
Spring profile is active — except here it's driven by `ASPNETCORE_ENVIRONMENT`
instead of `spring.profiles.active`, and the overlay always applies by
filename convention rather than needing `spring.config.activate.on-profile`.

`builder.Configuration["RabbitMq:Host"]` reading `"RabbitMq": { "Host": "..." }`
from JSON is exactly `@Value("${rabbitmq.host}")` reading
`rabbitmq.host: ...` from YAML — same nested-key-to-colon/dot convention,
same idea of environment variables overriding file config
(`RabbitMq__Host` env var overrides `RabbitMq:Host` in JSON, the same way
`RABBITMQ_HOST` would override `rabbitmq.host` in Spring's relaxed
binding).

## 3. Dependency injection and lifetimes

Both frameworks have a built-in DI container — you don't need
Autofac/Ninject any more than you'd reach for a third-party DI framework
in modern Spring Boot.

| .NET lifetime | Spring equivalent | One instance per... |
|---|---|---|
| `AddSingleton<T>()` | `@Scope("singleton")` (Spring's default) | the whole application |
| `AddScoped<T>()` | `@Scope("request")` / `@RequestScope` | one HTTP request (or one message-consume scope — see below) |
| `AddTransient<T>()` | `@Scope("prototype")` | every single injection |

The one genuinely different idea: **`Scoped` isn't just "per HTTP
request" in this codebase.** `OrderDbContext` is `AddDbContext` (scoped by
default). When a RabbitMQ consumer like `OrderApprovedConsumer` runs,
MassTransit creates a *new DI scope per message* — so the scoped
`OrderDbContext` you inject into a consumer is fresh per message, the same
lifetime guarantee EF Core needs (a `DbContext` is **not** thread-safe and
is meant to be short-lived, much like an `EntityManager` in JPA).

```csharp
public class OrderApprovedConsumer : IConsumer<OrderApproved>
{
    private readonly OrderDbContext _db;   // injected fresh per message
    public OrderApprovedConsumer(OrderDbContext db) { _db = db; }
    ...
}
```

Compare to a `@RabbitListener` method where you'd inject a
`@PersistenceContext EntityManager` or a `@Transactional` repository —
Spring's container handles the per-invocation transaction/session scope
for you in a similar way, just via AOP proxies instead of an explicit
scope factory.

## 4. Controllers, DTOs, and validation

```csharp
[ApiController]                    // like @RestController
[Route("api/orders")]              // like @RequestMapping("/api/orders")
public class OrdersController : ControllerBase
{
    [HttpPost]                                          // like @PostMapping
    public async Task<ActionResult<OrderResponse>> Create(
        OrderCreateRequest request,                      // like @RequestBody @Valid OrderCreateRequest
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)  // like @RequestHeader
    { ... }
}
```

`[ApiController]` is the key piece of "magic" here: it makes data
annotation validation (`[Required]`, `[Range(1, int.MaxValue)]` in
`OrderCreateRequest`) run automatically and return `400 Bad Request` with
a validation-errors body if it fails — you never call `ModelState.IsValid`
yourself, the same way `@Valid` + Spring's `MethodArgumentNotValidException`
handling returns 400 without you writing an `if`.

`record OrderResponse(...)` is a C# **record** — an immutable DTO with
structural equality generated for you, the direct equivalent of a Java
`record` (Java 16+) or a Lombok `@Value` DTO.

## 5. EF Core vs. JPA/Hibernate

| EF Core | JPA / Hibernate |
|---|---|
| `DbContext` | `EntityManager` (conceptually) / a Spring Data `Repository`'s backing session |
| `DbSet<Order>` | a JPA `Repository<Order, Integer>` |
| `[Key]`, `[Required]`, `[MaxLength(100)]` on a model | `@Id`, `@Column(nullable = false, length = 100)` |
| `modelBuilder.Entity<Order>()...` (fluent config in `OnModelCreating`) | `@Table`/`@Column` annotations, or a Hibernate `orm.xml` |
| `dotnet ef migrations add X` | Flyway/Liquibase migration generation (EF Core generates the SQL for you from model diffs — closer to Hibernate's `ddl-auto=update` crossed with Flyway's versioned-migration-files discipline) |
| `dotnet ef database update` | `flyway migrate` |
| LINQ (`_db.Orders.Where(o => ...).ToListAsync()`) | JPQL / Criteria API / Spring Data method names |
| `Database.Migrate()` called at startup (see `Program.cs`) | `spring.flyway.enabled=true` running migrations on boot |

**A subtlety worth knowing cold:** `OrderProcessingService.PlaceOrderAsync`
opens an **explicit transaction** (`Database.BeginTransactionAsync()`)
instead of just calling `SaveChangesAsync()` once:

```csharp
await using var transaction = await _db.Database.BeginTransactionAsync();
_db.Orders.Add(order);
await _db.SaveChangesAsync();                 // assigns order.Id (identity column)
await _publishEndpoint.Publish(new OrderSubmitted(order.Id, ...));  // needs the real Id
await _db.SaveChangesAsync();                 // flushes the outbox row
await transaction.CommitAsync();
```

Why two `SaveChangesAsync()` calls? `Order.Id` is a SQL Server IDENTITY
column — its real value doesn't exist until the INSERT happens. The event
needs that real `OrderId`, so: save once to get the id, build the event,
save again to persist it (into the outbox table — see §9), commit both
together. This is the same reasoning as calling
`entityManager.persist(order); entityManager.flush();` in JPA to force an
identity-generated id before you can reference it elsewhere in the same
`@Transactional` method.

## 6. async/await vs. CompletableFuture

C#'s `async`/`await` is closer to Kotlin coroutines or Project Loom
virtual threads than to `CompletableFuture` chaining — it's
**compiler-rewritten sequential code**, not callback composition:

```csharp
public async Task<OrderResponse> PlaceOrderAsync(...)
{
    var product = await _productClient.ReduceStockAsync(id, qty, ct);  // "await" = suspend here,
    var quote = await _pricingClient.GetQuoteAsync(..., ct);           // release the thread,
    return ToResponse(order);                                          // resume when done — reads
}                                                                       // top-to-bottom like sync code
```

versus Java's

```java
return reduceStockAsync(id, qty)
    .thenCompose(product -> getQuoteAsync(...))
    .thenApply(this::toResponse);
```

Same non-blocking guarantee (the underlying thread isn't parked waiting on
I/O), but C# reads like synchronous code. `CancellationToken` is the
direct equivalent of passing a cancellation signal through a `Future` —
every async method in this codebase accepts one and passes it all the way
down (`context.CancellationToken` from a MassTransit consumer, down into
`HttpClient` calls), so a message-level timeout or shutdown actually
cancels the in-flight HTTP call instead of leaking it.

## 7. HttpClient, IHttpClientFactory, and connection pooling

**Never do `new HttpClient()` per call.** Each `HttpClient` instance owns
its own connection pool; creating one per request exhausts sockets under
load (the infamous "socket exhaustion" .NET footgun) — the same failure
mode as creating a new `RestTemplate`/raw `HttpURLConnection` per request
in Java instead of reusing a pooled `RestTemplate`/`WebClient` bean.

```csharp
// OrderService.Worker/Program.cs
builder.Services.AddHttpClient<IPricingServiceClient, PricingServiceClient>(client =>
{
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
})
.AddStandardResilienceHandler(o => { ... });   // timeout + retry + circuit breaker
```

`AddHttpClient<TInterface, TImpl>` registers **one named, pooled
`HttpClient`** and a typed wrapper class around it, created once by
`IHttpClientFactory` and reused — the direct equivalent of defining a
single `@Bean RestTemplate pricingRestTemplate(RestTemplateBuilder b)` (or
a `WebClient` bean) and injecting that everywhere instead of `new`-ing one
per call.

`AddStandardResilienceHandler()` bolts on timeout + retry-with-backoff +
a circuit breaker in one line — think Resilience4j's
`@CircuitBreaker`/`@Retry` annotations, except it's Microsoft's own Polly
library wired directly into the `HttpClient` pipeline rather than an AOP
annotation.

`ConcurrencyLimitingHandler` (`OrderService.Worker/Clients/`) is a custom
`DelegatingHandler` wrapping a `SemaphoreSlim` — the .NET equivalent of
wrapping a Resilience4j `Bulkhead` or a `Semaphore`-guarded
`RestTemplate` call: it caps how many requests to Product Service can be
in flight at once, so a burst of concurrently-processed orders can't pile
on a slow dependency.

## 8. MassTransit vs. Spring AMQP / Spring Cloud Stream

MassTransit is a **messaging abstraction layer** over RabbitMQ/Azure
Service Bus/etc., the same role `spring-rabbit`/Spring Cloud Stream plays
over the same brokers. You write to MassTransit's API; swapping the
underlying broker is (mostly) a `Program.cs` config change, not a
rewrite — the assignment's "keep business logic independent of the
selected broker" requirement, satisfied structurally.

| MassTransit | Spring AMQP |
|---|---|
| `IPublishEndpoint.Publish<T>(message)` | `rabbitTemplate.convertAndSend(exchange, routingKey, message)` |
| `IConsumer<T>.Consume(ConsumeContext<T> context)` | a method annotated `@RabbitListener` |
| `x.AddConsumer<OrderApprovedConsumer>()` | registering the listener bean / `@RabbitListener`-annotated class |
| a C# `record` message type (`OrderSubmitted`) | your POJO DTO, serialized by Jackson |
| `cfg.ReceiveEndpoint("order-submitted-queue", e => ...)` | `@RabbitListener(queues = "order-submitted-queue")` + a `Queue` bean |
| `ConfigureEndpoints(context)` auto-naming queues from consumer type names | Spring Cloud Stream's convention-based destination binding |

**Publish vs. consume, concretely, in this repo:**

```csharp
// Order Service publishes — OrderService.Api/Services/OrderProcessingService.cs
await _publishEndpoint.Publish(new OrderSubmitted(order.Id, order.ProductId, order.Quantity, ...));
```

```csharp
// Order Worker consumes — OrderService.Worker/Consumers/OrderSubmittedConsumer.cs
public class OrderSubmittedConsumer : IConsumer<OrderSubmitted>
{
    public async Task Consume(ConsumeContext<OrderSubmitted> context)
    {
        var message = context.Message;   // the strongly-typed OrderSubmitted record
        ...
        await context.Publish(new OrderApproved(...));   // chain into the next event
    }
}
```

`Publish` is a **fan-out** — any number of consumers across any number of
services can subscribe to `OrderSubmitted` without the publisher knowing
or caring who's listening (today: just the Worker). That's the same
pub/sub model as a fanout/topic exchange in RabbitMQ or a Kafka topic with
multiple consumer groups — as opposed to `Send`, MassTransit's point-to-
point equivalent of a direct queue/command, not used in this codebase.

## 9. The transactional outbox pattern

**The problem it solves:** `OrderProcessingService.PlaceOrderAsync` needs
to do two things that must *both* happen or *neither* happen: insert the
`Order` row, and publish `OrderSubmitted`. A SQL `INSERT` and an AMQP
publish are two completely different systems — there's no way to wrap
them in one native database transaction. Without a special pattern you
get one of two bugs:

- Publish first, then the DB insert fails → the worker processes an order
  that doesn't exist.
- Insert first, then the process crashes before the publish → the order
  sits as `Submitted` forever, silently stuck, no event ever sent.

**The fix:** don't publish to RabbitMQ directly. Write the event into an
**outbox table in the same database, same transaction** as the `Order`
insert — now it's a single, ordinary ACID transaction, the thing SQL
Server is good at. A separate background process then reads unsent outbox
rows and relays them to RabbitMQ, retrying forever until it succeeds.

```csharp
// OrderDbContext.cs
modelBuilder.AddInboxStateEntity();
modelBuilder.AddOutboxMessageEntity();   // <- the outbox table itself
modelBuilder.AddOutboxStateEntity();
```

```csharp
// Program.cs
x.AddEntityFrameworkOutbox<OrderDbContext>(o =>
{
    o.UseSqlServer();
    o.UseBusOutbox();   // a hosted background service relays outbox rows to RabbitMQ
});
```

If you've used Spring, there's no first-party equivalent shipped in
`spring-rabbit` — this is normally hand-rolled (an `OutboxMessage` JPA
entity + `@Scheduled` poller, or Debezium reading the DB's transaction log
via CDC). MassTransit gives you the whole thing — table mapping, the
relay background service, and even built-in duplicate-publish protection —
in two config calls.

**Trade-off worth knowing:** this makes publish *eventually* consistent,
not instant. There's a small window (normally milliseconds) between
`SaveChangesAsync()` committing and the outbox relay actually publishing
to RabbitMQ. For this use case (processing doesn't need to start within
microseconds) that's the right trade for the guarantee it buys.

## 10. Idempotency: two different problems with two different fixes

The assignment asks for idempotency in two different places, and it's
easy to conflate them — they're solved differently on purpose:

**(a) Client retries the same HTTP request** (their network blipped, they
didn't see the 201 response, so they resend the exact same "place this
order" request). Fixed with an `Idempotency-Key` header:

```csharp
// OrdersController.cs
public async Task<ActionResult<OrderResponse>> Create(
    OrderCreateRequest request,
    [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)

// OrderProcessingService.cs
var existing = await _db.Orders.FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey);
if (existing is not null) return ToResponse(existing);   // return the ORIGINAL order, don't recreate
```

Backed by a unique index on `Order.IdempotencyKey` (`OrderDbContext.cs`)
so even a race between two near-simultaneous retries can't slip through.
This is the same pattern Stripe's API popularized — if you've used an
`Idempotency-Key` header against Stripe, this is the same contract.

**(b) RabbitMQ redelivers the same message** (the Worker crashed after
reducing stock but before acking the message — RabbitMQ, like virtually
every broker, guarantees **at-least-once** delivery, never exactly-once).
Fixed with the **inbox pattern** — the mirror image of the outbox:

```csharp
// OrderSubmittedConsumer.cs
if (await _db.ProcessedMessages.FindAsync(context.MessageId!.Value) is not null)
{
    return;   // already handled this exact message, skip — don't reduce stock twice
}
... do the work ...
_db.ProcessedMessages.Add(new ProcessedMessage { MessageId = context.MessageId.Value, ... });
```

`context.MessageId` is a GUID MassTransit assigns **once per publish**,
not once per delivery attempt — so a redelivered message carries the
*same* MessageId, which is what makes it a safe dedupe key. This is the
"consumer idempotency" problem you'd solve in Spring with a
`@RabbitListener` method checking a `processed_messages` table (or Redis
`SETNX`) before doing real work — same idea, hand-rolled here into a tiny
SQLite table instead of a shared service, because it only needs to be
visible to this one Worker instance (see `§13` for why SQLite, not SQL
Server, was the right call here).

## 11. Retries, redelivery, and the error queue

```csharp
cfg.ReceiveEndpoint("order-submitted-queue", e =>
{
    e.UseMessageRetry(r => r.Exponential(
        retryLimit: 5,
        minInterval: TimeSpan.FromSeconds(1),
        maxInterval: TimeSpan.FromSeconds(30),
        intervalDelta: TimeSpan.FromSeconds(5)));
    ...
});
```

If `OrderSubmittedConsumer.Consume` throws, MassTransit redelivers the
message up to 5 times with exponential backoff **before** the message is
nacked permanently — comparable to Spring AMQP's
`SimpleRetryPolicy`/`RetryTemplate` wired into a listener container, or
`spring-retry`'s `@Retryable`. After retries are exhausted, MassTransit
automatically moves the message to an `order-submitted-queue_error` queue
(RabbitMQ transport convention) — the direct equivalent of a manually
configured Dead Letter Exchange/Queue in raw RabbitMQ or Spring AMQP.

**The deliberate split in this codebase** (`OrderSubmittedConsumer.ProcessAsync`):
a `ProductNotFoundException`/`InsufficientStockException` is a **business
outcome** — caught, turned into a published `OrderRejected`, never
rethrown, so it's *not* retried (retrying "product doesn't exist" five
times would never succeed and would just delay a correct rejection). A
`ProductServiceUnavailableException` (the HTTP call itself failed) is
**transient infrastructure failure** — deliberately left uncaught so it
propagates and triggers the retry policy above, because "try again in a
few seconds" is exactly the right response to "the dependency was briefly
down."

## 12. Concurrency: consumers and outbound HTTP

Two independent concurrency knobs, easy to confuse:

```csharp
// How many OrderSubmitted messages this Worker instance processes AT ONCE
e.ConcurrentMessageLimit = 8;
```
≈ a Spring AMQP listener container's `concurrentConsumers`/`maxConcurrentConsumers`.

```csharp
// How many outbound HTTP calls to Product Service can be in flight AT ONCE
.AddHttpMessageHandler(() => new ConcurrencyLimitingHandler(maxConcurrentRequests: 10));
```
≈ a Resilience4j `Bulkhead`, or a `Semaphore` around a `RestTemplate` call.

Without the second one, 8 concurrently-processing messages could fire 8
simultaneous requests at a struggling Product Service and make things
worse, not better — the two limits compose: message concurrency controls
*how much work starts*, the HTTP semaphore controls *how much of that work
can hit one dependency at once*.

Scaling **out** instead of up: `docker compose up -d --scale order-worker=3`
runs three Worker processes, all consuming from the same
`order-submitted-queue`. RabbitMQ round-robins unconsumed messages across
them — the same effect as running multiple instances of a Spring Boot
service behind the same `@RabbitListener` queue, no code change required,
because "how many consumers share this queue" is a broker-level concept,
not an application one.

## 13. Docker and Docker Compose

`docker-compose.yml` is the closest thing to a single page describing
"what does this whole system consist of." Service by service:

- **`sqlserver`** — one database *server*, hosting two separate logical
  databases (`ProductDb`, `OrderDb`). Each microservice still only has a
  connection string to its own database — "one instance, many databases"
  mirrors how a lot of real deployments run Postgres/MySQL (cost-driven),
  while still respecting the "each service owns its own schema" rule.
- **`rabbitmq`** — the broker. The `rabbitmq:3-management-alpine` image
  includes a web UI at `:15672` (guest/guest) where you can watch queues,
  message rates, and the `_error` dead-letter queue fill up live.
- **`order-worker`** — note it has **no published port**. It's a pure
  background consumer with no inbound HTTP server — nothing calls it, it
  only calls out (to Product Service, Pricing Service) and talks to
  RabbitMQ. This is the Docker-level signal that it's a worker, not an
  API, the same way you wouldn't `EXPOSE` a port on a Spring Boot
  `@Component`-only JAR with no embedded Tomcat.
- **Why the Worker's inbox is SQLite, not another SQL Server database:**
  it only needs to answer "have I already handled this exact message?"
  for *this one process* — no other service ever reads it, so a
  heavyweight shared SQL Server database would be the wrong tool. SQLite
  is a single file, mounted as its own named volume (`worker-data`) so it
  survives a container restart without needing a whole extra database
  server.
- Every service's Dockerfile is a **multi-stage build**: an SDK image
  compiles and publishes the app, then a slim ASP.NET runtime image
  (`mcr.microsoft.com/dotnet/aspnet:10.0`) only copies the *published
  output* — the SDK, NuGet cache, and source code never ship in the final
  image. Same motivation as a Maven/Gradle multi-stage Dockerfile that
  builds with a `maven:...` image and runs with a slim `eclipse-temurin:...-jre`
  image instead of shipping the whole JDK + build cache.
- `healthcheck` + `depends_on: condition: service_healthy` (on `sqlserver`
  and `rabbitmq`) is how `order-service`/`order-worker` wait for their
  real dependencies to be *ready to accept connections*, not just "the
  container process started" — the same gap `wait-for-it.sh`/Testcontainers'
  wait strategies close in a Java/Docker Compose setup.

## 14. Trace an order end to end

1. **Client** → `POST /api/orders` (`OrdersController.Create`).
2. **Order API**: validates (`[ApiController]` + data annotations), checks
   `Idempotency-Key` for an existing match, else inserts an `Order` row
   with `Status = Submitted` and an `OrderSubmitted` outbox row — one DB
   transaction (`OrderProcessingService.PlaceOrderAsync`). Returns `201`
   with the order immediately. **No stock check, no pricing yet.**
3. MassTransit's outbox delivery service picks up the row and publishes
   `OrderSubmitted` to RabbitMQ (usually within milliseconds).
4. **Order Worker**'s `OrderSubmittedConsumer` receives it off
   `order-submitted-queue`. First check: has this exact `MessageId` been
   processed before? (inbox dedupe, §10b). If not:
   - Calls Product Service `POST /api/products/{id}/reduce-stock` — the
     "inventory validation" step. `404`/`409` → publish `OrderRejected`,
     done. Connection failure → **throw**, let MassTransit retry (§11).
   - Calls Pricing Service `POST /api/pricing/quote` through a resilient,
     pooled `HttpClient` (§7) — gets back a (possibly discounted) final
     price.
   - Publishes `OrderApproved` with the final price, records this
     `MessageId` as processed.
5. **Order API**'s `OrderApprovedConsumer` (or `OrderRejectedConsumer`)
   receives it, looks up the `Order` by id, and updates `Status`,
   `UnitPrice`/`TotalPrice` (or `RejectionReason`) — guarded against
   double-applying the same terminal status if this event is ever
   redelivered (§10b's idea, applied here too — see the consumer code).
6. **Client** polls `GET /api/orders/{id}` and sees the final state.

No step in 3–5 blocks step 2's HTTP response — that's the entire point of
"accept, then process asynchronously."

## 15. Failure scenarios (interview/review prep)

**"What happens if RabbitMQ is down when Order Service tries to publish?"**
Nothing breaks the API request — the outbox row is already committed to
SQL Server as part of the same transaction as the Order insert. The outbox
delivery service keeps retrying to publish it once RabbitMQ comes back.
The order just sits as `Submitted` a bit longer than usual; nothing is
lost.

**"What happens if Product Service is unreachable when the Worker tries
to process an order?"** `ProductServiceClient` throws
`ProductServiceUnavailableException`, which `OrderSubmittedConsumer`
deliberately does **not** catch — it propagates, the message is nacked,
and MassTransit's `UseMessageRetry` redelivers it with exponential
backoff (up to 5 attempts) before it lands in the `_error` queue for
manual inspection. The order stays `Submitted` the whole time — it is
never wrongly marked `Rejected` just because a dependency hiccuped.

**"What happens if Pricing Service is slow or returns 503?"**
`AddStandardResilienceHandler()` on the Worker's Pricing client applies a
per-attempt timeout, several retries, and a circuit breaker — most
transient Pricing Service failures resolve themselves inside that policy
without the surrounding message-level retry ever needing to kick in. If
they don't, the exception still propagates the same way as the Product
Service case above.

**"How are duplicate client requests handled?"** `Idempotency-Key` header
+ unique DB index (§10a) — a second request with the same key returns the
original order rather than creating a new one.

**"How are duplicate messages handled?"** Every consumer either does
something naturally idempotent (setting `Status = Approved` twice is a
no-op) or explicitly checks a dedupe table before doing anything with a
side effect (reducing stock) — the inbox pattern (§10b).

**"Why not just make the whole thing synchronous — call Product Service
and Pricing Service directly inside `POST /api/orders`?"** That was the
*original* (pre-advanced-assignment) design. The downside: the client's
response time becomes the sum of every downstream call's latency, and a
slow/down Pricing Service makes placing an order itself fail, even though
"save that someone wants to order this" has nothing to do with pricing
being available at that exact instant. Decoupling the two means the API
stays fast and available even when downstream dependencies aren't.

<a id="why-doesnt-the-worker-just-update-orderdb-directly"></a>
**"Why doesn't the Worker just update `OrderDb` directly instead of
publishing `OrderApproved`/`OrderRejected` back to Order Service?"** The
architecture diagram in the assignment brief actually shows the worker
touching the database directly — this implementation deliberately
diverges from that for one reason: **exactly one service should ever
write to a given database.** If the Worker wrote to `OrderDb` too, you'd
have two different codebases (Api and Worker) that both need to agree on
every schema change, migration, and EF model — the moment they drift,
you get silent bugs. Publishing the result back as an event and letting
Order Service be the only writer keeps that invariant intact, at the cost
of one more hop. This is a real trade-off worth being able to defend in
review — both designs are legitimate, this repo picked database-ownership
purity over minimizing hops.

## 16. How to run everything

See the main [README.md](./README.md) — `docker compose up --build` is
the fastest path to seeing all of the above happen live, including the
RabbitMQ management UI at http://localhost:15672 to watch messages flow
in real time.

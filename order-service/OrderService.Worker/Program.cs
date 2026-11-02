using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Worker.Clients;
using OrderService.Worker.Consumers;
using OrderService.Worker.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<WorkerDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("WorkerDb")));

// Typed HttpClients: one pooled HttpClient per interface, created once by
// IHttpClientFactory and reused for every call (never `new HttpClient()`
// per request — that leaks sockets under load because each instance owns
// its own connection pool and finalizer-based cleanup is unreliable).
builder.Services.AddHttpClient<IProductServiceClient, ProductServiceClient>(client =>
{
    var baseUrl = builder.Configuration["ProductService:BaseUrl"]
        ?? throw new InvalidOperationException("ProductService:BaseUrl is not configured.");
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
})
    // Caps how many requests to Product Service this process will have
    // in flight at once, so a slow/overloaded Product Service can't make
    // the worker pile up unbounded concurrent calls — backpressure instead
    // of a thundering herd.
    .AddHttpMessageHandler(() => new ConcurrencyLimitingHandler(maxConcurrentRequests: 10));

builder.Services.AddHttpClient<IPricingServiceClient, PricingServiceClient>(client =>
{
    var baseUrl = builder.Configuration["PricingService:BaseUrl"]
        ?? throw new InvalidOperationException("PricingService:BaseUrl is not configured.");
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
})
    // Timeout + retry-with-backoff + circuit breaker, out of the box.
    .AddStandardResilienceHandler(o =>
    {
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
        o.Retry.MaxRetryAttempts = 3;
    });

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderSubmittedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });

        // "order-submitted-queue" is the queue this worker consumes from.
        // Explicit name (rather than ConfigureEndpoints' auto-generated one)
        // so it reads clearly in the RabbitMQ management UI and in docs.
        cfg.ReceiveEndpoint("order-submitted-queue", e =>
        {
            e.UseMessageRetry(r => r.Exponential(
                retryLimit: 5,
                minInterval: TimeSpan.FromSeconds(1),
                maxInterval: TimeSpan.FromSeconds(30),
                intervalDelta: TimeSpan.FromSeconds(5)));

            // How many OrderSubmitted messages this worker instance will
            // process in parallel. Too high and a burst of orders can
            // overwhelm Product/Pricing Service; too low and the queue
            // backs up during normal load. 8 is a reasonable default for a
            // single instance — scale out more worker instances instead of
            // raising this indefinitely.
            e.ConcurrentMessageLimit = 8;

            e.ConfigureConsumer<OrderSubmittedConsumer>(context);
        });
    });
});

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();
    db.Database.Migrate();
}

host.Run();

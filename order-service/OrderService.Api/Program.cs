using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Api.Consumers;
using OrderService.Api.Data;
using OrderService.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("OrderDb")));

builder.Services.AddScoped<IOrderService, OrderProcessingService>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderApprovedConsumer>();
    x.AddConsumer<OrderRejectedConsumer>();

    // Transactional outbox: anything sent/published through the MassTransit
    // scoped endpoints inside an OrderDbContext.SaveChangesAsync() call is
    // written to the outbox tables in that same DB transaction, then
    // relayed to Azure Service Bus by a background bus-outbox delivery
    // service. See OrderProcessingService.
    x.AddEntityFrameworkOutbox<OrderDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
    });

    x.UsingAzureServiceBus((context, cfg) =>
    {
        var connectionString = builder.Configuration["ServiceBus:ConnectionString"]
            ?? throw new InvalidOperationException("ServiceBus:ConnectionString is not configured.");
        cfg.Host(connectionString);

        // Azure Service Bus Basic tier (the tier this project's sandbox
        // namespace uses) supports queues only — no topics/subscriptions,
        // so no Publish-based fan-out. Every event in this system has
        // exactly one consumer anyway, so OrderService explicitly Sends
        // OrderSubmitted to a named queue (OrderProcessingService) instead
        // of Publishing it, and consumes OrderApproved/OrderRejected off
        // their own named queues below — plain point-to-point messaging,
        // which also happens to work unchanged if you swap back to
        // RabbitMQ or a Standard/Premium Service Bus tier later.
        cfg.ReceiveEndpoint("order-approved-queue", e =>
        {
            e.PublishFaults = false; // faults use Publish internally; Basic tier has no topics for them
            // By default MassTransit also provisions a topic+subscription
            // for every message type a receive endpoint consumes, so that
            // Publish<T> from anywhere would still reach it. Basic tier
            // can't create topics at all, and we only ever Send directly
            // to this queue anyway, so skip that topology entirely.
            e.ConfigureConsumeTopology = false;
            e.UseMessageRetry(r => r.Exponential(
                retryLimit: 5,
                minInterval: TimeSpan.FromSeconds(1),
                maxInterval: TimeSpan.FromSeconds(30),
                intervalDelta: TimeSpan.FromSeconds(5)));
            e.ConfigureConsumer<OrderApprovedConsumer>(context);
        });

        cfg.ReceiveEndpoint("order-rejected-queue", e =>
        {
            e.PublishFaults = false;
            e.ConfigureConsumeTopology = false;
            e.UseMessageRetry(r => r.Exponential(
                retryLimit: 5,
                minInterval: TimeSpan.FromSeconds(1),
                maxInterval: TimeSpan.FromSeconds(30),
                intervalDelta: TimeSpan.FromSeconds(5)));
            e.ConfigureConsumer<OrderRejectedConsumer>(context);
        });
    });
});

const string AngularClient = "AngularClient";
builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularClient, policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            db.Database.Migrate();
            break;
        }
        catch (Exception) when (attempt < 10)
        {
            // The SQL Server container can take a few seconds to accept
            // connections after it reports "healthy" — retry instead of
            // crashing on the first attempt.
            Thread.Sleep(TimeSpan.FromSeconds(3));
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(AngularClient);
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }

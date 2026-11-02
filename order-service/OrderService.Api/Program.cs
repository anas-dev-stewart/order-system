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

    // Transactional outbox: anything published through IPublishEndpoint
    // inside an OrderDbContext.SaveChangesAsync() call is written to the
    // outbox tables in that same DB transaction, then relayed to RabbitMQ
    // by a background bus-outbox delivery service. See OrderProcessingService.
    x.AddEntityFrameworkOutbox<OrderDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });

        // Redelivery with backoff before a message lands in the service's
        // _error queue for manual inspection.
        cfg.UseMessageRetry(r => r.Exponential(
            retryLimit: 5,
            minInterval: TimeSpan.FromSeconds(1),
            maxInterval: TimeSpan.FromSeconds(30),
            intervalDelta: TimeSpan.FromSeconds(5)));

        cfg.ConfigureEndpoints(context);
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

using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Api.Consumers;
using OrderService.Api.Data;
using OrderService.Api.Models;
using OrderSystem.Contracts;
using Xunit;

namespace OrderService.Tests;

/// <summary>
/// Exercises OrderApprovedConsumer/OrderRejectedConsumer through a real
/// (in-memory transport) MassTransit bus using the test harness, rather
/// than calling Consume() directly — this is "message processing", end to
/// end through publish/consume, the same way it happens in production.
/// </summary>
public class OrderStatusConsumersTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<OrderDbContext>(o => o.UseInMemoryDatabase(databaseName));

        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<OrderApprovedConsumer>();
            x.AddConsumer<OrderRejectedConsumer>();
        });

        _provider = services.BuildServiceProvider(validateScopes: true);
        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
    }

    private async Task<Order> SeedOrderAsync(OrderStatus status = OrderStatus.Submitted)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var order = new Order { CustomerName = "Test", ProductId = 1, Quantity = 1, Status = status, CreatedAtUtc = DateTime.UtcNow };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    [Fact]
    public async Task OrderApproved_UpdatesOrderToApproved_WithPrice()
    {
        var order = await SeedOrderAsync();

        await _harness.Bus.Publish(new OrderApproved(order.Id, 25m, 50m, DateTime.UtcNow));

        Assert.True(await _harness.Consumed.Any<OrderApproved>());

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var updated = await db.Orders.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Approved, updated!.Status);
        Assert.Equal(50m, updated.TotalPrice);
    }

    [Fact]
    public async Task OrderRejected_UpdatesOrderToRejected_WithReason()
    {
        var order = await SeedOrderAsync();

        await _harness.Bus.Publish(new OrderRejected(order.Id, "Out of stock", DateTime.UtcNow));

        Assert.True(await _harness.Consumed.Any<OrderRejected>());

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var updated = await db.Orders.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Rejected, updated!.Status);
        Assert.Equal("Out of stock", updated.RejectionReason);
    }

    [Fact]
    public async Task OrderApproved_RedeliveredForAlreadyApprovedOrder_DoesNotChangePrice()
    {
        // Simulates duplicate delivery of the same event (e.g. the worker's
        // ack to RabbitMQ was lost after it had already published once).
        var order = await SeedOrderAsync(OrderStatus.Approved);
        order.UnitPrice = 25m;
        order.TotalPrice = 50m;

        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            db.Attach(order).State = EntityState.Modified;
            await db.SaveChangesAsync();
        }

        // A different price arrives on the duplicate — if this were applied,
        // it would silently corrupt an already-settled order.
        await _harness.Bus.Publish(new OrderApproved(order.Id, 999m, 999m, DateTime.UtcNow));
        Assert.True(await _harness.Consumed.Any<OrderApproved>());

        using var verifyScope = _provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var unchanged = await verifyDb.Orders.FindAsync(order.Id);
        Assert.Equal(25m, unchanged!.UnitPrice);
    }
}

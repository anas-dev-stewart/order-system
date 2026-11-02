using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrderService.Worker.Clients;
using OrderService.Worker.Consumers;
using OrderService.Worker.Data;
using OrderSystem.Contracts;
using Xunit;

namespace OrderService.Worker.Tests;

public class OrderSubmittedConsumerTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;
    private Mock<IProductServiceClient> _productClient = null!;
    private Mock<IPricingServiceClient> _pricingClient = null!;

    public async Task InitializeAsync()
    {
        _productClient = new Mock<IProductServiceClient>();
        _pricingClient = new Mock<IPricingServiceClient>();

        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<WorkerDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddSingleton(_productClient.Object);
        services.AddSingleton(_pricingClient.Object);

        services.AddMassTransitTestHarness(x => x.AddConsumer<OrderSubmittedConsumer>());

        _provider = services.BuildServiceProvider(validateScopes: true);
        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
    }

    private WorkerDbContext NewDbContext()
    {
        var scope = _provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<WorkerDbContext>();
    }

    [Fact]
    public async Task OrderSubmitted_WithStockAndPricingAvailable_SendsOrderApproved()
    {
        _productClient
            .Setup(c => c.ReduceStockAsync(1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductClientDto(1, "Widget", 100m, 8));
        _pricingClient
            .Setup(c => c.GetQuoteAsync(It.IsAny<PricingQuoteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingQuoteResponse(95m, 190m));

        await _harness.Bus.Publish(new OrderSubmitted(42, 1, 2, "Ali", DateTime.UtcNow));

        Assert.True(await _harness.Consumed.Any<OrderSubmitted>());
        var approved = await _harness.Sent.Any<OrderApproved>(m => m.Context.Message.OrderId == 42);
        Assert.True(approved);
        Assert.False(await _harness.Sent.Any<OrderRejected>());
    }

    [Fact]
    public async Task OrderSubmitted_WhenProductDoesNotExist_SendsOrderRejected_WithoutCallingPricing()
    {
        _productClient
            .Setup(c => c.ReduceStockAsync(99, 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProductNotFoundException(99));

        await _harness.Bus.Publish(new OrderSubmitted(7, 99, 1, "Sara", DateTime.UtcNow));

        var rejected = await _harness.Sent.Any<OrderRejected>(m => m.Context.Message.OrderId == 7);
        Assert.True(rejected);
        _pricingClient.Verify(c => c.GetQuoteAsync(It.IsAny<PricingQuoteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OrderSubmitted_WhenStockInsufficient_SendsOrderRejected()
    {
        _productClient
            .Setup(c => c.ReduceStockAsync(1, 50, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InsufficientStockException(1));

        await _harness.Bus.Publish(new OrderSubmitted(8, 1, 50, "Sara", DateTime.UtcNow));

        var rejected = await _harness.Sent.Any<OrderRejected>(m => m.Context.Message.OrderId == 8);
        Assert.True(rejected);
    }

    [Fact]
    public async Task OrderSubmitted_WhenProductServiceUnavailable_FaultsTheMessage_AndSendsNothing()
    {
        // Transient infrastructure failure: should NOT be treated as a
        // business rejection. The consumer lets the exception propagate so
        // MassTransit's retry policy (configured in Program.cs) gets a
        // chance to try again instead of permanently rejecting an order
        // just because Product Service hiccuped once.
        _productClient
            .Setup(c => c.ReduceStockAsync(1, 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProductServiceUnavailableException());

        await _harness.Bus.Publish(new OrderSubmitted(9, 1, 1, "Sara", DateTime.UtcNow));

        Assert.True(await _harness.Published.Any<Fault<OrderSubmitted>>());
        Assert.False(await _harness.Sent.Any<OrderApproved>());
        Assert.False(await _harness.Sent.Any<OrderRejected>());

        await using var db = NewDbContext();
        Assert.Empty(db.ProcessedMessages);
    }

    [Fact]
    public async Task OrderSubmitted_Redelivered_IsSkipped_AndNeverCallsProductService()
    {
        var messageId = Guid.NewGuid();

        await using (var db = NewDbContext())
        {
            db.ProcessedMessages.Add(new ProcessedMessage { MessageId = messageId, ProcessedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        await _harness.Bus.Publish(new OrderSubmitted(10, 1, 1, "Sara", DateTime.UtcNow), ctx => ctx.MessageId = messageId);

        Assert.True(await _harness.Consumed.Any<OrderSubmitted>());
        _productClient.Verify(c => c.ReduceStockAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(await _harness.Sent.Any<OrderApproved>());
        Assert.False(await _harness.Sent.Any<OrderRejected>());
    }
}

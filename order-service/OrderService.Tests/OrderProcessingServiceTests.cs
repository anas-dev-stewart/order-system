using MassTransit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderService.Api.Data;
using OrderService.Api.Dtos;
using OrderService.Api.Models;
using OrderService.Api.Services;
using OrderSystem.Contracts;
using Xunit;

namespace OrderService.Tests;

/// <summary>
/// Uses a real (in-memory) SQLite connection rather than the EF Core
/// InMemory provider, because OrderProcessingService opens an explicit
/// relational transaction (Database.BeginTransactionAsync) — the InMemory
/// provider doesn't support that and throws.
/// </summary>
public class OrderProcessingServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private OrderDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new OrderDbContext(options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static Mock<ISendEndpointProvider> CreateSendEndpointProviderMock(out Mock<ISendEndpoint> sendEndpointMock)
    {
        sendEndpointMock = new Mock<ISendEndpoint>();
        var providerMock = new Mock<ISendEndpointProvider>();
        providerMock
            .Setup(p => p.GetSendEndpoint(It.IsAny<Uri>()))
            .ReturnsAsync(sendEndpointMock.Object);
        return providerMock;
    }

    [Fact]
    public async Task PlaceOrderAsync_SavesOrderAsSubmitted_AndSendsOrderSubmitted()
    {
        var providerMock = CreateSendEndpointProviderMock(out var sendEndpointMock);
        var service = new OrderProcessingService(_db, providerMock.Object, NullLogger<OrderProcessingService>.Instance);

        var result = await service.PlaceOrderAsync(
            new OrderCreateRequest { CustomerName = "Ali", ProductId = 1, Quantity = 2 },
            idempotencyKey: null);

        Assert.Equal(OrderStatus.Submitted, result.Status);
        Assert.Null(result.UnitPrice);
        Assert.Single(_db.Orders);

        sendEndpointMock.Verify(e => e.Send(
            It.Is<OrderSubmitted>(m => m.OrderId == result.Id && m.ProductId == 1 && m.Quantity == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PlaceOrderAsync_WithRepeatedIdempotencyKey_ReturnsExistingOrder_AndDoesNotSendAgain()
    {
        var providerMock = CreateSendEndpointProviderMock(out var sendEndpointMock);
        var service = new OrderProcessingService(_db, providerMock.Object, NullLogger<OrderProcessingService>.Instance);
        var request = new OrderCreateRequest { CustomerName = "Sara", ProductId = 1, Quantity = 1 };

        var first = await service.PlaceOrderAsync(request, idempotencyKey: "client-retry-key-1");
        var second = await service.PlaceOrderAsync(request, idempotencyKey: "client-retry-key-1");

        Assert.Equal(first.Id, second.Id);
        Assert.Single(_db.Orders);
        sendEndpointMock.Verify(e => e.Send(It.IsAny<OrderSubmitted>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

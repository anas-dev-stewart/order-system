using Microsoft.EntityFrameworkCore;
using Moq;
using OrderService.Api.Clients;
using OrderService.Api.Data;
using OrderService.Api.Dtos;
using OrderService.Api.Services;
using Xunit;

namespace OrderService.Tests;

public class OrderProcessingServiceTests
{
    private static OrderDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OrderDbContext(options);
    }

    [Fact]
    public async Task PlaceOrderAsync_ThrowsInsufficientStock_AndSavesNoOrder_WhenProductClientRejects()
    {
        await using var db = CreateContext();
        var clientMock = new Mock<IProductServiceClient>();
        clientMock
            .Setup(c => c.GetProductAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductClientDto(1, "Gadget", 50, 1));
        clientMock
            .Setup(c => c.ReduceStockAsync(1, 5, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InsufficientStockException(1));

        var service = new OrderProcessingService(db, clientMock.Object);

        await Assert.ThrowsAsync<InsufficientStockException>(() => service.PlaceOrderAsync(
            new OrderCreateRequest { CustomerName = "Sara", ProductId = 1, Quantity = 5 }));

        Assert.Empty(db.Orders);
    }

    [Fact]
    public async Task PlaceOrderAsync_ReducesStockAndSavesOrder_WhenProductHasEnoughStock()
    {
        await using var db = CreateContext();
        var clientMock = new Mock<IProductServiceClient>();
        clientMock
            .Setup(c => c.GetProductAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductClientDto(1, "Widget", 100, 10));
        clientMock
            .Setup(c => c.ReduceStockAsync(1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductClientDto(1, "Widget", 100, 8));

        var service = new OrderProcessingService(db, clientMock.Object);

        var result = await service.PlaceOrderAsync(
            new OrderCreateRequest { CustomerName = "Ali", ProductId = 1, Quantity = 2 });

        Assert.Equal(200, result.TotalPrice);
        Assert.Single(db.Orders);
        clientMock.Verify(c => c.ReduceStockAsync(1, 2, It.IsAny<CancellationToken>()), Times.Once);
    }
}

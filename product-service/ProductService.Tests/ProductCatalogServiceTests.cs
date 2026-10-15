using Microsoft.EntityFrameworkCore;
using ProductService.Api.Data;
using ProductService.Api.Dtos;
using ProductService.Api.Services;
using Xunit;

namespace ProductService.Tests;

public class ProductCatalogServiceTests
{
    private static ProductDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ProductDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ProductDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_SavesProductAndReturnsResponseWithId()
    {
        await using var db = CreateContext();
        var service = new ProductCatalogService(db);

        var result = await service.CreateAsync(new ProductCreateRequest
        {
            Name = "Laptop",
            Price = 1200,
            StockQuantity = 10
        });

        Assert.True(result.Id > 0);
        Assert.Equal("Laptop", result.Name);
        Assert.Equal(1200, result.Price);
        Assert.Equal(10, result.StockQuantity);
        Assert.Single(db.Products);
    }

    [Fact]
    public async Task ReduceStockAsync_ThrowsInsufficientStock_WhenRequestExceedsAvailable()
    {
        await using var db = CreateContext();
        var service = new ProductCatalogService(db);
        var created = await service.CreateAsync(new ProductCreateRequest
        {
            Name = "Gadget",
            Price = 50,
            StockQuantity = 2
        });

        await Assert.ThrowsAsync<InsufficientStockException>(
            () => service.ReduceStockAsync(created.Id, 5));

        var unchanged = await service.GetByIdAsync(created.Id);
        Assert.Equal(2, unchanged!.StockQuantity);
    }

    [Fact]
    public async Task ReduceStockAsync_ReducesStock_WhenEnoughIsAvailable()
    {
        await using var db = CreateContext();
        var service = new ProductCatalogService(db);
        var created = await service.CreateAsync(new ProductCreateRequest
        {
            Name = "Widget",
            Price = 100,
            StockQuantity = 10
        });

        var result = await service.ReduceStockAsync(created.Id, 3);

        Assert.Equal(7, result.StockQuantity);
    }
}

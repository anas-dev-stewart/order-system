using Microsoft.EntityFrameworkCore;
using ProductService.Api.Data;
using ProductService.Api.Dtos;
using ProductService.Api.Models;

namespace ProductService.Api.Services;

public class ProductCatalogService : IProductService
{
    private readonly ProductDbContext _db;

    public ProductCatalogService(ProductDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<ProductResponse>> GetAllAsync()
    {
        return await _db.Products
            .Select(p => ToResponse(p))
            .ToListAsync();
    }

    public async Task<ProductResponse?> GetByIdAsync(int id)
    {
        var product = await _db.Products.FindAsync(id);
        return product is null ? null : ToResponse(product);
    }

    public async Task<ProductResponse> CreateAsync(ProductCreateRequest request)
    {
        var product = new Product
        {
            Name = request.Name,
            Price = request.Price,
            StockQuantity = request.StockQuantity
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        return ToResponse(product);
    }

    public async Task<ProductResponse> UpdateAsync(int id, ProductUpdateRequest request)
    {
        var product = await _db.Products.FindAsync(id)
            ?? throw new ProductNotFoundException(id);

        product.Name = request.Name;
        product.Price = request.Price;
        product.StockQuantity = request.StockQuantity;

        await _db.SaveChangesAsync();

        return ToResponse(product);
    }

    public async Task DeleteAsync(int id)
    {
        var product = await _db.Products.FindAsync(id)
            ?? throw new ProductNotFoundException(id);

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
    }

    public async Task<ProductResponse> ReduceStockAsync(int id, int quantity)
    {
        var product = await _db.Products.FindAsync(id)
            ?? throw new ProductNotFoundException(id);

        if (product.StockQuantity < quantity)
        {
            throw new InsufficientStockException(id, product.StockQuantity, quantity);
        }

        product.StockQuantity -= quantity;
        await _db.SaveChangesAsync();

        return ToResponse(product);
    }

    private static ProductResponse ToResponse(Product p) =>
        new(p.Id, p.Name, p.Price, p.StockQuantity);
}

using Microsoft.EntityFrameworkCore;
using OrderService.Api.Clients;
using OrderService.Api.Data;
using OrderService.Api.Dtos;
using OrderService.Api.Models;

namespace OrderService.Api.Services;

public class OrderProcessingService : IOrderService
{
    private readonly OrderDbContext _db;
    private readonly IProductServiceClient _productClient;

    public OrderProcessingService(OrderDbContext db, IProductServiceClient productClient)
    {
        _db = db;
        _productClient = productClient;
    }

    public async Task<IEnumerable<OrderResponse>> GetAllAsync()
    {
        return await _db.Orders
            .Select(o => ToResponse(o))
            .ToListAsync();
    }

    public async Task<OrderResponse?> GetByIdAsync(int id)
    {
        var order = await _db.Orders.FindAsync(id);
        return order is null ? null : ToResponse(order);
    }

    public async Task<OrderResponse> PlaceOrderAsync(OrderCreateRequest request)
    {
        var product = await _productClient.GetProductAsync(request.ProductId)
            ?? throw new ProductNotFoundException(request.ProductId);

        var reducedProduct = await _productClient.ReduceStockAsync(request.ProductId, request.Quantity);

        var order = new Order
        {
            CustomerName = request.CustomerName,
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            UnitPrice = product.Price,
            TotalPrice = product.Price * request.Quantity,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        return ToResponse(order);
    }

    private static OrderResponse ToResponse(Order o) =>
        new(o.Id, o.CustomerName, o.ProductId, o.Quantity, o.UnitPrice, o.TotalPrice, o.CreatedAtUtc);
}

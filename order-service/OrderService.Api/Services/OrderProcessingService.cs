using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Api.Data;
using OrderService.Api.Dtos;
using OrderService.Api.Models;
using OrderSystem.Contracts;

namespace OrderService.Api.Services;

public class OrderProcessingService : IOrderService
{
    private static readonly Uri OrderSubmittedQueue = new("queue:order-submitted-queue");

    private readonly OrderDbContext _db;
    private readonly ISendEndpointProvider _sendEndpointProvider;
    private readonly ILogger<OrderProcessingService> _logger;

    public OrderProcessingService(OrderDbContext db, ISendEndpointProvider sendEndpointProvider, ILogger<OrderProcessingService> logger)
    {
        _db = db;
        _sendEndpointProvider = sendEndpointProvider;
        _logger = logger;
    }

    public async Task<IEnumerable<OrderResponse>> GetAllAsync()
    {
        return await _db.Orders
            .OrderByDescending(o => o.Id)
            .Select(o => ToResponse(o))
            .ToListAsync();
    }

    public async Task<OrderResponse?> GetByIdAsync(int id)
    {
        var order = await _db.Orders.FindAsync(id);
        return order is null ? null : ToResponse(order);
    }

    public async Task<OrderResponse> PlaceOrderAsync(OrderCreateRequest request, string? idempotencyKey)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _db.Orders.FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey);
            if (existing is not null)
            {
                _logger.LogInformation(
                    "Idempotency key {IdempotencyKey} already used for order {OrderId}; returning existing order instead of creating a duplicate.",
                    idempotencyKey, existing.Id);
                return ToResponse(existing);
            }
        }

        var order = new Order
        {
            CustomerName = request.CustomerName,
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            Status = OrderStatus.Submitted,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Explicit transaction because we need two SaveChangesAsync calls:
        // the first assigns the identity Order.Id (needed inside the event
        // payload), the second lets MassTransit's outbox interceptor write
        // the buffered OrderSubmitted message into the outbox table. Both
        // calls commit together, so we can never end up with an order saved
        // but no event published, or an event published for an order that
        // failed to save. A background delivery service relays committed
        // outbox rows to Azure Service Bus (see AddEntityFrameworkOutbox<OrderDbContext>
        // in Program.cs) — that final hop is "at least once", which is why
        // the worker that consumes OrderSubmitted must be idempotent too.
        await using var transaction = await _db.Database.BeginTransactionAsync();

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Send (point-to-point, to a specific queue), not Publish — Azure
        // Service Bus Basic tier has no topics. See the comment in
        // Program.cs for the full reasoning.
        var sendEndpoint = await _sendEndpointProvider.GetSendEndpoint(OrderSubmittedQueue);
        await sendEndpoint.Send(new OrderSubmitted(
            order.Id,
            order.ProductId,
            order.Quantity,
            order.CustomerName,
            order.CreatedAtUtc));

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return ToResponse(order);
    }

    private static OrderResponse ToResponse(Order o) =>
        new(o.Id, o.CustomerName, o.ProductId, o.Quantity, o.UnitPrice, o.TotalPrice, o.Status, o.RejectionReason, o.CreatedAtUtc, o.UpdatedAtUtc);
}

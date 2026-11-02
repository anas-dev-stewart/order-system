using MassTransit;
using OrderService.Api.Data;
using OrderService.Api.Models;
using OrderSystem.Contracts;

namespace OrderService.Api.Consumers;

/// <summary>
/// Updates the order's status once the worker has approved it. Setting the
/// same terminal status twice is a no-op, so redelivery of this event
/// (e.g. after a worker crash before it acks the message) is safe —
/// no extra side effects, no duplicate rows.
/// </summary>
public class OrderApprovedConsumer : IConsumer<OrderApproved>
{
    private readonly OrderDbContext _db;
    private readonly ILogger<OrderApprovedConsumer> _logger;

    public OrderApprovedConsumer(OrderDbContext db, ILogger<OrderApprovedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderApproved> context)
    {
        var message = context.Message;
        var order = await _db.Orders.FindAsync(message.OrderId);

        if (order is null)
        {
            _logger.LogWarning("Received OrderApproved for unknown order {OrderId}; ignoring.", message.OrderId);
            return;
        }

        if (order.Status == OrderStatus.Approved)
        {
            _logger.LogInformation("Order {OrderId} is already Approved; ignoring duplicate OrderApproved event.", message.OrderId);
            return;
        }

        order.Status = OrderStatus.Approved;
        order.UnitPrice = message.UnitPrice;
        order.TotalPrice = message.TotalPrice;
        order.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }
}

using MassTransit;
using OrderService.Api.Data;
using OrderService.Api.Models;
using OrderSystem.Contracts;

namespace OrderService.Api.Consumers;

public class OrderRejectedConsumer : IConsumer<OrderRejected>
{
    private readonly OrderDbContext _db;
    private readonly ILogger<OrderRejectedConsumer> _logger;

    public OrderRejectedConsumer(OrderDbContext db, ILogger<OrderRejectedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderRejected> context)
    {
        var message = context.Message;
        var order = await _db.Orders.FindAsync(message.OrderId);

        if (order is null)
        {
            _logger.LogWarning("Received OrderRejected for unknown order {OrderId}; ignoring.", message.OrderId);
            return;
        }

        if (order.Status == OrderStatus.Rejected)
        {
            _logger.LogInformation("Order {OrderId} is already Rejected; ignoring duplicate OrderRejected event.", message.OrderId);
            return;
        }

        order.Status = OrderStatus.Rejected;
        order.RejectionReason = message.Reason;
        order.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }
}

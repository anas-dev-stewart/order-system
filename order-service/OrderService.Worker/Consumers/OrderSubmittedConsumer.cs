using MassTransit;
using OrderService.Worker.Clients;
using OrderService.Worker.Data;
using OrderSystem.Contracts;

namespace OrderService.Worker.Consumers;

/// <summary>
/// Does the "real work" for an order: reserves stock with Product Service,
/// gets a confirmed price from Pricing Service, then publishes
/// OrderApproved or OrderRejected. Order Service picks that back up and
/// updates the order's status — this consumer never touches OrderDb.
/// </summary>
public class OrderSubmittedConsumer : IConsumer<OrderSubmitted>
{
    private readonly WorkerDbContext _db;
    private readonly IProductServiceClient _productClient;
    private readonly IPricingServiceClient _pricingClient;
    private readonly ILogger<OrderSubmittedConsumer> _logger;

    public OrderSubmittedConsumer(
        WorkerDbContext db,
        IProductServiceClient productClient,
        IPricingServiceClient pricingClient,
        ILogger<OrderSubmittedConsumer> logger)
    {
        _db = db;
        _productClient = productClient;
        _pricingClient = pricingClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderSubmitted> context)
    {
        // RabbitMQ (via MassTransit) guarantees "at least once" delivery:
        // if this consumer crashes, times out, or the connection drops
        // after doing its work but before acking the message, the broker
        // redelivers it. Without this check, a redelivered message would
        // call ReduceStockAsync a second time and double-debit stock for
        // the same order. MessageId is unique per publish (not per
        // redelivery attempt), so it's a safe dedupe key.
        if (await _db.ProcessedMessages.FindAsync(context.MessageId!.Value) is not null)
        {
            _logger.LogInformation(
                "MessageId {MessageId} (order {OrderId}) was already processed; skipping duplicate delivery.",
                context.MessageId, context.Message.OrderId);
            return;
        }

        // MessageId is recorded as processed only after a terminal outcome
        // (approved or rejected) is published inside ProcessAsync. If it
        // throws for a transient reason (Product/Pricing Service
        // unreachable), nothing is recorded, so MassTransit's retry policy
        // (configured in Program.cs) redelivers the message and we try
        // again from scratch.
        await ProcessAsync(context.Message, context);
    }

    private async Task ProcessAsync(OrderSubmitted message, ConsumeContext<OrderSubmitted> context)
    {
        Clients.ProductClientDto product;
        try
        {
            product = await _productClient.ReduceStockAsync(message.ProductId, message.Quantity, context.CancellationToken);
        }
        catch (ProductNotFoundException)
        {
            await RejectAsync(context, message.OrderId, $"Product {message.ProductId} does not exist.");
            return;
        }
        catch (InsufficientStockException)
        {
            await RejectAsync(context, message.OrderId, $"Product {message.ProductId} does not have enough stock.");
            return;
        }
        // ProductServiceUnavailableException is intentionally NOT caught here:
        // it's a transient infrastructure failure, so it should propagate,
        // fail the message, and let MassTransit's retry policy try again.

        PricingQuoteResponse quote;
        try
        {
            quote = await _pricingClient.GetQuoteAsync(
                new PricingQuoteRequest(message.ProductId, product.Price, message.Quantity),
                context.CancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Pricing Service unreachable/timed out after the resilience
            // handler's own retries gave up — also transient, let the
            // message-level retry handle it rather than rejecting the order.
            throw;
        }

        await ApproveAsync(context, message.OrderId, quote.FinalUnitPrice, quote.FinalTotalPrice);
    }

    private async Task ApproveAsync(ConsumeContext<OrderSubmitted> context, int orderId, decimal unitPrice, decimal totalPrice)
    {
        await context.Publish(new OrderApproved(orderId, unitPrice, totalPrice, DateTime.UtcNow));
        await MarkProcessedAsync(context.MessageId!.Value);
        _logger.LogInformation("Order {OrderId} approved at {UnitPrice}/unit.", orderId, unitPrice);
    }

    private async Task RejectAsync(ConsumeContext<OrderSubmitted> context, int orderId, string reason)
    {
        await context.Publish(new OrderRejected(orderId, reason, DateTime.UtcNow));
        await MarkProcessedAsync(context.MessageId!.Value);
        _logger.LogInformation("Order {OrderId} rejected: {Reason}", orderId, reason);
    }

    private async Task MarkProcessedAsync(Guid messageId)
    {
        _db.ProcessedMessages.Add(new ProcessedMessage { MessageId = messageId, ProcessedAtUtc = DateTime.UtcNow });
        await _db.SaveChangesAsync();
    }
}

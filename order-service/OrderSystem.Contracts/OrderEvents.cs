namespace OrderSystem.Contracts;

/// <summary>
/// Published by Order Service when an order has been accepted and saved,
/// before any processing (stock check, pricing) has happened.
/// </summary>
public record OrderSubmitted(
    int OrderId,
    int ProductId,
    int Quantity,
    string CustomerName,
    DateTime SubmittedAtUtc);

/// <summary>
/// Published by the Order Worker once stock has been reserved and a final
/// price has been confirmed with the Pricing Service.
/// </summary>
public record OrderApproved(
    int OrderId,
    decimal UnitPrice,
    decimal TotalPrice,
    DateTime ApprovedAtUtc);

/// <summary>
/// Published by the Order Worker when stock, pricing, or any other
/// validation step fails for an order.
/// </summary>
public record OrderRejected(
    int OrderId,
    string Reason,
    DateTime RejectedAtUtc);

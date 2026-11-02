using OrderService.Api.Dtos;

namespace OrderService.Api.Services;

public interface IOrderService
{
    Task<IEnumerable<OrderResponse>> GetAllAsync();
    Task<OrderResponse?> GetByIdAsync(int id);

    /// <summary>
    /// Saves the order as "Submitted" and publishes an OrderSubmitted event.
    /// Does NOT wait for stock validation or pricing — that happens
    /// asynchronously in the Order Worker. If <paramref name="idempotencyKey"/>
    /// matches an order already saved, the existing order is returned instead
    /// of creating a duplicate.
    /// </summary>
    Task<OrderResponse> PlaceOrderAsync(OrderCreateRequest request, string? idempotencyKey);
}

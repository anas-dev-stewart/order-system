using OrderService.Api.Dtos;

namespace OrderService.Api.Services;

public interface IOrderService
{
    Task<IEnumerable<OrderResponse>> GetAllAsync();
    Task<OrderResponse?> GetByIdAsync(int id);
    Task<OrderResponse> PlaceOrderAsync(OrderCreateRequest request);
}

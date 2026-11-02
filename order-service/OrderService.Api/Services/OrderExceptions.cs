namespace OrderService.Api.Services;

public class OrderNotFoundException : Exception
{
    public OrderNotFoundException(int id) : base($"Order with id {id} was not found.")
    {
    }
}

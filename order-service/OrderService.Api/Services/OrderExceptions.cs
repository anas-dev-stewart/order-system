namespace OrderService.Api.Services;

public class ProductNotFoundException : Exception
{
    public ProductNotFoundException(int productId) : base($"Product with id {productId} was not found.")
    {
    }
}

public class InsufficientStockException : Exception
{
    public InsufficientStockException(int productId) : base($"Product {productId} does not have enough stock for this order.")
    {
    }
}

public class ProductServiceUnavailableException : Exception
{
    public ProductServiceUnavailableException()
        : base("The Product Service is unavailable. Please try again later.")
    {
    }

    public ProductServiceUnavailableException(string message) : base(message)
    {
    }
}

public class OrderNotFoundException : Exception
{
    public OrderNotFoundException(int id) : base($"Order with id {id} was not found.")
    {
    }
}

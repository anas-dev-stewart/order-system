namespace ProductService.Api.Services;

public class ProductNotFoundException : Exception
{
    public ProductNotFoundException(int id) : base($"Product with id {id} was not found.")
    {
    }
}

public class InsufficientStockException : Exception
{
    public InsufficientStockException(int id, int available, int requested)
        : base($"Product {id} has only {available} in stock; requested {requested}.")
    {
    }
}

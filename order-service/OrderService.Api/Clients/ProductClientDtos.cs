namespace OrderService.Api.Clients;

public record ProductClientDto(int Id, string Name, decimal Price, int StockQuantity);

public class ReduceStockClientRequest
{
    public int Quantity { get; set; }
}

public class ProductClientErrorResponse
{
    public string Message { get; set; } = string.Empty;
}

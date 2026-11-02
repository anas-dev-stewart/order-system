namespace OrderService.Worker.Clients;

public record ProductClientDto(int Id, string Name, decimal Price, int StockQuantity);

public class ReduceStockClientRequest
{
    public int Quantity { get; set; }
}

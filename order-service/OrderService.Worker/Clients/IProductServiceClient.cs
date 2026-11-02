namespace OrderService.Worker.Clients;

public interface IProductServiceClient
{
    /// <summary>Returns null when the product does not exist (404 from Product Service).</summary>
    Task<ProductClientDto?> GetProductAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reduces stock on the Product Service.
    /// Throws ProductNotFoundException (404), InsufficientStockException (409),
    /// or ProductServiceUnavailableException (connection failure/5xx) as appropriate.
    /// </summary>
    Task<ProductClientDto> ReduceStockAsync(int id, int quantity, CancellationToken cancellationToken = default);
}

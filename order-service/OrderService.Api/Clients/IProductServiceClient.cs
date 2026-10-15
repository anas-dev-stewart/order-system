namespace OrderService.Api.Clients;

public interface IProductServiceClient
{
    /// <summary>Returns null when the product does not exist (404 from Product Service).</summary>
    Task<ProductClientDto?> GetProductAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reduces stock on the Product Service.
    /// Throws Services.ProductNotFoundException (404), Services.InsufficientStockException (409),
    /// or Services.ProductServiceUnavailableException (connection failure) as appropriate.
    /// </summary>
    Task<ProductClientDto> ReduceStockAsync(int id, int quantity, CancellationToken cancellationToken = default);
}

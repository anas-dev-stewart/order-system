using System.Net;
using System.Net.Http.Json;
using OrderService.Api.Services;

namespace OrderService.Api.Clients;

public class ProductServiceClient : IProductServiceClient
{
    private readonly HttpClient _httpClient;

    public ProductServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ProductClientDto?> GetProductAsync(int id, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync($"/api/products/{id}", cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new ProductServiceUnavailableException();
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductClientDto>(cancellationToken: cancellationToken);
    }

    public async Task<ProductClientDto> ReduceStockAsync(int id, int quantity, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(
                $"/api/products/{id}/reduce-stock",
                new ReduceStockClientRequest { Quantity = quantity },
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new ProductServiceUnavailableException();
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ProductNotFoundException(id);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new InsufficientStockException(id);
        }

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ProductClientDto>(cancellationToken: cancellationToken);
        return result ?? throw new ProductServiceUnavailableException("Product Service returned an empty response.");
    }
}

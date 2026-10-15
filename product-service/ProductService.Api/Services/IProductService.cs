using ProductService.Api.Dtos;

namespace ProductService.Api.Services;

public interface IProductService
{
    Task<IEnumerable<ProductResponse>> GetAllAsync();
    Task<ProductResponse?> GetByIdAsync(int id);
    Task<ProductResponse> CreateAsync(ProductCreateRequest request);
    Task<ProductResponse> UpdateAsync(int id, ProductUpdateRequest request);
    Task DeleteAsync(int id);
    Task<ProductResponse> ReduceStockAsync(int id, int quantity);
}

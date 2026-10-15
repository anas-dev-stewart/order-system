using System.ComponentModel.DataAnnotations;

namespace ProductService.Api.Dtos;

public record ProductResponse(int Id, string Name, decimal Price, int StockQuantity);

public class ProductCreateRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0.")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "StockQuantity must be 0 or more.")]
    public int StockQuantity { get; set; }
}

public class ProductUpdateRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0.")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "StockQuantity must be 0 or more.")]
    public int StockQuantity { get; set; }
}

public class ReduceStockRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than 0.")]
    public int Quantity { get; set; }
}

public class ErrorResponse
{
    public string Message { get; set; } = string.Empty;

    public ErrorResponse(string message) => Message = message;
}

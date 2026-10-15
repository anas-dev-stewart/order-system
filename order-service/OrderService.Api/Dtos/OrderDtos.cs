using System.ComponentModel.DataAnnotations;

namespace OrderService.Api.Dtos;

public record OrderResponse(
    int Id,
    string CustomerName,
    int ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    DateTime CreatedAtUtc);

public class OrderCreateRequest
{
    [Required]
    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    public int ProductId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than 0.")]
    public int Quantity { get; set; }
}

public class ErrorResponse
{
    public string Message { get; set; } = string.Empty;

    public ErrorResponse(string message) => Message = message;
}

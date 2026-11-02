using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderService.Api.Models;

public enum OrderStatus
{
    Submitted,
    Approved,
    Rejected
}

public class Order
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalPrice { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Submitted;

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    /// <summary>
    /// Client-supplied key (e.g. from an "Idempotency-Key" header) used to
    /// detect and short-circuit duplicate submissions of the same logical
    /// order. Null when the client didn't send one.
    /// </summary>
    [MaxLength(100)]
    public string? IdempotencyKey { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}

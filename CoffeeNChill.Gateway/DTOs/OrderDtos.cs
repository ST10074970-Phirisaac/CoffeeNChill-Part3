using System.ComponentModel.DataAnnotations;

namespace CoffeeNChill.Gateway.DTOs;

public sealed class QueueOrderRequestDto
{
    [Required]
    public string OrderId { get; set; } = string.Empty;

    [Required]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<string> SelectedItemSKUs { get; set; } = new();

    [Range(0, 100000)]
    public double TotalPrice { get; set; }

    public DateTimeOffset? OrderTimestamp { get; set; }
}

public sealed class UpdateOrderStatusDto
{
    [Required]
    public string OrderDate { get; set; } = string.Empty;

    [Required]
    public string OrderId { get; set; } = string.Empty;

    [Required]
    public string Status { get; set; } = string.Empty;
}

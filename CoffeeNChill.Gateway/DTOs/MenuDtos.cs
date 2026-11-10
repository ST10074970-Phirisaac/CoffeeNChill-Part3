using System.ComponentModel.DataAnnotations;

namespace CoffeeNChill.Gateway.DTOs;

public sealed class MenuItemDto
{
    [Required]
    public string PartitionKey { get; set; } = string.Empty;

    [Required]
    public string RowKey { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Range(0, 10000)]
    public double Price { get; set; }

    public bool IsAvailable { get; set; } = true;
}

public sealed class UpdateMenuItemDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Range(0, 10000)]
    public double Price { get; set; }

    public bool IsAvailable { get; set; } = true;
}

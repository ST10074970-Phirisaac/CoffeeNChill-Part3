using System.ComponentModel.DataAnnotations;

namespace CoffeeNChill.Gateway.DTOs;

/// <summary>
/// Information required to register a new customer.
/// </summary>
public class RegisterRequestDto
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [RegularExpression("^(Customer|Staff|Admin)$", ErrorMessage = "Role must be 'Customer', 'Staff', or 'Admin'.")]
    public string Role { get; set; } = "Customer";
}

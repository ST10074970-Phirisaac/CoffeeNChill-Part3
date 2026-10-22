
using CoffeeNChill.Gateway.Models;

namespace CoffeeNChill.Gateway.Services;

/// <summary>
/// Defines operations for managing CoffeeNChill users.
/// </summary>
public interface IUserService
{
    Task<UserEntity?> GetByEmailAsync(string email);

    Task<bool> EmailExistsAsync(string email);

    Task CreateUserAsync(UserEntity user);
}

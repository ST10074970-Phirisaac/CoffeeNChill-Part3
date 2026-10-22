
using Azure;
using CoffeeNChill.Gateway.DTOs;
using CoffeeNChill.Gateway.Models;
using CoffeeNChill.Gateway.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.Gateway.Controllers;

/// <summary>
/// Handles customer registration and authentication.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly PasswordHasher<UserEntity> _passwordHasher = new();

    public AuthController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Registers a new CoffeeNChill customer.
    /// </summary>
    /// <response code="201">Customer registered successfully.</response>
    /// <response code="400">Invalid registration details.</response>
    /// <response code="409">Email address is already registered.</response>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequestDto request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _userService.EmailExistsAsync(email))
        {
            return Conflict(new
            {
                Message = "Email address is already registered."
            });
        }

        var user = new UserEntity
        {
            PartitionKey = "Customer",
            RowKey = email,
            FullName = request.FullName.Trim(),
            CreatedDate = DateTimeOffset.UtcNow
        };

        user.PasswordHash =
            _passwordHasher.HashPassword(user, request.Password);

        try
        {
            await _userService.CreateUserAsync(user);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            return Conflict(new
            {
                Message = "Email address is already registered."
            });
        }

        return StatusCode(StatusCodes.Status201Created, new
        {
            Message = "Customer registered successfully.",
            FullName = user.FullName,
            Email = user.RowKey,
            Role = user.PartitionKey
        });
    }
}


using Azure;
using System.Security.Claims;
using CoffeeNChill.Gateway.DTOs;
using CoffeeNChill.Gateway.Models;
using CoffeeNChill.Gateway.Services;
using Microsoft.AspNetCore.Authorization;
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
    private readonly JwtTokenService _jwtTokenService;
    private readonly PasswordHasher<UserEntity> _passwordHasher = new();

    public AuthController(
        IUserService userService,
        JwtTokenService jwtTokenService)
    {
        _userService = userService;
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>
    /// Registers a new CoffeeNChill customer.
    /// </summary>
    /// <response code="201">Customer registered successfully.</response>
    /// <response code="400">Invalid registration details.</response>
    /// <response code="409">Email address is already registered.</response>
    [AllowAnonymous]
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

    /// <summary>
    /// Authenticates a user and returns a signed JWT token.
    /// </summary>
    /// <response code="200">Login successful.</response>
    /// <response code="400">Invalid login details.</response>
    /// <response code="401">Invalid email or password.</response>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequestDto request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _userService.GetByEmailAsync(email);

        if (user == null)
        {
            return Unauthorized(new
            {
                Message = "Invalid email or password."
            });
        }

        var passwordResult =
            _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password);

        if (passwordResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new
            {
                Message = "Invalid email or password."
            });
        }

        var token = _jwtTokenService.GenerateToken(user);

        return Ok(new
        {
            Message = "Login successful.",
            Token = token,
            TokenType = "Bearer",
            ExpiresIn = 7200,
            FullName = user.FullName,
            Email = user.RowKey,
            Role = user.PartitionKey
        });
    }

    /// <summary>
    /// Returns information about the authenticated user.
    /// Requires a valid JWT bearer token.
    /// </summary>
    /// <response code="200">User authenticated successfully.</response>
    /// <response code="401">Missing or invalid authentication token.</response>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        return Ok(new
        {
            Message = "Authenticated successfully.",
            FullName = User.FindFirst(ClaimTypes.Name)?.Value,
            Email = User.FindFirst(ClaimTypes.Email)?.Value,
            Role = User.FindFirst(ClaimTypes.Role)?.Value
        });
    }
}

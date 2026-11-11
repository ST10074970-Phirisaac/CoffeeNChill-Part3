
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.Gateway.Controllers;

/// <summary>
/// Checks the health of the CoffeeNChill API Gateway.
/// </summary>
[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Returns the current API Gateway status.
    /// </summary>
    /// <response code="200">The API Gateway is running.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            Status = "Healthy",
            Service = "CoffeeNChill API Gateway",
            Timestamp = DateTime.UtcNow
        });
    }
}

using System.Net;
using System.Text;
using System.Text.Json;
using CoffeeNChill.Gateway.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.Gateway.Controllers;

[ApiController]
[Route("api/menu")]
public class MenuController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public MenuController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    // Public: view all menu items
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<MenuItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status502BadGateway)]
    public Task<IActionResult> GetAll()
    {
        return ForwardAsync(HttpMethod.Get, "api/menu");
    }

    // Public: view menu items by category
    [HttpGet("category/{category}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<MenuItemDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetByCategory(string category)
    {
        return ForwardAsync(
            HttpMethod.Get,
            $"api/menu/category/{Uri.EscapeDataString(category)}");
    }

    // Staff/Admin: create menu items
    [HttpPost]
    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(typeof(MenuItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> Create([FromBody] MenuItemDto item)
    {
        return ForwardAsync(HttpMethod.Post, "api/menu", item);
    }

    // Staff/Admin: update menu items
    [HttpPut("{category}/{id}")]
    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(typeof(MenuItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Update(
        string category,
        string id,
        [FromBody] UpdateMenuItemDto item)
    {
        return ForwardAsync(
            HttpMethod.Put,
            $"api/menu/{Uri.EscapeDataString(category)}/{Uri.EscapeDataString(id)}",
            item);
    }

    // Staff/Admin: delete menu items
    [HttpDelete("{category}/{id}")]
    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Delete(string category, string id)
    {
        return ForwardAsync(
            HttpMethod.Delete,
            $"api/menu/{Uri.EscapeDataString(category)}/{Uri.EscapeDataString(id)}");
    }

    private async Task<IActionResult> ForwardAsync(
        HttpMethod method,
        string path,
        object? body = null)
    {
        var client = _httpClientFactory.CreateClient("AzureFunctions");
        using var request = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");
        }

        try
        {
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                HttpContext.RequestAborted);

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return NoContent();
            }

            var content = await response.Content.ReadAsStringAsync(
                HttpContext.RequestAborted);

            var contentType =
                response.Content.Headers.ContentType?.ToString()
                ?? "application/json";

            return new ContentResult
            {
                Content = content,
                ContentType = contentType,
                StatusCode = (int)response.StatusCode
            };
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new ErrorResponseDto
            {
                Message = "Azure Functions service is unavailable."
            });
        }
        catch (OperationCanceledException)
            when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(504, new ErrorResponseDto
            {
                Message = "Azure Functions service timed out."
            });
        }
    }
}

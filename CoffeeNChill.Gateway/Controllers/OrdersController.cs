using System.Net;
using System.Net.Http.Json;
using CoffeeNChill.Gateway.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.Gateway.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public OrdersController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("queue")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> QueueOrder([FromBody] QueueOrderRequestDto order)
    {
        if (string.IsNullOrWhiteSpace(order.OrderId) ||
            string.IsNullOrWhiteSpace(order.CustomerName) ||
            order.SelectedItemSKUs is null ||
            order.SelectedItemSKUs.Count == 0 ||
            order.TotalPrice < 0)
        {
            return BadRequest(new ErrorResponseDto
            {
                Message = "Valid order ID, customer name, items and total price are required."
            });
        }

        var client = _httpClientFactory.CreateClient("AzureFunctions");

        try
        {
            using var response = await client.PostAsJsonAsync(
                "api/orders/queue",
                order,
                HttpContext.RequestAborted);

            var content = await response.Content.ReadAsStringAsync(
                HttpContext.RequestAborted);

            return new ContentResult
            {
                Content = content,
                ContentType = response.Content.Headers.ContentType?.ToString()
                              ?? "application/json",
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

    [HttpGet("{date}/{orderId}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderStatus(string date, string orderId)
    {
        var client = _httpClientFactory.CreateClient("AzureFunctions");
        var path = $"api/orders/{Uri.EscapeDataString(date)}/{Uri.EscapeDataString(orderId)}";

        try
        {
            using var response = await client.GetAsync(path, HttpContext.RequestAborted);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return NotFound(new ErrorResponseDto
                {
                    Message = "Order not found."
                });
            }

            var content = await response.Content.ReadAsStringAsync(
                HttpContext.RequestAborted);

            return new ContentResult
            {
                Content = content,
                ContentType = response.Content.Headers.ContentType?.ToString()
                              ?? "application/json",
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

    [HttpPut("status")]
    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateOrderStatus([FromBody] UpdateOrderStatusDto request)
    {
        var client = _httpClientFactory.CreateClient("AzureFunctions");

        try
        {
            using var response = await client.PutAsJsonAsync(
                "api/orders/status",
                request,
                HttpContext.RequestAborted);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return NotFound(new ErrorResponseDto
                {
                    Message = "Order not found."
                });
            }

            var content = await response.Content.ReadAsStringAsync(
                HttpContext.RequestAborted);

            return new ContentResult
            {
                Content = content,
                ContentType = response.Content.Headers.ContentType?.ToString()
                              ?? "application/json",
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

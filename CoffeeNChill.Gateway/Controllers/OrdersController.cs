
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.Gateway.Controllers;

public sealed class QueueOrderRequest
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public List<string> SelectedItemSKUs { get; set; } = new();
    public double TotalPrice { get; set; }
    public DateTimeOffset? OrderTimestamp { get; set; }
}

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public OrdersController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("queue")]
    [Authorize(Roles = "Customer,Staff,Admin")]
    public async Task<IActionResult> QueueOrder(
        [FromBody] QueueOrderRequest order)
    {
        if (string.IsNullOrWhiteSpace(order.OrderId) ||
            string.IsNullOrWhiteSpace(order.CustomerName) ||
            order.SelectedItemSKUs is null ||
            order.SelectedItemSKUs.Count == 0 ||
            order.TotalPrice < 0)
        {
            return BadRequest(new
            {
                message = "Valid order ID, customer name, items and total price are required."
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
            return StatusCode(502, new
            {
                message = "Azure Functions service is unavailable."
            });
        }
        catch (OperationCanceledException)
            when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(504, new
            {
                message = "Azure Functions service timed out."
            });
        }
    }

    [HttpGet("{date}/{orderId}")]
    [Authorize(Roles = "Customer,Staff,Admin")]
    public async Task<IActionResult> GetOrderStatus(
        string date,
        string orderId)
    {
        var client = _httpClientFactory.CreateClient("AzureFunctions");

        var path = $"api/orders/{Uri.EscapeDataString(date)}/" +
                   $"{Uri.EscapeDataString(orderId)}";

        try
        {
            using var response = await client.GetAsync(
                path,
                HttpContext.RequestAborted);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return NotFound(new
                {
                    message = "Order not found."
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
            return StatusCode(502, new
            {
                message = "Azure Functions service is unavailable."
            });
        }
        catch (OperationCanceledException)
            when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return StatusCode(504, new
            {
                message = "Azure Functions service timed out."
            });
        }
    }
}

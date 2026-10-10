using System.Net;
using System.Text.Json;
using CoffeeNChill.Functions.Models;
using CoffeeNChill.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions.Functions;

public class OrderFunctions
{
    private readonly OrderStorageService _storage;
    private readonly ILogger<OrderFunctions> _logger;
    public OrderFunctions(OrderStorageService storage, ILogger<OrderFunctions> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    [Function("QueueOrder")]
    public async Task<HttpResponseData> QueueOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders/queue")] HttpRequestData req)
    {
        Order? order;
        try { order = await JsonSerializer.DeserializeAsync<Order>(req.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException) { return req.CreateResponse(HttpStatusCode.BadRequest); }
        if (order is null || string.IsNullOrWhiteSpace(order.OrderId) ||
            string.IsNullOrWhiteSpace(order.CustomerName) || order.SelectedItemSKUs is null ||
            order.SelectedItemSKUs.Count == 0 || order.TotalPrice < 0)
            return req.CreateResponse(HttpStatusCode.BadRequest);
        if (order.OrderTimestamp == default) order.OrderTimestamp = DateTimeOffset.UtcNow;
        await _storage.EnqueueAsync(order);
        var response = req.CreateResponse(HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new { order.OrderId, status = "Queued", message = "Order accepted for asynchronous processing" });
        return response;
    }

    [Function("ProcessOrderQueue")]
    public async Task ProcessOrderQueue(
        [QueueTrigger("order-processing-queue", Connection = "AzureWebJobsStorage")] string message)
    {
        var order = JsonSerializer.Deserialize<Order>(message)
            ?? throw new InvalidOperationException("Queue message does not contain an order.");
        foreach (var status in new[] { "Received", "Preparing", "Ready", "Collected" })
        {
            await _storage.SaveStatusAsync(order, status);
            _logger.LogInformation("Order {OrderId}: {Status}", order.OrderId, status);
            // Each transition is persisted; no artificial delay is needed for the demonstration.
        }
    }

    [Function("GetOrderStatus")]
    public async Task<HttpResponseData> GetOrderStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/{date}/{orderId}")] HttpRequestData req,
        string date, string orderId)
    {
        var entity = await _storage.GetOrderAsync(date, orderId);
        if (entity is null) return req.CreateResponse(HttpStatusCode.NotFound);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            OrderId = entity.RowKey, OrderDate = entity.PartitionKey,
            Status = entity.GetString("Status"), CustomerName = entity.GetString("CustomerName"),
            SelectedItemSKUs = JsonSerializer.Deserialize<List<string>>(entity.GetString("SelectedItemSKUs") ?? "[]"),
            TotalPrice = entity.GetDouble("TotalPrice"), UpdatedAt = entity.GetDateTimeOffset("UpdatedAt")
        });
        return response;
    }
}

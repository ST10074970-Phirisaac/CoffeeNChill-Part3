
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

    public OrderFunctions(
        OrderStorageService storage,
        ILogger<OrderFunctions> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    // HTTP endpoint: Submit an order to Azure Queue Storage
    [Function("QueueOrder")]
    public async Task<HttpResponseData> QueueOrder(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "orders/queue")] HttpRequestData req)
    {
        Order? order;

        try
        {
            order = await JsonSerializer.DeserializeAsync<Order>(
                req.Body,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Order submission rejected: invalid JSON.");

            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        if (order is null ||
            string.IsNullOrWhiteSpace(order.OrderId) ||
            string.IsNullOrWhiteSpace(order.CustomerName) ||
            order.SelectedItemSKUs is null ||
            order.SelectedItemSKUs.Count == 0 ||
            order.TotalPrice < 0)
        {
            _logger.LogWarning(
                "Order submission rejected: missing or invalid order fields.");

            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        if (order.OrderTimestamp == default)
        {
            order.OrderTimestamp = DateTimeOffset.UtcNow;
        }

        await _storage.EnqueueAsync(order);

        _logger.LogInformation(
            "Order {OrderId} successfully added to the processing queue.",
            order.OrderId);

        var response = req.CreateResponse(HttpStatusCode.Accepted);

        await response.WriteAsJsonAsync(new
        {
            order.OrderId,
            status = "Queued",
            message = "Order accepted for asynchronous processing"
        });

        return response;
    }

    // Queue trigger: Process orders asynchronously
    [Function("ProcessOrderQueue")]
    public async Task ProcessOrderQueue(
        [QueueTrigger(
            "order-processing-queue",
            Connection = "AzureWebJobsStorage")] string message)
    {
        string orderId = "Unknown";

        try
        {
            var order = JsonSerializer.Deserialize<Order>(message)
                ?? throw new InvalidOperationException(
                    "Queue message does not contain an order.");

            if (string.IsNullOrWhiteSpace(order.OrderId) ||
                string.IsNullOrWhiteSpace(order.CustomerName) ||
                order.SelectedItemSKUs is null ||
                order.SelectedItemSKUs.Count == 0 ||
                order.TotalPrice < 0)
            {
                throw new InvalidOperationException(
                    "Queue message contains invalid order data.");
            }

            orderId = order.OrderId;

            _logger.LogInformation(
                "Starting queue processing for order {OrderId}.",
                orderId);

            foreach (var status in new[]
            {
                "Received",
                "Preparing",
                "Ready",
                "Collected"
            })
            {
                await _storage.SaveStatusAsync(order, status);

                _logger.LogInformation(
                    "Order {OrderId}: {Status}",
                    orderId,
                    status);
            }

            _logger.LogInformation(
                "Order {OrderId} processed successfully.",
                orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process queued order {OrderId}. " +
                "Azure Functions will handle retries and poison-queue " +
                "placement according to the configured queue settings.",
                orderId);

            throw;
        }
    }

    // HTTP endpoint: Retrieve the latest order status
    [Function("GetOrderStatus")]
    public async Task<HttpResponseData> GetOrderStatus(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "orders/{date}/{orderId}")] HttpRequestData req,
        string date,
        string orderId)
    {
        var entity = await _storage.GetOrderAsync(date, orderId);

        if (entity is null)
        {
            _logger.LogWarning(
                "Order {OrderId} was not found for date {OrderDate}.",
                orderId,
                date);

            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);

        await response.WriteAsJsonAsync(new
        {
            OrderId = entity.RowKey,
            OrderDate = entity.PartitionKey,
            Status = entity.GetString("Status"),
            CustomerName = entity.GetString("CustomerName"),
            SelectedItemSKUs =
                JsonSerializer.Deserialize<List<string>>(
                    entity.GetString("SelectedItemSKUs") ?? "[]"),
            TotalPrice = entity.GetDouble("TotalPrice"),
            UpdatedAt = entity.GetDateTimeOffset("UpdatedAt")
        });

        return response;
    }

    // HTTP endpoint: Update an existing order's status
    [Function("UpdateOrderStatus")]
    public async Task<HttpResponseData> UpdateOrderStatus(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "put",
            Route = "orders/status")] HttpRequestData req)
    {
        UpdateOrderStatusRequest? request;

        try
        {
            request =
                await JsonSerializer.DeserializeAsync<UpdateOrderStatusRequest>(
                    req.Body,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Invalid order status update JSON.");

            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        if (request is null ||
            string.IsNullOrWhiteSpace(request.Date) ||
            string.IsNullOrWhiteSpace(request.OrderId) ||
            string.IsNullOrWhiteSpace(request.Status))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        var updated = await _storage.UpdateStatusAsync(
            request.Date,
            request.OrderId,
            request.Status);

        if (!updated)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        _logger.LogInformation(
            "Order {OrderId} status updated to {Status}.",
            request.OrderId,
            request.Status);

        var response = req.CreateResponse(HttpStatusCode.OK);

        await response.WriteAsJsonAsync(new
        {
            request.OrderId,
            request.Date,
            request.Status,
            message = "Order status updated successfully."
        });

        return response;
    }
}

// Request model for updating an order's status
public class UpdateOrderStatusRequest
{
    public string Date { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

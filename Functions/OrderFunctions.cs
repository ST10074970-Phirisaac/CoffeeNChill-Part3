using System.Net;
using System.Text.Json;
using CoffeeNChill.Functions.Models;
using CoffeeNChill.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions.Functions;

public abstract class OrderFunctions(
    OrderStorageService storage,
    ILogger<OrderFunctions> logger)
{
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
            logger.LogWarning(
                ex,
                "Order submission rejected: invalid JSON.");

            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        if (order is null ||
            string.IsNullOrWhiteSpace(order.OrderId) ||
            string.IsNullOrWhiteSpace(order.CustomerName) ||
            order.SelectedItemSkUs.Count == 0 ||
            order.TotalPrice < 0)
        {
            logger.LogWarning(
                "Order submission rejected: missing or invalid order fields.");

            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        if (order.OrderTimestamp == default)
        {
            order.OrderTimestamp = DateTimeOffset.UtcNow;
        }

        try
{
    await storage.EnqueueAsync(order);
}
catch (Exception ex)
{
    logger.LogError(
        ex,
        "Failed to enqueue order {OrderId}. Returning HTTP 500 to client.",
        order.OrderId);

    return req.CreateResponse(HttpStatusCode.InternalServerError);
}

logger.LogInformation(
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
                order.SelectedItemSkUs is null ||
                order.SelectedItemSkUs.Count == 0 ||
                order.TotalPrice < 0)
            {
                throw new InvalidOperationException(
                    "Queue message contains invalid order data.");
            }

            orderId = order.OrderId;
            logger.LogInformation(
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
                await storage.SaveStatusAsync(order, status);
                logger.LogInformation(
                    "Order {OrderId}: {Status}",
                    orderId,
                    status);
            }

            logger.LogInformation(
                "Order {OrderId} processed successfully.",
                orderId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to process queued order {OrderId}. " +
                "Azure Functions will handle retries and poison-queue " +
                "placement according to the configured queue settings.",
                orderId);

            // Rethrow so Azure Functions detects the failure.
            // Failed messages are retried and can eventually be
            // moved to order-processing-queue-poison.
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
        var entity = await storage.GetOrderAsync(date, orderId);

        if (entity is null)
        {
            logger.LogWarning(
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
}

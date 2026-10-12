using Azure;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using CoffeeNChill.Functions.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CoffeeNChill.Functions.Services;

public class OrderStorageService
{
    private readonly QueueClient _queue;
    private readonly TableClient _orders;
    private readonly ILogger<OrderStorageService> _logger;

    public OrderStorageService(ILogger<OrderStorageService> logger)
    {
        _logger = logger;

        var connection = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

        if (string.IsNullOrWhiteSpace(connection))
        {
            var ex = new InvalidOperationException(
                "AzureWebJobsStorage is missing or empty. " +
                "Set it in local.settings.json or as an environment variable.");

            _logger.LogCritical(ex,
                "OrderStorageService failed to initialise because AzureWebJobsStorage is not configured.");

            throw ex;
        }

        _logger.LogInformation(
            "Initialising OrderStorageService. Queue={QueueName}, Table={TableName}.",
            "order-processing-queue",
            "Orders");

        _queue = new QueueClient(
            connection,
            "order-processing-queue",
            new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });

        _orders = new TableClient(connection, "Orders");
    }

    public async Task EnqueueAsync(Order order)
{
    _logger.LogInformation(
        "Enqueueing order {OrderId} for customer {CustomerName}.",
        order.OrderId,
        order.CustomerName);

    try
    {
        await _queue.CreateIfNotExistsAsync();
        await _queue.SendMessageAsync(JsonSerializer.Serialize(order));

        _logger.LogInformation(
            "Order {OrderId} enqueued successfully.",
            order.OrderId);
    }
    catch (RequestFailedException ex)
    {
        _logger.LogError(
            ex,
            "Azure Queue Storage request failed while enqueueing order {OrderId}. " +
            "HttpStatus={HttpStatus}, ErrorCode={ErrorCode}.",
            order.OrderId,
            ex.Status,
            ex.ErrorCode);

        throw;
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Unexpected error while enqueueing order {OrderId}.",
            order.OrderId);

        throw;
    }
}

    public async Task SaveStatusAsync(Order order, string status)
{
    _logger.LogInformation(
        "Saving status {OrderStatus} for order {OrderId}.",
        status,
        order.OrderId);

    try
    {
        await _orders.CreateIfNotExistsAsync();

        var entity = new TableEntity(
            order.OrderTimestamp.UtcDateTime.ToString("yyyy-MM-dd"),
            order.OrderId)
        {
            ["CustomerName"]     = order.CustomerName,
            ["SelectedItemSKUs"] = JsonSerializer.Serialize(order.SelectedItemSkUs),
            ["TotalPrice"]       = order.TotalPrice,
            ["OrderTimestamp"]   = order.OrderTimestamp,
            ["Status"]           = status,
            ["UpdatedAt"]        = DateTimeOffset.UtcNow
        };

        await _orders.UpsertEntityAsync(entity, TableUpdateMode.Replace);

        _logger.LogInformation(
            "Status {OrderStatus} persisted for order {OrderId}.",
            status,
            order.OrderId);
    }
    catch (RequestFailedException ex)
    {
        _logger.LogError(
            ex,
            "Azure Table Storage request failed while saving status {OrderStatus} " +
            "for order {OrderId}. HttpStatus={HttpStatus}, ErrorCode={ErrorCode}.",
            status,
            order.OrderId,
            ex.Status,
            ex.ErrorCode);

        throw;
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Unexpected error while saving status {OrderStatus} for order {OrderId}.",
            status,
            order.OrderId);

        throw;
    }
}

    public async Task<TableEntity?> GetOrderAsync(string date, string orderId)
    {
        await _orders.CreateIfNotExistsAsync();
        var result = await _orders.GetEntityIfExistsAsync<TableEntity>(date, orderId);
        return result.HasValue ? result.Value : null;
    }
}
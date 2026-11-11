using Azure;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using CoffeeNChill.Functions.Models;
using System.Text.Json;

namespace CoffeeNChill.Functions.Services;

public class OrderStorageService
{
    private readonly QueueClient _queue;
    private readonly TableClient _orders;

    public OrderStorageService()
    {
        var connection = Environment.GetEnvironmentVariable("AzureWebJobsStorage")
            ?? throw new InvalidOperationException("AzureWebJobsStorage is missing.");

        _queue = new QueueClient(connection, "order-processing-queue", new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
        _orders = new TableClient(connection, "Orders");
    }

    public async Task EnqueueAsync(Order order)
    {
        await _queue.CreateIfNotExistsAsync();
        await _queue.SendMessageAsync(JsonSerializer.Serialize(order));
    }

    public async Task SaveStatusAsync(Order order, string status)
    {
        await _orders.CreateIfNotExistsAsync();
        var entity = new TableEntity(order.OrderTimestamp.UtcDateTime.ToString("yyyy-MM-dd"), order.OrderId)
        {
            ["CustomerName"] = order.CustomerName,
            ["SelectedItemSKUs"] = JsonSerializer.Serialize(order.SelectedItemSKUs),
            ["TotalPrice"] = order.TotalPrice,
            ["OrderTimestamp"] = order.OrderTimestamp,
            ["Status"] = status,
            ["UpdatedAt"] = DateTimeOffset.UtcNow
        };
        await _orders.UpsertEntityAsync(entity, TableUpdateMode.Replace);
    }

    public async Task<TableEntity?> GetOrderAsync(string date, string orderId)
    {
        await _orders.CreateIfNotExistsAsync();
        var result = await _orders.GetEntityIfExistsAsync<TableEntity>(date, orderId);
        return result.HasValue ? result.Value : null;
    }

    // ADDED THIS METHOD:
    public async Task<TableEntity?> UpdateStatusAsync(string date, string orderId, string status)
    {
        await _orders.CreateIfNotExistsAsync();
        var existing = await _orders.GetEntityIfExistsAsync<TableEntity>(date, orderId);

        if (!existing.HasValue || existing.Value is null)
            return null;

        var entity = existing.Value;
        entity["Status"] = status;
        entity["UpdatedAt"] = DateTimeOffset.UtcNow;

        await _orders.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace);
        return entity;
    }
}
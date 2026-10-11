using Azure.Data.Tables;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
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
            ["SelectedItemSKUs"] = JsonSerializer.Serialize(order.SelectedItemSkUs),
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
}

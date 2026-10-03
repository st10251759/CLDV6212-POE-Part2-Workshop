using System.Text.Json;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PageTurn.Functions.Helpers;
using PageTurn.Functions.Models;
using PageTurn.Functions.Models.Dtos;
using PageTurn.Functions.Models.Entities;
using PageTurn.Functions.Services;

namespace PageTurn.Functions.Functions;

/// <summary>
/// Fires automatically when a message lands on order-processing-queue.
/// Failures are rethrown: the runtime retries (host.json maxDequeueCount = 3), then moves the
/// message to order-processing-queue-poison.
/// </summary>
public class ProcessOrderQueueFunction
{
    private static readonly string[] Lifecycle = { OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Collected };

    private readonly ILogger<ProcessOrderQueueFunction> _logger;
    private readonly IOrderService _orders;
    private readonly int _delaySeconds;

    public ProcessOrderQueueFunction(ILogger<ProcessOrderQueueFunction> logger, IOrderService orders, IConfiguration config)
    {
        _logger = logger;
        _orders = orders;
        _delaySeconds = config.GetValue<int?>("StatusTransitionDelaySeconds") ?? 3;
    }

    [Function("ProcessOrderQueue")]
    public async Task Run(
        [QueueTrigger(StorageNames.OrderQueue, Connection = StorageNames.ConnectionSetting)] QueueMessage message,
        CancellationToken ct)
    {
        _logger.LogInformation("Processing message {Id} (attempt {Attempt})", message.MessageId, message.DequeueCount);

        try
        {
            // 1. Parse the JSON payload into an Order object.
            var order = JsonSerializer.Deserialize<OrderMessage>(message.MessageText, JsonDefaults.Options);

            if (order is null
                || string.IsNullOrWhiteSpace(order.OrderId)
                || string.IsNullOrWhiteSpace(order.CustomerName)
                || order.SelectedItemSKUs is null || order.SelectedItemSKUs.Count == 0
                || order.TotalPrice <= 0)
            {
                throw new InvalidDataException("Order message is missing required fields.");
            }

            // 2. Write the order to the Orders table with status "Received".
            bool created = await _orders.CreateAsync(order, ct);
            if (!created)
            {
                _logger.LogWarning("Order {OrderId} already exists, skipping duplicate message.", order.OrderId);
                return;
            }
            _logger.LogInformation("Order {OrderId} stored with status {Status}", order.OrderId, OrderStatus.Received);

            // 3. Simulate the lifecycle: Received → Preparing → Ready → Collected.
            string orderDate = OrderEntity.ToPartitionKey(order.OrderTimestamp);
            foreach (string status in Lifecycle)
            {
                await Task.Delay(TimeSpan.FromSeconds(_delaySeconds), ct);

                var updated = await _orders.UpdateStatusAsync(orderDate, order.OrderId, status, ct);
                if (updated is null)
                {
                    _logger.LogWarning("Order {OrderId} disappeared during processing.", order.OrderId);
                    return;
                }
                _logger.LogInformation("Order {OrderId} → {Status}", order.OrderId, status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Message {Id} failed on attempt {Attempt}. After the maximum retries it moves to '{Poison}'.",
                message.MessageId, message.DequeueCount, StorageNames.OrderQueuePoison);
            throw;   // rethrow so the runtime retries and eventually poisons the message
        }
    }
}
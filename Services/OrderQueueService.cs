using System.Text.Json;
using Azure.Storage.Queues;
using PageTurn.Functions.Helpers;
using PageTurn.Functions.Models.Dtos;

namespace PageTurn.Functions.Services;

public interface IOrderQueueService
{
    Task EnqueueAsync(OrderMessage order, CancellationToken ct = default);
}

/// <summary>Queue producer: serialises the order to JSON and pushes it (Base64-encoded by the client options).</summary>
public class OrderQueueService : IOrderQueueService
{
    private readonly QueueClient _queue;
    private bool _queueReady;

    public OrderQueueService(QueueServiceClient serviceClient)
    {
        _queue = serviceClient.GetQueueClient(StorageNames.OrderQueue);
    }

    public async Task EnqueueAsync(OrderMessage order, CancellationToken ct = default)
    {
        if (!_queueReady)
        {
            await _queue.CreateIfNotExistsAsync(cancellationToken: ct);
            _queueReady = true;
        }

        string json = JsonSerializer.Serialize(order);
        await _queue.SendMessageAsync(json, ct);
    }
}
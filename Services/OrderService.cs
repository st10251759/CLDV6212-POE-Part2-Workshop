using Azure;
using Azure.Data.Tables;
using PageTurn.Functions.Helpers;
using PageTurn.Functions.Models.Dtos;
using PageTurn.Functions.Models.Entities;

namespace PageTurn.Functions.Services;

public interface IOrderService
{
    Task<bool> CreateAsync(OrderMessage order, CancellationToken ct = default);
    Task<OrderEntity?> UpdateStatusAsync(string orderDate, string orderId, string status, CancellationToken ct = default);
    Task<OrderEntity?> GetAsync(string orderDate, string orderId, CancellationToken ct = default);
    Task<IReadOnlyList<OrderEntity>> GetAllAsync(string? orderDate, CancellationToken ct = default);
}

public class OrderService : IOrderService
{
    private readonly TableClient _table;
    private bool _tableReady;

    public OrderService(TableServiceClient serviceClient)
    {
        _table = serviceClient.GetTableClient(StorageNames.OrdersTable);
    }

    /// <summary>Returns false if the order already exists, so queue redelivery is idempotent.</summary>
    public async Task<bool> CreateAsync(OrderMessage order, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);
        try
        {
            await _table.AddEntityAsync(OrderEntity.FromMessage(order), ct);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            return false;
        }
    }

    public async Task<OrderEntity?> UpdateStatusAsync(string orderDate, string orderId, string status, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);
        try
        {
            var entity = (await _table.GetEntityAsync<OrderEntity>(orderDate, orderId, cancellationToken: ct)).Value;
            entity.Status = status;
            entity.LastUpdatedUtc = DateTimeOffset.UtcNow;
            await _table.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Merge, ct);
            return entity;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<OrderEntity?> GetAsync(string orderDate, string orderId, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);
        try
        {
            return (await _table.GetEntityAsync<OrderEntity>(orderDate, orderId, cancellationToken: ct)).Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<OrderEntity>> GetAllAsync(string? orderDate, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);
        var results = new List<OrderEntity>();

        var query = orderDate is null
            ? _table.QueryAsync<OrderEntity>(cancellationToken: ct)
            : _table.QueryAsync<OrderEntity>(o => o.PartitionKey == orderDate, cancellationToken: ct);

        await foreach (var order in query)
            results.Add(order);

        return results.OrderByDescending(o => o.OrderTimestamp).ToList();
    }

    private async Task EnsureTableAsync(CancellationToken ct)
    {
        if (_tableReady) return;
        await _table.CreateIfNotExistsAsync(ct);
        _tableReady = true;
    }
}
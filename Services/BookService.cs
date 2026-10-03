using Azure;
using Azure.Data.Tables;
using PageTurn.Functions.Helpers;
using PageTurn.Functions.Models.Dtos;
using PageTurn.Functions.Models.Entities;

namespace PageTurn.Functions.Services;

public interface IBookService
{
    Task<BookEntity?> CreateAsync(CreateBookRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<BookEntity>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BookEntity>> GetByCategoryAsync(string category, CancellationToken ct = default);
    Task<BookEntity?> UpdateAsync(string category, string sku, UpdateBookRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(string category, string sku, CancellationToken ct = default);
}

public class BookService : IBookService
{
    private readonly TableClient _table;
    private bool _tableReady;

    public BookService(TableServiceClient serviceClient)
    {
        _table = serviceClient.GetTableClient(StorageNames.BooksTable);
    }

    /// <summary>Returns null when the SKU already exists in that category (caller maps to 409).</summary>
    public async Task<BookEntity?> CreateAsync(CreateBookRequest request, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);

        var entity = new BookEntity
        {
            PartitionKey = request.Category.Trim(),
            RowKey = request.Sku.Trim(),
            Title = request.Title.Trim(),
            Author = request.Author.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Price = request.Price,
            IsAvailable = request.IsAvailable
        };

        try
        {
            await _table.AddEntityAsync(entity, ct);
            return entity;
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<BookEntity>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);
        var results = new List<BookEntity>();
        await foreach (var book in _table.QueryAsync<BookEntity>(cancellationToken: ct))
            results.Add(book);
        return results;
    }

    /// <summary>Typed LINQ filter is parameterised by the SDK, so there is no OData injection risk.</summary>
    public async Task<IReadOnlyList<BookEntity>> GetByCategoryAsync(string category, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);
        var results = new List<BookEntity>();
        await foreach (var book in _table.QueryAsync<BookEntity>(b => b.PartitionKey == category, cancellationToken: ct))
            results.Add(book);
        return results;
    }

    public async Task<BookEntity?> UpdateAsync(string category, string sku, UpdateBookRequest request, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);
        try
        {
            var entity = (await _table.GetEntityAsync<BookEntity>(category, sku, cancellationToken: ct)).Value;

            if (request.Price.HasValue) entity.Price = request.Price.Value;
            if (request.IsAvailable.HasValue) entity.IsAvailable = request.IsAvailable.Value;

            // Using the entity's ETag gives optimistic concurrency (412 → mapped to 409).
            await _table.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Merge, ct);
            return entity;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<bool> DeleteAsync(string category, string sku, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);
        try
        {
            await _table.DeleteEntityAsync(category, sku, cancellationToken: ct);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    private async Task EnsureTableAsync(CancellationToken ct)
    {
        if (_tableReady) return;
        await _table.CreateIfNotExistsAsync(ct);
        _tableReady = true;
    }
}
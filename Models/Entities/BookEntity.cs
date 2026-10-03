using Azure;
using Azure.Data.Tables;

namespace PageTurn.Functions.Models.Entities;

/// <summary>Table "Books". PartitionKey = Category, RowKey = SKU.</summary>
public class BookEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;   // Category e.g. "Textbooks"
    public string RowKey { get; set; } = string.Empty;         // SKU e.g. "TXT-001"
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Price { get; set; }
    public bool IsAvailable { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
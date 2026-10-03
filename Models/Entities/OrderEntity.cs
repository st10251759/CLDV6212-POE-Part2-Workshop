using Azure;
using Azure.Data.Tables;
using PageTurn.Functions.Models.Dtos;

namespace PageTurn.Functions.Models.Entities;

/// <summary>Table "Orders". PartitionKey = OrderDate (yyyy-MM-dd), RowKey = OrderId.</summary>
public class OrderEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string SelectedItemSkus { get; set; } = string.Empty;   // Table Storage has no array type → comma-separated
    public double TotalPrice { get; set; }
    public DateTimeOffset OrderTimestamp { get; set; }
    public string Status { get; set; } = OrderStatus.Received;
    public DateTimeOffset LastUpdatedUtc { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public static string ToPartitionKey(DateTimeOffset timestamp) => timestamp.UtcDateTime.ToString("yyyy-MM-dd");

    public static OrderEntity FromMessage(OrderMessage m) => new()
    {
        PartitionKey = ToPartitionKey(m.OrderTimestamp),
        RowKey = m.OrderId,
        CustomerName = m.CustomerName,
        SelectedItemSkus = string.Join(",", m.SelectedItemSKUs),
        TotalPrice = m.TotalPrice,
        OrderTimestamp = m.OrderTimestamp,
        Status = OrderStatus.Received,
        LastUpdatedUtc = DateTimeOffset.UtcNow
    };
}
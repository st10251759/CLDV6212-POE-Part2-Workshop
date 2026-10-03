using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using PageTurn.Functions.Helpers;
using PageTurn.Functions.Models.Entities;

namespace PageTurn.Functions.Models.Dtos;

/// <summary>Body of POST /api/orders/queue.</summary>
public class PlaceOrderRequest : IValidatableObject
{
    [Required(ErrorMessage = "OrderId is required.")]
    [RegularExpression(ValidationPatterns.OrderId, ErrorMessage = "OrderId must look like 'ORD-2026-8801'.")]
    public string OrderId { get; set; } = string.Empty;

    [Required(ErrorMessage = "CustomerName is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "CustomerName must be 2-100 characters.")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "SelectedItemSKUs is required.")]
    [MinLength(1, ErrorMessage = "At least one SKU is required.")]
    [MaxLength(20, ErrorMessage = "An order can contain at most 20 SKUs.")]
    public List<string> SelectedItemSKUs { get; set; } = new();

    [Range(0.01, 100000, ErrorMessage = "TotalPrice must be between 0.01 and 100000.")]
    public double TotalPrice { get; set; }

    /// <summary>Optional. The server generates one when omitted.</summary>
    public DateTimeOffset? OrderTimestamp { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        // DataAnnotations do not validate list items, so check each SKU here.
        foreach (var sku in SelectedItemSKUs ?? new List<string>())
        {
            if (!ValidationPatterns.IsValidSku(sku))
                yield return new ValidationResult($"SKU '{sku}' is invalid. Expected a format like 'TXT-001'.",
                    new[] { nameof(SelectedItemSKUs) });
        }
    }

    public OrderMessage ToMessage() => new()
    {
        OrderId = OrderId.Trim(),
        CustomerName = CustomerName.Trim(),
        SelectedItemSKUs = SelectedItemSKUs,
        TotalPrice = TotalPrice,
        OrderTimestamp = OrderTimestamp ?? DateTimeOffset.UtcNow   // timestamp generation
    };
}

/// <summary>The JSON payload placed on order-processing-queue (PascalCase, matching the brief).</summary>
public class OrderMessage
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public List<string> SelectedItemSKUs { get; set; } = new();
    public double TotalPrice { get; set; }
    public DateTimeOffset OrderTimestamp { get; set; }
}

public record OrderQueuedResponse(string OrderId, string Status, string Message, string StatusUrl);

public record OrderResponse(
    string OrderDate, string OrderId, string CustomerName, IReadOnlyList<string> SelectedItemSKUs,
    double TotalPrice, DateTimeOffset OrderTimestamp, string Status, DateTimeOffset LastUpdatedUtc)
{
    public static OrderResponse FromEntity(OrderEntity e) => new(
        e.PartitionKey, e.RowKey, e.CustomerName,
        e.SelectedItemSkus.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        e.TotalPrice, e.OrderTimestamp, e.Status, e.LastUpdatedUtc);
}
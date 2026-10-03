using System.Text.RegularExpressions;

namespace PageTurn.Functions.Helpers;

/// <summary>Single source of truth for every storage resource name.</summary>
public static class StorageNames
{
    public const string ConnectionSetting = "AzureWebJobsStorage";
    public const string BooksTable = "Books";
    public const string OrdersTable = "Orders";
    public const string StaffDocsContainer = "staff-docs";
    public const string OrderQueue = "order-processing-queue";
    public const string OrderQueuePoison = "order-processing-queue-poison";
}

/// <summary>Shared validation patterns (used by DTO attributes and route checks).</summary>
public static class ValidationPatterns
{
    public const string Category = @"^[A-Za-z0-9 &\-]{2,50}$";
    public const string Sku = @"^[A-Z]{2,4}-\d{3,5}$";
    public const string OrderId = @"^ORD-\d{4}-\d{3,6}$";

    public static bool IsValidCategory(string? value) => value is not null && Regex.IsMatch(value, Category);
    public static bool IsValidSku(string? value) => value is not null && Regex.IsMatch(value, Sku);
}
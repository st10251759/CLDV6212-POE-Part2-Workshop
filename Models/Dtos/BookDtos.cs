using System.ComponentModel.DataAnnotations;
using PageTurn.Functions.Helpers;
using PageTurn.Functions.Models.Entities;

namespace PageTurn.Functions.Models.Dtos;

public class CreateBookRequest
{
    [Required(ErrorMessage = "Category is required.")]
    [RegularExpression(ValidationPatterns.Category,
        ErrorMessage = "Category must be 2-50 characters: letters, numbers, spaces, '&' or '-'.")]
    public string Category { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sku is required.")]
    [RegularExpression(ValidationPatterns.Sku, ErrorMessage = "Sku must look like 'TXT-001'.")]
    public string Sku { get; set; } = string.Empty;

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Author is required.")]
    [StringLength(100, ErrorMessage = "Author cannot exceed 100 characters.")]
    public string Author { get; set; } = string.Empty;

    [StringLength(250, ErrorMessage = "Description cannot exceed 250 characters.")]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 100000, ErrorMessage = "Price must be between 0.01 and 100000.")]
    public double Price { get; set; }

    public bool IsAvailable { get; set; } = true;
}

/// <summary>PUT body: price and/or availability (at least one required).</summary>
public class UpdateBookRequest : IValidatableObject
{
    [Range(0.01, 100000, ErrorMessage = "Price must be between 0.01 and 100000.")]
    public double? Price { get; set; }

    public bool? IsAvailable { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Price is null && IsAvailable is null)
            yield return new ValidationResult("Provide at least one of 'price' or 'isAvailable'.",
                new[] { nameof(Price), nameof(IsAvailable) });
    }
}

public record BookResponse(
    string Category, string Sku, string Title, string Author, string Description,
    double Price, bool IsAvailable, DateTimeOffset? LastModified)
{
    /// <summary>Entity → DTO mapping so storage keys never leak into the API contract.</summary>
    public static BookResponse FromEntity(BookEntity e) =>
        new(e.PartitionKey, e.RowKey, e.Title, e.Author, e.Description, e.Price, e.IsAvailable, e.Timestamp);
}
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PageTurn.Functions.Helpers;
using PageTurn.Functions.Models.Dtos;
using PageTurn.Functions.Services;

namespace PageTurn.Functions.Functions;

/// <summary>HTTP endpoints for the Books table. Triggers validate and map; services talk to storage.</summary>
public class BookFunctions
{
    private readonly ILogger<BookFunctions> _logger;
    private readonly IBookService _books;

    public BookFunctions(ILogger<BookFunctions> logger, IBookService books)
    {
        _logger = logger;
        _books = books;
    }

    [Function("CreateBook")]
    public async Task<IActionResult> CreateBook(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "books")] HttpRequest req,
        CancellationToken ct)
    {
        try
        {
            var parsed = await RequestReader.ReadJsonAsync<CreateBookRequest>(req, ct);
            if (!parsed.IsValid)
                return ApiResults.BadRequest("Validation failed.", parsed.Errors);

            var created = await _books.CreateAsync(parsed.Value!, ct);
            if (created is null)
                return ApiResults.Conflict($"Book '{parsed.Value!.Sku}' already exists in category '{parsed.Value.Category}'.");

            _logger.LogInformation("Created book {Sku} in {Category}", created.RowKey, created.PartitionKey);
            return new ObjectResult(BookResponse.FromEntity(created)) { StatusCode = StatusCodes.Status201Created };
        }
        catch (Exception ex) { return ApiResults.FromException(_logger, ex); }
    }

    [Function("GetAllBooks")]
    public async Task<IActionResult> GetAllBooks(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "books")] HttpRequest req,
        CancellationToken ct)
    {
        try
        {
            var books = await _books.GetAllAsync(ct);
            return new OkObjectResult(books.Select(BookResponse.FromEntity));
        }
        catch (Exception ex) { return ApiResults.FromException(_logger, ex); }
    }

    [Function("GetBooksByCategory")]
    public async Task<IActionResult> GetBooksByCategory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "books/category/{category}")] HttpRequest req,
        string category,
        CancellationToken ct)
    {
        try
        {
            if (!ValidationPatterns.IsValidCategory(category))
                return ApiResults.BadRequest("Invalid category.",
                    new[] { "Category must be 2-50 characters: letters, numbers, spaces, '&' or '-'." });

            var books = await _books.GetByCategoryAsync(category, ct);
            return new OkObjectResult(books.Select(BookResponse.FromEntity));
        }
        catch (Exception ex) { return ApiResults.FromException(_logger, ex); }
    }

    [Function("UpdateBook")]
    public async Task<IActionResult> UpdateBook(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "books/{category}/{sku}")] HttpRequest req,
        string category,
        string sku,
        CancellationToken ct)
    {
        try
        {
            if (!ValidationPatterns.IsValidCategory(category) || !ValidationPatterns.IsValidSku(sku))
                return ApiResults.BadRequest("Invalid category or SKU in the route.");

            var parsed = await RequestReader.ReadJsonAsync<UpdateBookRequest>(req, ct);
            if (!parsed.IsValid)
                return ApiResults.BadRequest("Validation failed.", parsed.Errors);

            var updated = await _books.UpdateAsync(category, sku, parsed.Value!, ct);
            return updated is null
                ? ApiResults.NotFound($"Book '{sku}' was not found in category '{category}'.")
                : new OkObjectResult(BookResponse.FromEntity(updated));
        }
        catch (Exception ex) { return ApiResults.FromException(_logger, ex); }
    }

    [Function("DeleteBook")]
    public async Task<IActionResult> DeleteBook(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "books/{category}/{sku}")] HttpRequest req,
        string category,
        string sku,
        CancellationToken ct)
    {
        try
        {
            if (!ValidationPatterns.IsValidCategory(category) || !ValidationPatterns.IsValidSku(sku))
                return ApiResults.BadRequest("Invalid category or SKU in the route.");

            bool deleted = await _books.DeleteAsync(category, sku, ct);
            return deleted
                ? new OkObjectResult(new { message = $"Book '{sku}' deleted from '{category}'." })
                : ApiResults.NotFound($"Book '{sku}' was not found in category '{category}'.");
        }
        catch (Exception ex) { return ApiResults.FromException(_logger, ex); }
    }
}
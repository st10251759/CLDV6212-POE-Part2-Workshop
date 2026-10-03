using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace PageTurn.Functions.Helpers;

public sealed class ParsedRequest<T> where T : class
{
    public T? Value { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public bool IsValid => Value is not null && Errors.Count == 0;
}

/// <summary>Reads a JSON body, then validates it with DataAnnotations / IValidatableObject.</summary>
public static class RequestReader
{
    public static async Task<ParsedRequest<T>> ReadJsonAsync<T>(HttpRequest req, CancellationToken ct = default)
        where T : class
    {
        string body;
        using (var reader = new StreamReader(req.Body))
        {
            body = await reader.ReadToEndAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(body))
            return Fail<T>("Request body is required.");

        T? value;
        try
        {
            value = JsonSerializer.Deserialize<T>(body, JsonDefaults.Options);
        }
        catch (JsonException)
        {
            return Fail<T>("Request body is not valid JSON or contains values of the wrong type.");
        }

        if (value is null)
            return Fail<T>("Request body is required.");

        var results = new List<ValidationResult>();
        bool ok = Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);

        return ok
            ? new ParsedRequest<T> { Value = value }
            : new ParsedRequest<T>
            {
                Value = value,
                Errors = results.Select(r => r.ErrorMessage ?? "Invalid value.").ToList()
            };
    }

    private static ParsedRequest<T> Fail<T>(string error) where T : class =>
        new() { Errors = new[] { error } };
}
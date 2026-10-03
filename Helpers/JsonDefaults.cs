using System.Text.Json;

namespace PageTurn.Functions.Helpers;

public static class JsonDefaults
{
    /// <summary>Case-insensitive so "title" and "Title" both bind.</summary>
    public static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
}
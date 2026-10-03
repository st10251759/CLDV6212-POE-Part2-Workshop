using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace PageTurn.Functions.Helpers;

/// <summary>All upload rules in one place: missing, empty, oversized, wrong type, spoofed type, bad name.</summary>
public static class DocumentValidator
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;                 // 5 MB
    public const long MaxRequestBytes = MaxFileSizeBytes + 1024 * 1024;   // + multipart overhead

    private const string AllowedExtension = ".pdf";
    private const string AllowedContentType = "application/pdf";
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly Regex SafeName =
        new(@"^[A-Za-z0-9][A-Za-z0-9 _\-.()]{0,95}\.pdf$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Returns an error result, or null when the file is acceptable.</summary>
    public static async Task<IActionResult?> ValidateUploadAsync(IFormFile? file)
    {
        if (file is null)
            return ApiResults.BadRequest("No file received.",
                new[] { "Send multipart/form-data with the file in a form field named 'file'." });

        if (file.Length == 0)
            return ApiResults.BadRequest("The uploaded file is empty.");

        if (file.Length > MaxFileSizeBytes)
            return ApiResults.PayloadTooLarge(
                $"File is {file.Length / 1024.0 / 1024.0:F1} MB. The maximum allowed size is {MaxFileSizeBytes / 1024 / 1024} MB.");

        string name = Path.GetFileName(file.FileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name))
            return ApiResults.BadRequest("The file must have a name.");

        if (!string.Equals(Path.GetExtension(name), AllowedExtension, StringComparison.OrdinalIgnoreCase))
            return ApiResults.UnsupportedMediaType("Only PDF files are allowed.",
                new[] { $"Received extension '{Path.GetExtension(name)}'." });

        if (!SafeName.IsMatch(name))
            return ApiResults.BadRequest("Invalid file name.",
                new[] { "Use letters, numbers, spaces, '-', '_', '.', '(' or ')' (max 100 characters)." });

        if (!string.Equals(file.ContentType, AllowedContentType, StringComparison.OrdinalIgnoreCase))
            return ApiResults.UnsupportedMediaType("Content-Type must be application/pdf.",
                new[] { $"Received '{file.ContentType}'." });

        // Guards against a renamed file: check the real "%PDF-" header bytes.
        await using var stream = file.OpenReadStream();
        var header = new byte[PdfSignature.Length];
        int read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        if (read < header.Length || !header.AsSpan().SequenceEqual(PdfSignature))
            return ApiResults.UnsupportedMediaType("The file content is not a valid PDF.");

        return null;
    }

    /// <summary>Blocks path traversal and unexpected names on download.</summary>
    public static bool IsSafeFileName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name == Path.GetFileName(name) && SafeName.IsMatch(name);
}
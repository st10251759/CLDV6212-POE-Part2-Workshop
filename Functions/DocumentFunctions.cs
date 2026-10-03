using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using PageTurn.Functions.Helpers;
using PageTurn.Functions.Services;

namespace PageTurn.Functions.Functions;

/// <summary>Staff documents (supplier catalogues, return policies, till guides) in Blob Storage.</summary>
public class DocumentFunctions
{
    private readonly ILogger<DocumentFunctions> _logger;
    private readonly IDocumentService _documents;

    public DocumentFunctions(ILogger<DocumentFunctions> logger, IDocumentService documents)
    {
        _logger = logger;
        _documents = documents;
    }

    [Function("UploadStaffDocument")]
    public async Task<IActionResult> Upload(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "documents/upload")] HttpRequest req,
        CancellationToken ct)
    {
        try
        {
            if (!req.HasFormContentType)
                return ApiResults.UnsupportedMediaType("Content-Type must be multipart/form-data.");

            // Reject oversized requests before the body is parsed.
            if (req.ContentLength > DocumentValidator.MaxRequestBytes)
                return ApiResults.PayloadTooLarge(
                    $"Request exceeds the {DocumentValidator.MaxFileSizeBytes / 1024 / 1024} MB file limit.");

            var form = await req.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");

            var error = await DocumentValidator.ValidateUploadAsync(file);
            if (error is not null) return error;

            string safeName = Path.GetFileName(file!.FileName);
            await using var stream = file.OpenReadStream();
            var info = await _documents.UploadAsync(safeName, "application/pdf", stream, ct);

            _logger.LogInformation("Uploaded {File} ({Bytes} bytes)", info.FileName, info.SizeInBytes);
            return new ObjectResult(info) { StatusCode = StatusCodes.Status201Created };
        }
        catch (Exception ex) { return ApiResults.FromException(_logger, ex); }
    }

    [Function("ListStaffDocuments")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents")] HttpRequest req,
        CancellationToken ct)
    {
        try
        {
            return new OkObjectResult(await _documents.ListAsync(ct));
        }
        catch (Exception ex) { return ApiResults.FromException(_logger, ex); }
    }

    [Function("DownloadStaffDocument")]
    public async Task<IActionResult> Download(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents/download/{fileName}")] HttpRequest req,
        string fileName,
        CancellationToken ct)
    {
        try
        {
            if (!DocumentValidator.IsSafeFileName(fileName))
                return ApiResults.BadRequest("Invalid file name.");

            var doc = await _documents.DownloadAsync(fileName, ct);
            if (doc is null)
                return ApiResults.NotFound($"Document '{fileName}' was not found.");

            // Streamed straight from Blob Storage to the client.
            return new FileStreamResult(doc.Content, doc.ContentType) { FileDownloadName = doc.FileName };
        }
        catch (Exception ex) { return ApiResults.FromException(_logger, ex); }
    }
}
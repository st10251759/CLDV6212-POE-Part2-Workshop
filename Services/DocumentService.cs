using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using PageTurn.Functions.Helpers;
using PageTurn.Functions.Models.Dtos;

namespace PageTurn.Functions.Services;

public interface IDocumentService
{
    Task<DocumentInfo> UploadAsync(string fileName, string contentType, Stream content, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentInfo>> ListAsync(CancellationToken ct = default);
    Task<DocumentDownload?> DownloadAsync(string fileName, CancellationToken ct = default);
}

/// <summary>Azure Blob Storage (container "staff-docs"). Blob is used because Azurite does not emulate File Shares.</summary>
public class DocumentService : IDocumentService
{
    private readonly BlobContainerClient _container;
    private bool _containerReady;

    public DocumentService(BlobServiceClient serviceClient)
    {
        _container = serviceClient.GetBlobContainerClient(StorageNames.StaffDocsContainer);
    }

    /// <summary>Streams the upload straight to the blob (no full in-memory copy). Same name overwrites.</summary>
    public async Task<DocumentInfo> UploadAsync(string fileName, string contentType, Stream content, CancellationToken ct = default)
    {
        await EnsureContainerAsync(ct);
        var blob = _container.GetBlobClient(fileName);

        await blob.UploadAsync(content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, ct);

        BlobProperties props = (await blob.GetPropertiesAsync(cancellationToken: ct)).Value;
        return new DocumentInfo(fileName, props.ContentLength, props.ContentType, props.CreatedOn, props.LastModified);
    }

    public async Task<IReadOnlyList<DocumentInfo>> ListAsync(CancellationToken ct = default)
    {
        await EnsureContainerAsync(ct);
        var results = new List<DocumentInfo>();

        await foreach (BlobItem item in _container.GetBlobsAsync(cancellationToken: ct))
        {
            results.Add(new DocumentInfo(
                item.Name,
                item.Properties.ContentLength ?? 0,
                item.Properties.ContentType ?? "application/octet-stream",
                item.Properties.CreatedOn,
                item.Properties.LastModified));
        }
        return results;
    }

    /// <summary>Returns null if the blob does not exist (caller maps to 404).</summary>
    public async Task<DocumentDownload?> DownloadAsync(string fileName, CancellationToken ct = default)
    {
        await EnsureContainerAsync(ct);
        try
        {
            var result = (await _container.GetBlobClient(fileName).DownloadStreamingAsync(cancellationToken: ct)).Value;
            return new DocumentDownload(result.Content, result.Details.ContentType ?? "application/pdf", fileName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private async Task EnsureContainerAsync(CancellationToken ct)
    {
        if (_containerReady) return;
        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
        _containerReady = true;
    }
}
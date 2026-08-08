using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using AiBrandStudio.Application.Common;
using Microsoft.Extensions.Configuration;

namespace AiBrandStudio.Infrastructure.Storage;

public class AzureBlobFileStorage : IFileStorage
{
    private readonly BlobContainerClient _container;

    public AzureBlobFileStorage(IConfiguration config)
    {
        var cs = config["Storage:Azure:ConnectionString"]
                 ?? throw new InvalidOperationException("Storage:Azure:ConnectionString is required when Storage:Provider=AzureBlob.");
        var container = config["Storage:Azure:Container"] ?? "assets";
        _container = new BlobContainerClient(cs, container);
        _container.CreateIfNotExists(PublicAccessType.None);
    }

    public async Task<string> SaveAsync(string relativePath, byte[] content, CancellationToken ct = default)
    {
        var path = NormalizeStoragePath(relativePath);
        var blob = _container.GetBlobClient(path);
        await using var ms = new MemoryStream(content);
        await blob.UploadAsync(ms, overwrite: true, cancellationToken: ct);
        return path;
    }

    public async Task<byte[]?> ReadAsync(string relativePath, CancellationToken ct = default)
    {
        var path = NormalizeStoragePath(relativePath);
        if (string.IsNullOrWhiteSpace(path)) return null;
        var blob = _container.GetBlobClient(path);
        if (!await blob.ExistsAsync(ct)) return null;
        var download = await blob.DownloadContentAsync(ct);
        return download.Value.Content.ToArray();
    }

    public string GetAssetContentUrl(Guid assetId) => $"/api/assets/{assetId}/content";

    public string NormalizeStoragePath(string? pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl)) return string.Empty;
        var value = pathOrUrl.Trim().Replace('\\', '/');
        if (value.StartsWith("/storage/", StringComparison.OrdinalIgnoreCase))
            value = value["/storage/".Length..];
        if (value.StartsWith("storage/", StringComparison.OrdinalIgnoreCase))
            value = value["storage/".Length..];
        if (value.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        return value.TrimStart('/');
    }
}

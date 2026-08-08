using AiBrandStudio.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiBrandStudio.Application.Features.Generators;

public record DownloadAssetQuery(Guid AssetId, Guid? TokenUserId = null) : IRequest<(byte[] Bytes, string ContentType, string FileName)>;

public class DownloadAssetHandler(IAppDbContext db, ICurrentUser current, IFileStorage storage)
    : IRequestHandler<DownloadAssetQuery, (byte[] Bytes, string ContentType, string FileName)>
{
    public async Task<(byte[] Bytes, string ContentType, string FileName)> Handle(
        DownloadAssetQuery request,
        CancellationToken cancellationToken)
    {
        var userId = request.TokenUserId ?? current.UserId ?? throw new UnauthorizedAccessException();
        var asset = await db.Assets.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AssetId && a.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Asset not found.");

        var bytes = await storage.ReadAsync(asset.StoragePath, cancellationToken)
            ?? throw new KeyNotFoundException("Asset file missing.");

        return (bytes, asset.ContentType, asset.FileName);
    }
}

public record CreateAssetDownloadLinkQuery(Guid AssetId) : IRequest<object>;

public class CreateAssetDownloadLinkHandler(
    IAppDbContext db,
    ICurrentUser current,
    IFileStorage storage,
    IDownloadTokenService tokens) : IRequestHandler<CreateAssetDownloadLinkQuery, object>
{
    public async Task<object> Handle(CreateAssetDownloadLinkQuery request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var exists = await db.Assets.AsNoTracking()
            .AnyAsync(a => a.Id == request.AssetId && a.UserId == userId, cancellationToken);
        if (!exists) throw new KeyNotFoundException("Asset not found.");

        var token = tokens.CreateAssetToken(userId, request.AssetId, TimeSpan.FromMinutes(5));
        var path = storage.GetAssetContentUrl(request.AssetId);
        return new
        {
            url = $"{path}?dt={Uri.EscapeDataString(token)}",
            expiresInSeconds = 300
        };
    }
}

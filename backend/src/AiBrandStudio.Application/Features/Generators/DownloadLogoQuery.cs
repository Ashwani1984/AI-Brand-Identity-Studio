using AiBrandStudio.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiBrandStudio.Application.Features.Generators;

public record DownloadLogoQuery(Guid LogoId) : IRequest<(byte[] Bytes, string ContentType, string FileName)>;

public class DownloadLogoHandler(IAppDbContext db, ICurrentUser current, IFileStorage storage)
    : IRequestHandler<DownloadLogoQuery, (byte[] Bytes, string ContentType, string FileName)>
{
    public async Task<(byte[] Bytes, string ContentType, string FileName)> Handle(
        DownloadLogoQuery request,
        CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var logo = await db.Logos.AsNoTracking()
            .Include(l => l.Brand).ThenInclude(b => b.Project)
            .FirstOrDefaultAsync(l => l.Id == request.LogoId && l.Brand.Project.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Logo not found.");

        var path = storage.NormalizeStoragePath(logo.ImageUrl);
        if (string.IsNullOrWhiteSpace(path))
            throw new KeyNotFoundException("Logo file missing.");

        var bytes = await storage.ReadAsync(path, cancellationToken)
            ?? throw new KeyNotFoundException("Logo file missing.");

        var contentType = path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? "image/svg+xml" : "image/png";
        return (bytes, contentType, Path.GetFileName(path));
    }
}

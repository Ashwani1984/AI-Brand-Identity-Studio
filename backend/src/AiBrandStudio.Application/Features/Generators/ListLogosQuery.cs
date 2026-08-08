using AiBrandStudio.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiBrandStudio.Application.Features.Generators;

public record ListLogosQuery(Guid BrandId) : IRequest<IReadOnlyList<LogoDto>>;

public class ListLogosHandler(IAppDbContext db, ICurrentUser current, IFileStorage storage)
    : IRequestHandler<ListLogosQuery, IReadOnlyList<LogoDto>>
{
    public async Task<IReadOnlyList<LogoDto>> Handle(ListLogosQuery request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var owns = await db.Brands.AnyAsync(b => b.Id == request.BrandId && b.Project.UserId == userId, cancellationToken);
        if (!owns) throw new KeyNotFoundException("Brand not found.");

        var logos = await db.Logos.AsNoTracking()
            .Where(l => l.BrandId == request.BrandId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var assets = await db.Assets.AsNoTracking()
            .Where(a => a.BrandId == request.BrandId && a.Kind == "Logo")
            .ToListAsync(cancellationToken);

        return logos.Select(l =>
        {
            var path = storage.NormalizeStoragePath(l.ImageUrl);
            var asset = assets.FirstOrDefault(a =>
                string.Equals(a.StoragePath, path, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a.StoragePath, l.ImageUrl, StringComparison.OrdinalIgnoreCase));
            var url = asset is not null
                ? storage.GetAssetContentUrl(asset.Id)
                : $"/api/logos/{l.Id}/content";
            return new LogoDto(l.Id, l.BrandId, l.Style, l.PromptText, url, l.SvgContent, l.Provider);
        }).ToList();
    }
}

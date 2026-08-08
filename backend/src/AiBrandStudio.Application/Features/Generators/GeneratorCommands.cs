using System.Text;
using AiBrandStudio.Application.Common;
using AiBrandStudio.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiBrandStudio.Application.Features.Generators;

public record GenerateLogoCommand(Guid BrandId, string Style) : IRequest<LogoDto>;
public record GeneratePaletteCommand(Guid BrandId) : IRequest<PaletteDto>;
public record ExportPaletteQuery(Guid PaletteId, string Format) : IRequest<string>;
public record SuggestTypographyCommand(Guid BrandId) : IRequest<FontDto>;
public record GetAssetsQuery() : IRequest<IReadOnlyList<AssetDto>>;
public record ExportZipCommand(Guid ProjectId, string? PackageType = null) : IRequest<AssetDto>;
public record GetPlansQuery() : IRequest<IReadOnlyList<PlanDto>>;
public record CreateRazorpayOrderCommand(string PlanCode) : IRequest<object>;
public record HandleRazorpayWebhookCommand(string RawBody, string? Signature, bool IsDevelopment) : IRequest<object>;
public record ListUsersQuery() : IRequest<IReadOnlyList<UserAdminDto>>;

public class GenerateLogoHandler(
    IAppDbContext db,
    ICurrentUser current,
    IAiProviderFactory ai,
    ICreditService credits,
    IFileStorage storage) : IRequestHandler<GenerateLogoCommand, LogoDto>
{
    public async Task<LogoDto> Handle(GenerateLogoCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var brand = await db.Brands.Include(b => b.Project)
            .FirstOrDefaultAsync(b => b.Id == request.BrandId && b.Project.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Brand not found.");

        const int cost = 8;
        const string reason = "LogoGenerate";
        await credits.DebitAsync(userId, cost, reason, brand.Id, cancellationToken);
        try
        {
            var name = brand.GeneratedName ?? brand.BusinessName;
            var prompt =
                $"Minimal vector logo for {name}, {request.Style} style, geometric core-node icon, premium typography, teal and ink palette, flat design, SVG style, centered, white background, no mockup, no shadow, no watermark";
            var provider = ai.GetImageProvider();
            var (bytes, contentType, svg) = await provider.GenerateImageAsync(prompt, cancellationToken);
            var path = $"logos/{brand.Id}/{Guid.NewGuid()}.png";
            await storage.SaveAsync(path, bytes, cancellationToken);

            var logo = new Logo
            {
                BrandId = brand.Id,
                Style = request.Style,
                PromptText = prompt,
                ImageUrl = path,
                SvgContent = svg,
                Provider = provider.Name
            };
            db.Add(logo);
            var asset = new Asset
            {
                UserId = userId,
                ProjectId = brand.ProjectId,
                BrandId = brand.Id,
                Kind = "Logo",
                FileName = Path.GetFileName(path),
                ContentType = contentType,
                StoragePath = path,
                ByteSize = bytes.LongLength
            };
            db.Add(asset);
            db.Add(new PromptRecord
            {
                BrandId = brand.Id,
                UserId = userId,
                Provider = provider.Name,
                Kind = "LogoGenerate",
                InputText = prompt,
                OutputText = path
            });
            await db.SaveChangesAsync(cancellationToken);
            return new LogoDto(logo.Id, logo.BrandId, logo.Style, logo.PromptText, storage.GetAssetContentUrl(asset.Id), logo.SvgContent, logo.Provider);
        }
        catch
        {
            await credits.CreditAsync(userId, cost, $"Refund:{reason}", cancellationToken);
            throw;
        }
    }
}

public class GeneratePaletteHandler(
    IAppDbContext db,
    ICurrentUser current,
    ICreditService credits) : IRequestHandler<GeneratePaletteCommand, PaletteDto>
{
    public async Task<PaletteDto> Handle(GeneratePaletteCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var brand = await db.Brands.Include(b => b.Project)
            .FirstOrDefaultAsync(b => b.Id == request.BrandId && b.Project.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Brand not found.");

        const int cost = 2;
        const string reason = "PaletteGenerate";
        await credits.DebitAsync(userId, cost, reason, brand.Id, cancellationToken);
        try
        {
            var palette = new Palette
            {
                BrandId = brand.Id,
                PrimaryHex = "#0F766E",
                SecondaryHex = "#334155",
                AccentHex = "#C2410C",
                BackgroundHex = "#F7F8FA",
                TypographyHex = "#0B1220"
            };
            palette.CssExport = BuildCss(palette);
            db.Add(palette);
            await db.SaveChangesAsync(cancellationToken);
            return Map(palette);
        }
        catch
        {
            await credits.CreditAsync(userId, cost, $"Refund:{reason}", cancellationToken);
            throw;
        }
    }

    internal static string BuildCss(Palette p) =>
        $":root {{\n  --color-primary: {p.PrimaryHex};\n  --color-secondary: {p.SecondaryHex};\n  --color-accent: {p.AccentHex};\n  --color-bg: {p.BackgroundHex};\n  --color-text: {p.TypographyHex};\n}}";

    internal static PaletteDto Map(Palette p) =>
        new(p.Id, p.BrandId, p.PrimaryHex, p.SecondaryHex, p.AccentHex, p.BackgroundHex, p.TypographyHex, p.CssExport);
}

public class ExportPaletteHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<ExportPaletteQuery, string>
{
    public async Task<string> Handle(ExportPaletteQuery request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var p = await db.Palettes.AsNoTracking().Include(x => x.Brand).ThenInclude(b => b.Project)
            .FirstOrDefaultAsync(x => x.Id == request.PaletteId && x.Brand.Project.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Palette not found.");

        return request.Format.ToLowerInvariant() switch
        {
            "css" or "scss" => p.CssExport ?? GeneratePaletteHandler.BuildCss(p),
            "tailwind" => $"module.exports = {{ theme: {{ extend: {{ colors: {{ primary: '{p.PrimaryHex}', secondary: '{p.SecondaryHex}', accent: '{p.AccentHex}', background: '{p.BackgroundHex}', typography: '{p.TypographyHex}' }} }} }} }};",
            "json" => $"{{\"primary\":\"{p.PrimaryHex}\",\"secondary\":\"{p.SecondaryHex}\",\"accent\":\"{p.AccentHex}\",\"background\":\"{p.BackgroundHex}\",\"typography\":\"{p.TypographyHex}\"}}",
            _ => throw new ArgumentException("Unsupported format. Use json|css|scss|tailwind")
        };
    }
}

public class SuggestTypographyHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<SuggestTypographyCommand, FontDto>
{
    public async Task<FontDto> Handle(SuggestTypographyCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var brand = await db.Brands.Include(b => b.Project)
            .FirstOrDefaultAsync(b => b.Id == request.BrandId && b.Project.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Brand not found.");

        var font = new FontRecommendation
        {
            BrandId = brand.Id,
            HeadlineFont = "Plus Jakarta Sans",
            BodyFont = "Plus Jakarta Sans",
            MonoFont = "JetBrains Mono",
            GoogleFontsUrl = "https://fonts.google.com/share?selection.family=Plus+Jakarta+Sans:wght@400;500;600;700|JetBrains+Mono"
        };
        db.Add(font);
        await db.SaveChangesAsync(cancellationToken);
        return new FontDto(font.Id, font.BrandId, font.HeadlineFont, font.BodyFont, font.MonoFont, font.GoogleFontsUrl);
    }
}

public class GetAssetsHandler(IAppDbContext db, ICurrentUser current, IFileStorage storage)
    : IRequestHandler<GetAssetsQuery, IReadOnlyList<AssetDto>>
{
    public async Task<IReadOnlyList<AssetDto>> Handle(GetAssetsQuery request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var rows = await db.Assets.AsNoTracking().Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return rows.Select(a => new AssetDto(a.Id, a.Kind, a.FileName, a.ContentType, storage.GetAssetContentUrl(a.Id), a.ByteSize, a.CreatedAtUtc)).ToList();
    }
}

public class ExportZipHandler(
    IAppDbContext db,
    ICurrentUser current,
    IFileStorage storage,
    ICreditService credits) : IRequestHandler<ExportZipCommand, AssetDto>
{
    public async Task<AssetDto> Handle(ExportZipCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var package = string.IsNullOrWhiteSpace(request.PackageType) ? "Standard" : request.PackageType!;
        var isPro = package.Equals("Pro", StringComparison.OrdinalIgnoreCase);
        const int proCost = 5;
        const string proReason = "ExportZipPro";
        if (isPro) await credits.DebitAsync(userId, proCost, proReason, request.ProjectId, cancellationToken);

        try
        {
            var project = await db.Projects.Include(p => p.Brand).ThenInclude(b => b!.Palettes)
                .Include(p => p.Brand).ThenInclude(b => b!.Logos)
                .Include(p => p.Brand).ThenInclude(b => b!.Fonts)
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.UserId == userId, cancellationToken)
                ?? throw new KeyNotFoundException("Project not found.");

            var relatedAssets = await db.Assets.AsNoTracking()
                .Where(a => a.ProjectId == project.Id)
                .OrderByDescending(a => a.CreatedAtUtc)
                .Take(isPro ? 40 : 10)
                .ToListAsync(cancellationToken);

            using var ms = new MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                var brandJson = archive.CreateEntry("01-brand/brand.json");
                await using (var s = brandJson.Open())
                {
                    var snapshot = new
                    {
                        package,
                        project.Brand?.Id,
                        project.Brand?.BusinessName,
                        project.Brand?.GeneratedName,
                        project.Brand?.Tagline,
                        project.Brand?.BrandStory,
                        project.Brand?.BrandVoice,
                        project.Brand?.Mission,
                        project.Brand?.Vision,
                        project.Brand?.Industry,
                        exportedAtUtc = DateTime.UtcNow
                    };
                    var payload = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(snapshot));
                    await s.WriteAsync(payload, cancellationToken);
                }

                var readme = archive.CreateEntry("README.txt");
                await using (var s = readme.Open())
                {
                    var text = Encoding.UTF8.GetBytes(
                        $"{package} Brand Kit export for {project.Brand?.GeneratedName ?? project.Name}\n" +
                        "Includes brand JSON, palette CSS, logos, and selected assets.\n" +
                        "Pro package includes guidelines/landing/mockup assets when available.\n" +
                        "Upgrade at /billing for more exports and white-label delivery.\n");
                    await s.WriteAsync(text, cancellationToken);
                }

                if (project.Brand?.Palettes.FirstOrDefault() is { } palette)
                {
                    var css = archive.CreateEntry("02-palette/palette.css");
                    await using var s = css.Open();
                    var bytes = Encoding.UTF8.GetBytes(palette.CssExport ?? GeneratePaletteHandler.BuildCss(palette));
                    await s.WriteAsync(bytes, cancellationToken);

                    var json = archive.CreateEntry("02-palette/palette.json");
                    await using var js = json.Open();
                    var pj = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new
                    {
                        palette.PrimaryHex, palette.SecondaryHex, palette.AccentHex, palette.BackgroundHex, palette.TypographyHex
                    }));
                    await js.WriteAsync(pj, cancellationToken);
                }

                if (project.Brand?.Fonts.FirstOrDefault() is { } font)
                {
                    var fonts = archive.CreateEntry("03-typography/fonts.json");
                    await using var s = fonts.Open();
                    var bytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new
                    {
                        font.HeadlineFont, font.BodyFont, font.MonoFont, font.GoogleFontsUrl
                    }));
                    await s.WriteAsync(bytes, cancellationToken);
                }

                foreach (var logo in project.Brand?.Logos ?? [])
                {
                    var relative = storage.NormalizeStoragePath(logo.ImageUrl);
                    if (string.IsNullOrWhiteSpace(relative)) continue;
                    var data = await storage.ReadAsync(relative, cancellationToken);
                    if (data is null) continue;
                    var entry = archive.CreateEntry($"04-logos/{Path.GetFileName(relative)}");
                    await using var s = entry.Open();
                    await s.WriteAsync(data, cancellationToken);
                }

                foreach (var asset in relatedAssets)
                {
                    if (asset.Kind is "ExportZip") continue;
                    if (!isPro && asset.Kind is "BrandGuidelines" or "BrandGuidelinesHtml" or "LandingPage" or "Mockup:BusinessCard")
                        continue;
                    var data = await storage.ReadAsync(asset.StoragePath, cancellationToken);
                    if (data is null) continue;
                    var folder = asset.Kind.StartsWith("Mockup") ? "05-mockups" :
                        asset.Kind.Contains("Guideline") ? "06-guidelines" :
                        asset.Kind == "LandingPage" ? "07-landing" : "08-assets";
                    var entry = archive.CreateEntry($"{folder}/{asset.FileName}");
                    await using var s = entry.Open();
                    await s.WriteAsync(data, cancellationToken);
                }
            }

            var zipBytes = ms.ToArray();
            var path = $"exports/{userId}/{package.ToLowerInvariant()}-{project.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
            await storage.SaveAsync(path, zipBytes, cancellationToken);
            var export = new ExportJob
            {
                UserId = userId,
                ProjectId = project.Id,
                Format = isPro ? "ZIP-PRO" : "ZIP",
                StoragePath = path
            };
            db.Add(export);
            var assetRow = new Asset
            {
                UserId = userId,
                ProjectId = project.Id,
                BrandId = project.Brand?.Id,
                Kind = isPro ? "ExportZipPro" : "ExportZip",
                FileName = Path.GetFileName(path),
                ContentType = "application/zip",
                StoragePath = path,
                ByteSize = zipBytes.LongLength
            };
            db.Add(assetRow);
            await db.SaveChangesAsync(cancellationToken);
            return new AssetDto(assetRow.Id, assetRow.Kind, assetRow.FileName, assetRow.ContentType, storage.GetAssetContentUrl(assetRow.Id), assetRow.ByteSize, assetRow.CreatedAtUtc);
        }
        catch
        {
            if (isPro) await credits.CreditAsync(userId, proCost, $"Refund:{proReason}", cancellationToken);
            throw;
        }
    }
}

public class GetPlansHandler(IAppDbContext db) : IRequestHandler<GetPlansQuery, IReadOnlyList<PlanDto>>
{
    public async Task<IReadOnlyList<PlanDto>> Handle(GetPlansQuery request, CancellationToken cancellationToken) =>
        await db.SubscriptionPlans.AsNoTracking().Where(p => p.IsActive)
            .Select(p => new PlanDto(p.Id, p.Code, p.Name, p.MonthlyPriceInr, p.MonthlyCredits))
            .ToListAsync(cancellationToken);
}

public class CreateRazorpayOrderHandler(ICurrentUser current, IPaymentGateway payments)
    : IRequestHandler<CreateRazorpayOrderCommand, object>
{
    public async Task<object> Handle(CreateRazorpayOrderCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        return await payments.CreateOrderForPlanAsync(userId, request.PlanCode, cancellationToken);
    }
}

public class HandleRazorpayWebhookHandler(IPaymentGateway payments)
    : IRequestHandler<HandleRazorpayWebhookCommand, object>
{
    public Task<object> Handle(HandleRazorpayWebhookCommand request, CancellationToken cancellationToken) =>
        payments.HandleWebhookAsync(request.RawBody, request.Signature, request.IsDevelopment, cancellationToken);
}

public class ListUsersHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<ListUsersQuery, IReadOnlyList<UserAdminDto>>
{
    public async Task<IReadOnlyList<UserAdminDto>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        if (!current.IsAdmin) throw new UnauthorizedAccessException("Admin only.");
        return await db.Users.AsNoTracking().OrderByDescending(u => u.CreatedAtUtc)
            .Select(u => new UserAdminDto(u.Id, u.Email, u.FullName, u.CreditBalance, u.IsActive, u.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}

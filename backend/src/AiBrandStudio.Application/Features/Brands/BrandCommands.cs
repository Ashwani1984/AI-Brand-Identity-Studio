using System.Text.Json;
using AiBrandStudio.Application.Common;
using AiBrandStudio.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiBrandStudio.Application.Features.Brands;

public record WizardBrandCommand(
    Guid? ProjectId,
    string? ProjectName,
    string BusinessName,
    string? Industry,
    string? Audience,
    string? Competitors,
    string? Country,
    string? Language,
    string? Style,
    string? Mood,
    string? PreferredColors,
    string? PreferredFonts,
    string? Keywords,
    string? Mission,
    string? Vision,
    string? Usp) : IRequest<BrandDto>;

public record GenerateBrandCommand(Guid BrandId) : IRequest<BrandDto>;
public record GetBrandQuery(Guid BrandId) : IRequest<BrandDto>;

public class WizardBrandValidator : AbstractValidator<WizardBrandCommand>
{
    public WizardBrandValidator()
    {
        RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(200);
    }
}

public class WizardBrandHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<WizardBrandCommand, BrandDto>
{
    public async Task<BrandDto> Handle(WizardBrandCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        Project project;
        if (request.ProjectId is Guid pid)
        {
            project = await db.Projects.Include(p => p.Brand)
                .FirstOrDefaultAsync(p => p.Id == pid && p.UserId == userId, cancellationToken)
                ?? throw new KeyNotFoundException("Project not found.");
        }
        else
        {
            project = new Project
            {
                UserId = userId,
                Name = string.IsNullOrWhiteSpace(request.ProjectName) ? request.BusinessName : request.ProjectName!.Trim(),
                Status = "Draft"
            };
            db.Add(project);
            await db.SaveChangesAsync(cancellationToken);
        }

        Brand brand;
        if (project.Brand is null)
        {
            brand = new Brand { ProjectId = project.Id };
            db.Add(brand);
        }
        else brand = project.Brand;

        brand.BusinessName = request.BusinessName.Trim();
        brand.Industry = request.Industry;
        brand.Audience = request.Audience;
        brand.Competitors = request.Competitors;
        brand.Country = request.Country;
        brand.Language = request.Language;
        brand.Style = request.Style;
        brand.Mood = request.Mood;
        brand.PreferredColors = request.PreferredColors;
        brand.PreferredFonts = request.PreferredFonts;
        brand.Keywords = request.Keywords;
        brand.Mission = request.Mission;
        brand.Vision = request.Vision;
        brand.Usp = request.Usp;
        brand.UpdatedAtUtc = DateTime.UtcNow;
        project.Status = "InProgress";
        project.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Map(brand);
    }

    internal static BrandDto Map(Brand b) => new(
        b.Id, b.ProjectId, b.BusinessName, b.Industry, b.Audience, b.Competitors, b.Country, b.Language,
        b.Style, b.Mood, b.PreferredColors, b.PreferredFonts, b.Keywords, b.Mission, b.Vision, b.Usp,
        b.GeneratedName, b.Tagline, b.BrandStory, b.BrandVoice);
}

public class GenerateBrandHandler(
    IAppDbContext db,
    ICurrentUser current,
    IAiProviderFactory ai,
    ICreditService credits) : IRequestHandler<GenerateBrandCommand, BrandDto>
{
    public async Task<BrandDto> Handle(GenerateBrandCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var brand = await db.Brands.Include(b => b.Project)
            .FirstOrDefaultAsync(b => b.Id == request.BrandId && b.Project.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Brand not found.");

        const int cost = 5;
        const string reason = "BrandGenerate";
        await credits.DebitAsync(userId, cost, reason, brand.Id, cancellationToken);
        try
        {
            var provider = ai.GetTextProvider();
            var input = JsonSerializer.Serialize(new
            {
                brand.BusinessName,
                brand.Industry,
                brand.Audience,
                brand.Competitors,
                brand.Style,
                brand.Mood,
                brand.Keywords,
                brand.Mission,
                brand.Vision,
                brand.Usp
            });

            var system = "You are a brand strategist. Return concise brand identity fields as plain text with labels: NAME, TAGLINE, STORY, VOICE, MISSION, VISION.";
            var output = await provider.GenerateTextAsync(system, input, cancellationToken);

            brand.GeneratedName = Extract(output, "NAME") ?? brand.BusinessName;
            brand.Tagline = Extract(output, "TAGLINE") ?? "Build with clarity.";
            brand.BrandStory = Extract(output, "STORY") ?? output;
            brand.BrandVoice = Extract(output, "VOICE") ?? "Premium, modern, precise.";
            brand.Mission ??= Extract(output, "MISSION");
            brand.Vision ??= Extract(output, "VISION");
            brand.UpdatedAtUtc = DateTime.UtcNow;
            brand.Project.Status = "Generated";

            db.Add(new PromptRecord
            {
                BrandId = brand.Id,
                UserId = userId,
                Provider = provider.Name,
                Kind = "BrandGenerate",
                InputText = input,
                OutputText = output
            });
            db.Add(new BrandVersion
            {
                BrandId = brand.Id,
                VersionNumber = await db.Brands.Where(b => b.Id == brand.Id).CountAsync(cancellationToken),
                SnapshotJson = JsonSerializer.Serialize(WizardBrandHandler.Map(brand))
            });

            await db.SaveChangesAsync(cancellationToken);
            return WizardBrandHandler.Map(brand);
        }
        catch
        {
            await credits.CreditAsync(userId, cost, $"Refund:{reason}", cancellationToken);
            throw;
        }
    }

    private static string? Extract(string text, string label)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var line = lines.FirstOrDefault(l => l.StartsWith(label, StringComparison.OrdinalIgnoreCase));
        if (line is null) return null;
        var idx = line.IndexOf(':');
        return idx >= 0 ? line[(idx + 1)..].Trim() : null;
    }
}

public class GetBrandHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<GetBrandQuery, BrandDto>
{
    public async Task<BrandDto> Handle(GetBrandQuery request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var brand = await db.Brands.AsNoTracking().Include(b => b.Project)
            .FirstOrDefaultAsync(b => b.Id == request.BrandId && b.Project.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Brand not found.");
        return WizardBrandHandler.Map(brand);
    }
}

using System.Text;
using System.Text.Json;
using AiBrandStudio.Application.Common;
using AiBrandStudio.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiBrandStudio.Application.Features.Modules;

public record GeneratePromptCommand(Guid BrandId, string Kind) : IRequest<object>;
public record GenerateMockupCommand(Guid BrandId, string MockupType) : IRequest<object>;
public record GenerateLandingCommand(Guid BrandId) : IRequest<object>;
public record GenerateVideoCommand(Guid BrandId, string Provider) : IRequest<object>;
public record GenerateMarketingCommand(Guid BrandId) : IRequest<object>;
public record GenerateSocialCalendarCommand(Guid BrandId, int Days) : IRequest<object>;
public record GenerateGuidelinesCommand(Guid BrandId) : IRequest<object>;
public record GetAnalyticsQuery() : IRequest<object>;
public record ListAdminLogsQuery() : IRequest<object>;

public class GeneratePromptHandler(IAppDbContext db, ICurrentUser current, IAiProviderFactory ai, ICreditService credits)
    : IRequestHandler<GeneratePromptCommand, object>
{
    public async Task<object> Handle(GeneratePromptCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var brand = await RequireBrand(db, userId, request.BrandId, cancellationToken);
        const int cost = 1;
        const string reason = "PromptPack";
        await credits.DebitAsync(userId, cost, reason, brand.Id, cancellationToken);
        try
        {
            var provider = ai.GetTextProvider();
            var name = brand.GeneratedName ?? brand.BusinessName;
            var tagline = brand.Tagline ?? "Build from the core.";
            var style = brand.Style ?? "Minimal";
            var industry = brand.Industry ?? "Technology";
            var kind = string.IsNullOrWhiteSpace(request.Kind) ? "Logo" : request.Kind;
            var basePrompt = kind.ToLowerInvariant() switch
            {
                "logo" => $"Minimal vector logo for {name}, {style} geometric core-node icon, premium sans wordmark, teal #0F766E and ink #0B1220, flat SVG style, centered, white background, sharp edges, luxury branding, no mockup, no shadow, no watermark, ultra high resolution",
                "mockup" => $"Photorealistic premium product mockup for {name}, branded {industry} merchandise, teal core-node logo print, soft studio lighting, porcelain table, 85mm lens, 4K commercial photography, no watermark",
                "video" => $"10-second cinematic 4K 60fps brand film for {name}, teal particle atmosphere, logo crystallize, end card '{tagline}', CTA Start free, premium lighting, parallax, depth of field",
                "social" => $"Premium Instagram 1080x1080 post for {name}, bold headline '{tagline}', teal ink porcelain palette, minimal SaaS creative, high contrast typography, no clutter, no watermark",
                "landing" => $"Website hero section UI for {name}, brand-first composition, headline '{tagline}', full-bleed atmospheric background, primary CTA, premium SaaS landing design, desktop 1440px",
                "packaging" => $"Luxury matte packaging mockup for {name}, geometric teal and ink system, centered logo, soft rim light, unboxing aesthetic, photorealistic, 4K",
                "ads" => $"Meta ad creative 1080x1080 for {name}, headline '{tagline}', clean premium SaaS look, strong CTA Learn More, teal accent, conversion-focused layout",
                _ => $"Premium brand creative for {name} ({kind}), teal and ink, minimal luxury, ultra high resolution, no watermark"
            };

            var tools = new
            {
                ideogram = basePrompt,
                midjourney = $"{basePrompt} --v 6 --stylize 50 --style raw",
                dalle = basePrompt,
                flux = $"award-winning {basePrompt}",
                stableDiffusion = $"(masterpiece:1.2), {basePrompt}\nNegative prompt: watermark, blurry, lowres, text artifacts, mockup clutter",
                runway = kind.Equals("Video", StringComparison.OrdinalIgnoreCase) ? basePrompt : $"cinematic motion graphic using brand mark for {name}, {tagline}",
                chatgpt = $"You are a brand creative director for {name} ({industry}). Improve this production prompt and keep it under 80 words:\n{basePrompt}"
            };

            var json = System.Text.Json.JsonSerializer.Serialize(new { kind, basePrompt, tools });
            db.Add(new PromptRecord
            {
                BrandId = brand.Id,
                UserId = userId,
                Provider = provider.Name,
                Kind = $"Prompt:{kind}",
                InputText = kind,
                OutputText = json
            });
            await db.SaveChangesAsync(cancellationToken);
            return new
            {
                brandId = brand.Id,
                brandName = name,
                kind,
                creditCost = 1,
                masterPrompt = basePrompt,
                tools,
                monetizationTip = "Sell these prompt packs to clients as Ideogram/Midjourney/Flux deliverables, or upsell Pro credits for unlimited prompt suites.",
                provider = provider.Name
            };
        }
        catch
        {
            await credits.CreditAsync(userId, cost, $"Refund:{reason}", cancellationToken);
            throw;
        }
    }

    internal static async Task<Brand> RequireBrand(IAppDbContext db, Guid userId, Guid brandId, CancellationToken ct) =>
        await db.Brands.Include(b => b.Project)
            .FirstOrDefaultAsync(b => b.Id == brandId && b.Project.UserId == userId, ct)
        ?? throw new KeyNotFoundException("Brand not found.");
}

public class GenerateMockupHandler(IAppDbContext db, ICurrentUser current, IAiProviderFactory ai, IFileStorage storage, ICreditService credits)
    : IRequestHandler<GenerateMockupCommand, object>
{
    public async Task<object> Handle(GenerateMockupCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var brand = await GeneratePromptHandler.RequireBrand(db, userId, request.BrandId, cancellationToken);
        const int cost = 3;
        const string reason = "MockupGenerate";
        await credits.DebitAsync(userId, cost, reason, brand.Id, cancellationToken);
        try
        {
            var type = string.IsNullOrWhiteSpace(request.MockupType) ? "BusinessCard" : request.MockupType;
            var name = brand.GeneratedName ?? brand.BusinessName;
            var prompt = $"Premium {type} mockup for {name}, teal core-node logo, photorealistic studio product photography, 4K, no watermark";
            var image = await ai.GetImageProvider().GenerateImageAsync(prompt, cancellationToken);
            var path = $"mockups/{brand.Id}/{type}-{Guid.NewGuid():N}.svg";
            await storage.SaveAsync(path, image.Bytes, cancellationToken);
            var mockup = new Mockup
            {
                BrandId = brand.Id,
                MockupType = type,
                PromptText = prompt,
                ImageUrl = path
            };
            db.Add(mockup);
            var asset = new Asset
            {
                UserId = userId,
                ProjectId = brand.ProjectId,
                BrandId = brand.Id,
                Kind = $"Mockup:{type}",
                FileName = Path.GetFileName(path),
                ContentType = image.ContentType,
                StoragePath = path,
                ByteSize = image.Bytes.LongLength
            };
            db.Add(asset);
            await db.SaveChangesAsync(cancellationToken);
            return new { mockup.Id, mockup.MockupType, imageUrl = storage.GetAssetContentUrl(asset.Id), mockup.PromptText };
        }
        catch
        {
            await credits.CreditAsync(userId, cost, $"Refund:{reason}", cancellationToken);
            throw;
        }
    }
}

public class GenerateLandingHandler(IAppDbContext db, ICurrentUser current, IAiProviderFactory ai, IFileStorage storage)
    : IRequestHandler<GenerateLandingCommand, object>
{
    public async Task<object> Handle(GenerateLandingCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var brand = await GeneratePromptHandler.RequireBrand(db, userId, request.BrandId, cancellationToken);
        var name = brand.GeneratedName ?? brand.BusinessName;
        var tagline = brand.Tagline ?? "Build from the core.";
        var html = $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8"/>
              <meta name="viewport" content="width=device-width, initial-scale=1"/>
              <title>{{name}}</title>
              <style>
                :root { --teal:#0F766E; --ink:#0B1220; --bg:#F7F8FA; }
                body{margin:0;font-family:system-ui,sans-serif;background:var(--bg);color:var(--ink)}
                header{padding:2rem;display:flex;justify-content:space-between;align-items:center}
                .hero{min-height:70vh;display:grid;place-items:center;text-align:center;padding:2rem;background:radial-gradient(circle at top,rgba(15,118,110,.2),transparent)}
                h1{font-size:clamp(2.5rem,6vw,4.5rem);margin:.2rem 0}
                .brand{font-size:1.1rem;letter-spacing:.18em;text-transform:uppercase;color:var(--teal)}
                .btn{background:var(--teal);color:#fff;border:0;padding:.9rem 1.2rem;border-radius:.8rem;font-weight:600}
                section{padding:3rem 1.5rem;max-width:960px;margin:0 auto}
              </style>
            </head>
            <body>
              <header><strong>{{name}}</strong><button class="btn">Start free</button></header>
              <section class="hero">
                <div>
                  <div class="brand">{{name}}</div>
                  <h1>{{tagline}}</h1>
                  <p>{{brand.BrandStory ?? "A premium brand experience generated by AI Brand Identity Studio."}}</p>
                  <p><button class="btn">See how it works</button></p>
                </div>
              </section>
              <section>
                <h2>About</h2>
                <p>{{brand.Mission ?? "Mission-driven clarity for modern teams."}}</p>
                <h2>Services</h2>
                <p>Brand systems, visual identity, and launch assets.</p>
                <h2>Contact</h2>
                <p>hello@{{name.Replace(" ", "").ToLowerInvariant()}}.com</p>
              </section>
            </body>
            </html>
            """;
        var path = $"landing/{brand.Id}/index.html";
        await storage.SaveAsync(path, Encoding.UTF8.GetBytes(html), cancellationToken);
        var asset = new Asset
        {
            UserId = userId,
            ProjectId = brand.ProjectId,
            BrandId = brand.Id,
            Kind = "LandingPage",
            FileName = "index.html",
            ContentType = "text/html",
            StoragePath = path,
            ByteSize = Encoding.UTF8.GetByteCount(html)
        };
        db.Add(asset);
        var provider = ai.GetTextProvider();
        db.Add(new PromptRecord
        {
            BrandId = brand.Id,
            UserId = userId,
            Provider = provider.Name,
            Kind = "LandingPage",
            InputText = brand.Id.ToString(),
            OutputText = path
        });
        await db.SaveChangesAsync(cancellationToken);
        return new { url = storage.GetAssetContentUrl(asset.Id), htmlPreview = html[..Math.Min(html.Length, 500)] + "..." };
    }
}

public class GenerateVideoHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<GenerateVideoCommand, object>
{
    public async Task<object> Handle(GenerateVideoCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var brand = await GeneratePromptHandler.RequireBrand(db, userId, request.BrandId, cancellationToken);
        var name = brand.GeneratedName ?? brand.BusinessName;
        var provider = string.IsNullOrWhiteSpace(request.Provider) ? "Runway" : request.Provider;
        var prompt = $"10s cinematic 4K 60fps brand film for {name}, teal particles, logo assemble, tagline '{brand.Tagline ?? "Build from the core."}', CTA Start free, premium lighting, parallax, DOF";
        var storyboard = JsonSerializer.Serialize(new
        {
            durationSec = 10,
            shots = new[]
            {
                new { t = "0-3", shot = "Dark ink void with teal particles" },
                new { t = "3-7", shot = "Logo crystallizes with shallow DOF" },
                new { t = "7-10", shot = $"End card: {name} / {brand.Tagline} / Start free" }
            },
            voiceover = $"{name}. {brand.Tagline ?? "Build from the core."}",
            music = "minimal electronic 105 BPM, elegant, no vocals"
        });
        var video = new VideoJob
        {
            BrandId = brand.Id,
            Provider = provider,
            PromptText = prompt,
            StoryboardJson = storyboard,
            Status = "PromptReady"
        };
        db.Add(video);
        await db.SaveChangesAsync(cancellationToken);
        return new { video.Id, video.Provider, video.PromptText, storyboard = JsonSerializer.Deserialize<object>(storyboard), video.Status };
    }
}

public class GenerateMarketingHandler(IAppDbContext db, ICurrentUser current, IAiProviderFactory ai)
    : IRequestHandler<GenerateMarketingCommand, object>
{
    public async Task<object> Handle(GenerateMarketingCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var brand = await GeneratePromptHandler.RequireBrand(db, userId, request.BrandId, cancellationToken);
        var name = brand.GeneratedName ?? brand.BusinessName;
        var pack = new
        {
            brand = name,
            facebookAd = new { primary = $"Serious brands need a calm core. Meet {name}.", headline = brand.Tagline ?? "Build from the core.", cta = "Learn More" },
            instagramAd = new { hook = "Stop scaling chaos", headline = brand.Tagline, cta = "Shop now / Learn More" },
            linkedInAd = new { intro = "CTOs don’t need louder tools.", headline = $"{name} — Architecture that ships", cta = "Request demo" },
            emailSubject = $"Introducing {name}",
            launchPlan = new[] { "Day 1-7 Craft", "Day 8-14 Signal", "Day 15-21 Reveal", "Day 22-30 Amplify" },
            roadmap90 = new[] { "Recognition", "Authority", "Demand" }
        };
        var json = JsonSerializer.Serialize(pack);
        db.Add(new PromptRecord
        {
            BrandId = brand.Id,
            UserId = userId,
            Provider = ai.GetTextProvider().Name,
            Kind = "MarketingPack",
            InputText = brand.Id.ToString(),
            OutputText = json
        });
        await db.SaveChangesAsync(cancellationToken);
        return pack;
    }
}

public class GenerateSocialCalendarHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<GenerateSocialCalendarCommand, object>
{
    public async Task<object> Handle(GenerateSocialCalendarCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var brand = await GeneratePromptHandler.RequireBrand(db, userId, request.BrandId, cancellationToken);
        var days = request.Days <= 0 ? 14 : Math.Min(request.Days, 30);
        var platforms = new[] { "LinkedIn", "Instagram", "X", "Facebook", "YouTube Shorts" };
        var items = Enumerable.Range(1, days).Select(d => new
        {
            day = d,
            platform = platforms[(d - 1) % platforms.Length],
            contentType = d % 3 == 0 ? "Carousel" : d % 2 == 0 ? "Reel/Short" : "Feed post",
            caption = $"{brand.GeneratedName ?? brand.BusinessName}: {brand.Tagline ?? "Build from the core."} — Day {d}",
            cta = d % 2 == 0 ? "Join waitlist" : "Learn more",
            hashtags = "#BrandIdentity #AIDesign #SaaS",
            bestTime = "10:00 IST"
        }).ToList();
        db.Add(new PromptRecord
        {
            BrandId = brand.Id,
            UserId = userId,
            Provider = "Mock",
            Kind = "SocialCalendar",
            InputText = days.ToString(),
            OutputText = JsonSerializer.Serialize(items)
        });
        await db.SaveChangesAsync(cancellationToken);
        return new { brandId = brand.Id, days, items };
    }
}

public class GenerateGuidelinesHandler(IAppDbContext db, ICurrentUser current, IFileStorage storage, ICreditService credits)
    : IRequestHandler<GenerateGuidelinesCommand, object>
{
    public async Task<object> Handle(GenerateGuidelinesCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var brand = await GeneratePromptHandler.RequireBrand(db, userId, request.BrandId, cancellationToken);
        const int cost = 3;
        const string reason = "GuidelinesPro";
        await credits.DebitAsync(userId, cost, reason, brand.Id, cancellationToken);
        try
        {
            var name = brand.GeneratedName ?? brand.BusinessName;
            var tagline = brand.Tagline ?? "Build from the core.";
            var voice = brand.BrandVoice ?? "Premium, modern, precise, and calm.";
            var story = brand.BrandStory ?? $"{name} helps teams ship recognizable brand systems without design friction.";
            var md = $"""
                # {name} — Brand Guidelines
                **Tagline:** {tagline}
                **Version:** 1.0 · Client-ready

                ## 1. Brand promise
                {story}

                ## 2. Voice & tone
                {voice}
                - Do: clear, confident, architectural sentences
                - Don't: hype slang, emoji clutter, buzzword stacks

                ## 3. Logo system
                - Primary lockup: Core Node icon + wordmark
                - Clear space: height of letter "M" on all sides
                - Minimum digital width: 120px
                - Approved: full color, mono black, mono white
                - Forbidden: stretch, rotate, recolor, glow, drop shadow

                ## 4. Color system
                | Role | HEX |
                | --- | --- |
                | Primary Teal | #0F766E |
                | Ink | #0B1220 |
                | Secondary | #334155 |
                | Background | #F7F8FA |
                | Accent | #C2410C |

                ## 5. Typography
                - Headlines: Plus Jakarta Sans / Satoshi Bold
                - Body: Plus Jakarta Sans Regular
                - Mono: JetBrains Mono

                ## 6. Photography & illustration
                Soft daylight, cool neutrals, teal accents, real product context.
                Flat geometric illustration only — no cartoon mascots.

                ## 7. Wrong usage
                Neon cyberpunk, purple gradients, busy photo backgrounds without scrim, low-contrast teal-on-teal.

                ## 8. Commercial use
                These guidelines are intended for client presentations, investor decks, and partner kits.
                Upgrade to Pro for white-label PDF + multi-brand workspaces.
                """;

            var html = $$"""
                <!DOCTYPE html><html><head><meta charset="utf-8"/><title>{{name}} Brand Guidelines</title>
                <style>
                body{font-family:Georgia,serif;max-width:820px;margin:40px auto;padding:0 24px;color:#0B1220;line-height:1.55}
                h1{font-family:system-ui,sans-serif;font-size:2rem;margin-bottom:.2rem}
                .tag{color:#0F766E;font-family:system-ui,sans-serif;letter-spacing:.08em;text-transform:uppercase;font-size:.8rem}
                h2{font-family:system-ui,sans-serif;margin-top:2rem;border-top:1px solid #e5e7eb;padding-top:1rem}
                table{border-collapse:collapse;width:100%} td,th{border:1px solid #e5e7eb;padding:.5rem;text-align:left}
                .swatch{display:inline-block;width:18px;height:18px;border-radius:4px;vertical-align:middle;margin-right:6px}
                </style></head><body>
                <div class="tag">Brand Guidelines · Client Ready</div>
                <h1>{{name}}</h1>
                <p><strong>{{tagline}}</strong></p>
                <h2>Brand promise</h2><p>{{story}}</p>
                <h2>Voice</h2><p>{{voice}}</p>
                <h2>Colors</h2>
                <table><tr><th>Role</th><th>HEX</th></tr>
                <tr><td><span class="swatch" style="background:#0F766E"></span>Primary</td><td>#0F766E</td></tr>
                <tr><td><span class="swatch" style="background:#0B1220"></span>Ink</td><td>#0B1220</td></tr>
                <tr><td><span class="swatch" style="background:#C2410C"></span>Accent</td><td>#C2410C</td></tr>
                </table>
                <h2>Logo rules</h2>
                <ul><li>Clear space = M height</li><li>No stretch / glow / recolor</li><li>Min width 120px digital</li></ul>
                <p style="margin-top:3rem;font-size:.85rem;color:#64748b">Generated by AI Brand Identity Studio · Upgrade for white-label PDF exports</p>
                </body></html>
                """;

            var mdPath = $"guidelines/{brand.Id}/brand-guidelines.md";
            var htmlPath = $"guidelines/{brand.Id}/brand-guidelines.html";
            await storage.SaveAsync(mdPath, Encoding.UTF8.GetBytes(md), cancellationToken);
            await storage.SaveAsync(htmlPath, Encoding.UTF8.GetBytes(html), cancellationToken);
            Guid? mdAssetId = null;
            Guid? htmlAssetId = null;
            foreach (var (path, file, ctype, kind) in new[]
                     {
                         (mdPath, "brand-guidelines.md", "text/markdown", "BrandGuidelines"),
                         (htmlPath, "brand-guidelines.html", "text/html", "BrandGuidelinesHtml")
                     })
            {
                var asset = new Asset
                {
                    UserId = userId,
                    ProjectId = brand.ProjectId,
                    BrandId = brand.Id,
                    Kind = kind,
                    FileName = file,
                    ContentType = ctype,
                    StoragePath = path,
                    ByteSize = Encoding.UTF8.GetByteCount(path.EndsWith(".md") ? md : html)
                };
                db.Add(asset);
                if (kind == "BrandGuidelines") mdAssetId = asset.Id;
                else htmlAssetId = asset.Id;
            }
            await db.SaveChangesAsync(cancellationToken);
            return new
            {
                brandName = name,
                creditCost = 3,
                markdownUrl = storage.GetAssetContentUrl(mdAssetId!.Value),
                htmlUrl = storage.GetAssetContentUrl(htmlAssetId!.Value),
                markdown = md,
                sections = new[] { "Promise", "Voice", "Logo", "Color", "Typography", "Photography", "Wrong usage", "Commercial use" },
                upsell = "Offer this as a Rs 4,999-Rs 14,999 client brand-book deliverable. Pro plan unlocks white-label PDF + unlimited regenerations."
            };
        }
        catch
        {
            await credits.CreditAsync(userId, cost, $"Refund:{reason}", cancellationToken);
            throw;
        }
    }
}

public class GetAnalyticsHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<GetAnalyticsQuery, object>
{
    public async Task<object> Handle(GetAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var projects = await db.Projects.CountAsync(p => p.UserId == userId, cancellationToken);
        var brands = await db.Brands.CountAsync(b => b.Project.UserId == userId, cancellationToken);
        var logos = await db.Logos.CountAsync(l => l.Brand.Project.UserId == userId, cancellationToken);
        var assets = await db.Assets.CountAsync(a => a.UserId == userId, cancellationToken);
        var credits = await db.Users.Where(u => u.Id == userId).Select(u => u.CreditBalance).FirstAsync(cancellationToken);
        var recent = await db.CreditLedgers.AsNoTracking().Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAtUtc).Take(10)
            .Select(c => new { c.Delta, c.Reason, c.CreatedAtUtc })
            .ToListAsync(cancellationToken);
        return new { projects, brands, logos, assets, credits, recentLedger = recent };
    }
}

public class ListAdminLogsHandler(IAppDbContext db, ICurrentUser current) : IRequestHandler<ListAdminLogsQuery, object>
{
    public async Task<object> Handle(ListAdminLogsQuery request, CancellationToken cancellationToken)
    {
        if (!current.IsAdmin) throw new UnauthorizedAccessException("Admin only.");
        var prompts = await db.Prompts.AsNoTracking().OrderByDescending(p => p.CreatedAtUtc).Take(50)
            .Select(p => new { p.Id, p.Kind, p.Provider, p.CreatedAtUtc, p.UserId })
            .ToListAsync(cancellationToken);
        var users = await db.Users.CountAsync(cancellationToken);
        var audit = await db.AuditLogs.AsNoTracking().OrderByDescending(a => a.CreatedAtUtc).Take(50)
            .Select(a => new { a.Id, a.UserId, a.Action, a.EntityType, a.EntityId, a.CreatedAtUtc })
            .ToListAsync(cancellationToken);
        return new { totalUsers = users, recentPrompts = prompts, recentAudit = audit };
    }
}

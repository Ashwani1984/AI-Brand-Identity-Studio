using AiBrandStudio.Application.Common;
using AiBrandStudio.Application.Features.Account;
using AiBrandStudio.Application.Features.Admin;
using AiBrandStudio.Application.Features.Auth;
using AiBrandStudio.Application.Features.Brands;
using AiBrandStudio.Application.Features.Generators;
using AiBrandStudio.Application.Features.Jobs;
using AiBrandStudio.Application.Features.Modules;
using AiBrandStudio.Application.Features.Organizations;
using AiBrandStudio.Application.Features.Projects;
using AiBrandStudio.Application.Features.Subscriptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AiBrandStudio.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController(IMediator mediator) : ControllerBase
{
    [HttpPost("register")]
    public Task<IActionResult> Register([FromBody] RegisterCommand cmd) => Send(cmd);

    [HttpPost("login")]
    public Task<IActionResult> Login([FromBody] LoginCommand cmd) => Send(cmd);

    [HttpPost("refresh")]
    public Task<IActionResult> Refresh([FromBody] RefreshCommand cmd) => Send(cmd);

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutCommand cmd)
    {
        await mediator.Send(cmd);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        try { return Ok(await mediator.Send(new MeQuery())); }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new { error = ex.Message }); }
    }

    [HttpGet("oauth/status")]
    [AllowAnonymous]
    public IActionResult OAuthStatus([FromServices] IConfiguration config) => Ok(new
    {
        google = !string.IsNullOrWhiteSpace(config["OAuth:Google:ClientId"]),
        microsoft = !string.IsNullOrWhiteSpace(config["OAuth:Microsoft:ClientId"]),
        github = !string.IsNullOrWhiteSpace(config["OAuth:GitHub:ClientId"]),
        message = "OAuth providers are enterprise-ready hooks. Configure ClientId/Secret in env to enable IdP login in Phase 2 UI."
    });

    private async Task<IActionResult> Send<T>(IRequest<T> cmd)
    {
        try { return Ok(await mediator.Send(cmd)); }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}

[ApiController]
[Authorize]
[Route("api/projects")]
public class ProjectsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? organizationId) =>
        Ok(await mediator.Send(new GetProjectsQuery(organizationId)));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        try { return Ok(await mediator.Send(new GetProjectQuery(id))); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectCommand cmd) => Ok(await mediator.Send(cmd));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectBody body) =>
        Ok(await mediator.Send(new UpdateProjectCommand(id, body.Name, body.Status)));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await mediator.Send(new DeleteProjectCommand(id));
        return NoContent();
    }

    public record UpdateProjectBody(string Name, string Status);
}

[ApiController]
[Authorize]
[Route("api/brands")]
public class BrandsController(IMediator mediator, IHubContext<Hubs.GenerationHub> hub) : ControllerBase
{
    [HttpPost("wizard")]
    public async Task<IActionResult> Wizard([FromBody] WizardBrandCommand cmd) => Ok(await mediator.Send(cmd));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        try { return Ok(await mediator.Send(new GetBrandQuery(id))); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/generate")]
    public async Task<IActionResult> Generate(Guid id)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userId, out var uid))
                await hub.Clients.User(uid.ToString("D")).SendAsync("generationProgress", new { brandId = id, stage = "started" });
            var result = await mediator.Send(new GenerateBrandCommand(id));
            if (Guid.TryParse(userId, out uid))
                await hub.Clients.User(uid.ToString("D")).SendAsync("generationProgress", new { brandId = id, stage = "completed" });
            return Ok(result);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

[ApiController]
[Authorize]
[Route("api/logos")]
public class LogosController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid brandId)
    {
        try { return Ok(await mediator.Send(new ListLogosQuery(brandId))); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid id)
    {
        try
        {
            var (bytes, contentType, fileName) = await mediator.Send(new DownloadLogoQuery(id));
            return File(bytes, contentType, fileName);
        }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateLogoBody body)
    {
        try
        {
            var job = await mediator.Send(new EnqueueLogoJobCommand(body.BrandId, body.Style ?? "Minimal"));
            return Accepted(job);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or UnauthorizedAccessException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    public record GenerateLogoBody(Guid BrandId, string? Style);
}

[ApiController]
[Authorize]
[Route("api/palettes")]
public class PalettesController(IMediator mediator) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GeneratePaletteBody body)
    {
        try { return Ok(await mediator.Send(new GeneratePaletteCommand(body.BrandId))); }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, [FromQuery] string format = "json")
    {
        try
        {
            var content = await mediator.Send(new ExportPaletteQuery(id, format));
            return Content(content, format is "json" ? "application/json" : "text/plain");
        }
        catch (Exception ex) when (ex is KeyNotFoundException or ArgumentException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    public record GeneratePaletteBody(Guid BrandId);
}

[ApiController]
[Authorize]
[Route("api/typography")]
public class TypographyController(IMediator mediator) : ControllerBase
{
    [HttpPost("suggest")]
    public async Task<IActionResult> Suggest([FromBody] SuggestBody body) =>
        Ok(await mediator.Send(new SuggestTypographyCommand(body.BrandId)));

    public record SuggestBody(Guid BrandId);
}

[ApiController]
[Route("api/assets")]
public class AssetsController(IMediator mediator, IDownloadTokenService tokens) : ControllerBase
{
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await mediator.Send(new GetAssetsQuery()));

    [Authorize]
    [HttpGet("{id:guid}/link")]
    public async Task<IActionResult> Link(Guid id)
    {
        try { return Ok(await mediator.Send(new CreateAssetDownloadLinkQuery(id))); }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet("{id:guid}/content")]
    [AllowAnonymous]
    public async Task<IActionResult> Content(Guid id, [FromQuery] string? dt)
    {
        try
        {
            Guid? tokenUserId = null;
            if (!string.IsNullOrWhiteSpace(dt))
            {
                if (!tokens.TryValidateAssetToken(dt, out var userId, out var assetId) || assetId != id)
                    return Unauthorized(new { error = "Invalid or expired download token." });
                tokenUserId = userId;
            }
            else if (User?.Identity?.IsAuthenticated != true)
            {
                return Unauthorized(new { error = "Authentication required." });
            }

            var (bytes, contentType, fileName) = await mediator.Send(new DownloadAssetQuery(id, tokenUserId));
            return File(bytes, contentType, fileName);
        }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }
}

[ApiController]
[Authorize]
[Route("api/exports")]
public class ExportsController(IMediator mediator) : ControllerBase
{
    [HttpPost("zip")]
    public async Task<IActionResult> Zip([FromBody] ZipBody body)
    {
        try
        {
            var job = await mediator.Send(new EnqueueExportJobCommand(body.ProjectId, body.PackageType));
            return Accepted(job);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    public record ZipBody(Guid ProjectId, string? PackageType);
}

[ApiController]
[Authorize]
[Route("api/jobs")]
public class JobsController(IMediator mediator) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        try { return Ok(await mediator.Send(new GetGenerationJobQuery(id))); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }
}

[ApiController]
[Authorize]
[Route("api/subscriptions")]
public class SubscriptionsController(IMediator mediator) : ControllerBase
{
    [HttpGet("plans")]
    public async Task<IActionResult> Plans() => Ok(await mediator.Send(new GetPlansQuery()));

    [HttpGet("me")]
    public async Task<IActionResult> Me() => Ok(await mediator.Send(new GetMySubscriptionQuery()));

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel([FromBody] CancelBody? body)
    {
        try { return Ok(await mediator.Send(new CancelSubscriptionCommand(body?.Immediately ?? false))); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("renew")]
    public async Task<IActionResult> Renew()
    {
        try { return Ok(await mediator.Send(new RenewSubscriptionCommand())); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    public record CancelBody(bool Immediately);
}

[ApiController]
[Authorize]
[Route("api/organizations")]
public class OrganizationsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await mediator.Send(new ListOrganizationsQuery()));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrganizationCommand cmd) => Ok(await mediator.Send(cmd));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        try { return Ok(await mediator.Send(new GetOrganizationQuery(id))); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> Members(Guid id)
    {
        try { return Ok(await mediator.Send(new ListOrganizationMembersQuery(id))); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> Invite(Guid id, [FromBody] InviteBody body)
    {
        try { return Ok(await mediator.Send(new InviteOrganizationMemberCommand(id, body.Email, body.Role ?? "Member"))); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> Remove(Guid id, Guid userId)
    {
        try
        {
            await mediator.Send(new RemoveOrganizationMemberCommand(id, userId));
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    public record InviteBody(string Email, string? Role);
}

[ApiController]
[Authorize]
[Route("api/account")]
public class AccountController(IMediator mediator) : ControllerBase
{
    [HttpPut("consent")]
    public async Task<IActionResult> Consent([FromBody] UpdateConsentCommand cmd)
    {
        try { return Ok(await mediator.Send(cmd)); }
        catch (ArgumentOutOfRangeException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export() => Ok(await mediator.Send(new ExportAccountDataQuery()));

    [HttpPost("delete-request")]
    public async Task<IActionResult> DeleteRequest([FromBody] DeleteBody body)
    {
        try { return Ok(await mediator.Send(new RequestAccountDeletionCommand(body.Confirmation))); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    public record DeleteBody(string Confirmation);
}

[ApiController]
[Authorize]
[Route("api/payments")]
public class PaymentsController(IMediator mediator, IHostEnvironment env) : ControllerBase
{
    [HttpPost("razorpay/order")]
    public async Task<IActionResult> CreateOrder([FromBody] OrderBody body) =>
        Ok(await mediator.Send(new CreateRazorpayOrderCommand(body.PlanCode)));

    [HttpPost("razorpay/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        using var reader = new StreamReader(Request.Body);
        var raw = await reader.ReadToEndAsync();
        var signature = Request.Headers["X-Razorpay-Signature"].FirstOrDefault();
        return Ok(await mediator.Send(new HandleRazorpayWebhookCommand(raw, signature, env.IsDevelopment())));
    }

    public record OrderBody(string PlanCode);
}

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public class AdminController(IMediator mediator) : ControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> Users() => Ok(await mediator.Send(new ListUsersQuery()));

    [HttpGet("logs")]
    public async Task<IActionResult> Logs() => Ok(await mediator.Send(new ListAdminLogsQuery()));

    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] int take = 100, [FromQuery] string? action = null) =>
        Ok(await mediator.Send(new ListAuditLogsQuery(take, action)));

    [HttpPost("credits/grant")]
    public async Task<IActionResult> GrantCredits([FromBody] GrantBody body)
    {
        try { return Ok(await mediator.Send(new GrantCreditsCommand(body.UserId, body.Amount, body.Reason))); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("credits/grant-org")]
    public async Task<IActionResult> GrantOrgCredits([FromBody] GrantOrgBody body)
    {
        try { return Ok(await mediator.Send(new GrantOrgCreditsCommand(body.OrganizationId, body.Amount, body.Reason))); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("reports/monthly")]
    public async Task<IActionResult> MonthlyReport([FromQuery] int year, [FromQuery] int month)
    {
        try
        {
            var report = await mediator.Send(new MonthlyAdminReportQuery(year, month));
            return File(report.Content, report.ContentType, report.FileName);
        }
        catch (ArgumentOutOfRangeException ex) { return BadRequest(new { error = ex.Message }); }
    }

    public record GrantBody(Guid UserId, int Amount, string Reason);
    public record GrantOrgBody(Guid OrganizationId, int Amount, string Reason);
}

[ApiController]
[Authorize]
[Route("api/prompts")]
public class PromptsController(IMediator mediator) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] PromptBody body) =>
        Ok(await mediator.Send(new GeneratePromptCommand(body.BrandId, body.Kind ?? "Logo")));
    public record PromptBody(Guid BrandId, string? Kind);
}

[ApiController]
[Authorize]
[Route("api/mockups")]
public class MockupsController(IMediator mediator) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] MockupBody body)
    {
        try
        {
            var job = await mediator.Send(new EnqueueMockupJobCommand(body.BrandId, body.MockupType ?? "BusinessCard"));
            return Accepted(job);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException) { return BadRequest(new { error = ex.Message }); }
    }
    public record MockupBody(Guid BrandId, string? MockupType);
}

[ApiController]
[Authorize]
[Route("api/landing")]
public class LandingController(IMediator mediator) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] BrandIdBody body) =>
        Ok(await mediator.Send(new GenerateLandingCommand(body.BrandId)));
    public record BrandIdBody(Guid BrandId);
}

[ApiController]
[Authorize]
[Route("api/videos")]
public class VideosController(IMediator mediator) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] VideoBody body) =>
        Ok(await mediator.Send(new GenerateVideoCommand(body.BrandId, body.Provider ?? "Runway")));
    public record VideoBody(Guid BrandId, string? Provider);
}

[ApiController]
[Authorize]
[Route("api/marketing")]
public class MarketingController(IMediator mediator) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] LandingController.BrandIdBody body) =>
        Ok(await mediator.Send(new GenerateMarketingCommand(body.BrandId)));
}

[ApiController]
[Authorize]
[Route("api/social")]
public class SocialController(IMediator mediator) : ControllerBase
{
    [HttpPost("calendar")]
    public async Task<IActionResult> Calendar([FromBody] SocialBody body) =>
        Ok(await mediator.Send(new GenerateSocialCalendarCommand(body.BrandId, body.Days ?? 14)));
    public record SocialBody(Guid BrandId, int? Days);
}

[ApiController]
[Authorize]
[Route("api/guidelines")]
public class GuidelinesController(IMediator mediator) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] LandingController.BrandIdBody body) =>
        Ok(await mediator.Send(new GenerateGuidelinesCommand(body.BrandId)));
}

[ApiController]
[Authorize]
[Route("api/analytics")]
public class AnalyticsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await mediator.Send(new GetAnalyticsQuery()));
}

[ApiController]
[Authorize]
[Route("api/downloads")]
public class DownloadsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await mediator.Send(new GetAssetsQuery()));

    [HttpPost("zip")]
    public async Task<IActionResult> Zip([FromBody] ExportsController.ZipBody body)
    {
        try
        {
            var job = await mediator.Send(new EnqueueExportJobCommand(body.ProjectId, body.PackageType));
            return Accepted(job);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

using AiBrandStudio.Application.Common;
using AiBrandStudio.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiBrandStudio.Application.Features.Account;

public record UpdateConsentCommand(bool ConsentMarketing, bool AcceptTerms, int? DataRetentionDays) : IRequest<object>;
public record ExportAccountDataQuery() : IRequest<object>;
public record RequestAccountDeletionCommand(string Confirmation) : IRequest<object>;

public class UpdateConsentHandler(IAppDbContext db, ICurrentUser current, IAuditService audit)
    : IRequestHandler<UpdateConsentCommand, object>
{
    public async Task<object> Handle(UpdateConsentCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var user = await db.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        user.ConsentMarketing = request.ConsentMarketing;
        if (request.AcceptTerms)
            user.TermsAcceptedAtUtc ??= DateTime.UtcNow;
        if (request.DataRetentionDays is int days)
        {
            if (days is < 30 or > 3650)
                throw new ArgumentOutOfRangeException(nameof(request.DataRetentionDays), "Retention days must be 30–3650.");
            user.DataRetentionDays = days;
        }
        user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("account.consent_updated", userId, "User", userId,
            new { request.ConsentMarketing, request.AcceptTerms, request.DataRetentionDays }, ct: cancellationToken);

        return new
        {
            user.ConsentMarketing,
            user.TermsAcceptedAtUtc,
            user.DataRetentionDays
        };
    }
}

public class ExportAccountDataHandler(IAppDbContext db, ICurrentUser current, IAuditService audit)
    : IRequestHandler<ExportAccountDataQuery, object>
{
    public async Task<object> Handle(ExportAccountDataQuery request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, cancellationToken);

        var orgs = await db.OrganizationMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new { m.OrganizationId, m.Organization.Name, m.Organization.Slug, m.Role, m.Organization.CreditBalance })
            .ToListAsync(cancellationToken);

        var projects = await db.Projects.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new { p.Id, p.Name, p.Status, p.OrganizationId, p.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        var brands = await db.Brands.AsNoTracking()
            .Where(b => b.Project.UserId == userId)
            .Select(b => new { b.Id, b.ProjectId, b.BusinessName, b.GeneratedName, b.Tagline, b.Industry })
            .ToListAsync(cancellationToken);

        var assets = await db.Assets.AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => new { a.Id, a.Kind, a.FileName, a.ContentType, a.ByteSize, a.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        var ledger = await db.CreditLedgers.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(500)
            .Select(c => new { c.Delta, c.Reason, c.OrganizationId, c.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new { p.Id, p.PlanCode, p.Amount, p.Currency, p.Status, p.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        var subscription = await db.Subscriptions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new { s.Status, Plan = s.Plan.Code, s.TrialEndsUtc, s.CurrentPeriodEndUtc, s.CancelAtPeriodEnd })
            .FirstOrDefaultAsync(cancellationToken);

        await audit.WriteAsync("account.data_exported", userId, "User", userId, ct: cancellationToken);

        return new
        {
            exportedAtUtc = DateTime.UtcNow,
            profile = new
            {
                user.Id,
                user.Email,
                user.FullName,
                user.CreditBalance,
                user.ConsentMarketing,
                user.TermsAcceptedAtUtc,
                user.DataRetentionDays,
                user.DeletionRequestedAtUtc,
                user.CreatedAtUtc
            },
            organizations = orgs,
            projects,
            brands,
            assets,
            creditLedger = ledger,
            payments,
            subscription
        };
    }
}

public class RequestAccountDeletionHandler(IAppDbContext db, ICurrentUser current, IAuditService audit)
    : IRequestHandler<RequestAccountDeletionCommand, object>
{
    public async Task<object> Handle(RequestAccountDeletionCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        if (!string.Equals(request.Confirmation, "DELETE", StringComparison.Ordinal))
            throw new ArgumentException("Confirmation must be exactly DELETE.");

        var user = await db.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        user.DeletionRequestedAtUtc = DateTime.UtcNow;
        user.IsActive = false;
        user.UpdatedAtUtc = DateTime.UtcNow;

        // Revoke refresh tokens
        var tokens = await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null).ToListAsync(cancellationToken);
        foreach (var t in tokens)
            t.RevokedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("account.deletion_requested", userId, "User", userId,
            new { retentionDays = user.DataRetentionDays ?? 30 }, ct: cancellationToken);

        return new
        {
            status = "queued",
            deletionRequestedAtUtc = user.DeletionRequestedAtUtc,
            message = "Account marked inactive and queued for erasure per retention policy. Contact support to cancel within the retention window."
        };
    }
}

using System.Globalization;
using System.Text;
using AiBrandStudio.Application.Common;
using AiBrandStudio.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiBrandStudio.Application.Features.Admin;

public record GrantCreditsCommand(Guid UserId, int Amount, string Reason) : IRequest<object>;
public record GrantOrgCreditsCommand(Guid OrganizationId, int Amount, string Reason) : IRequest<object>;
public record ListAuditLogsQuery(int Take = 100, string? Action = null) : IRequest<IReadOnlyList<AuditLogDto>>;
public record MonthlyAdminReportQuery(int Year, int Month) : IRequest<AdminReportResult>;

public record AdminReportResult(string FileName, string ContentType, byte[] Content);

public class GrantCreditsHandler(IAppDbContext db, ICurrentUser current, ICreditService credits, IAuditService audit)
    : IRequestHandler<GrantCreditsCommand, object>
{
    public async Task<object> Handle(GrantCreditsCommand request, CancellationToken cancellationToken)
    {
        if (!current.IsAdmin) throw new UnauthorizedAccessException("Admin only.");
        if (request.Amount == 0) throw new ArgumentException("Amount must be non-zero.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new ArgumentException("Reason is required.");

        var actorId = current.UserId ?? throw new UnauthorizedAccessException();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");

        if (request.Amount > 0)
            await credits.CreditAsync(user.Id, request.Amount, $"AdminGrant:{request.Reason.Trim()}", cancellationToken);
        else
            await credits.DebitAsync(user.Id, -request.Amount, $"AdminRevoke:{request.Reason.Trim()}", ct: cancellationToken);

        await audit.WriteAsync(
            request.Amount > 0 ? "admin.credit_grant" : "admin.credit_revoke",
            actorId,
            "User",
            user.Id,
            new { request.Amount, request.Reason, balanceAfter = await db.Users.Where(u => u.Id == user.Id).Select(u => u.CreditBalance).FirstAsync(cancellationToken) },
            ct: cancellationToken);

        var balance = await db.Users.Where(u => u.Id == user.Id).Select(u => u.CreditBalance).FirstAsync(cancellationToken);
        return new { userId = user.Id, creditBalance = balance };
    }
}

public class GrantOrgCreditsHandler(IAppDbContext db, ICurrentUser current, ICreditService credits, IAuditService audit)
    : IRequestHandler<GrantOrgCreditsCommand, object>
{
    public async Task<object> Handle(GrantOrgCreditsCommand request, CancellationToken cancellationToken)
    {
        if (!current.IsAdmin) throw new UnauthorizedAccessException("Admin only.");
        if (request.Amount <= 0) throw new ArgumentException("Amount must be positive.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new ArgumentException("Reason is required.");

        var actorId = current.UserId ?? throw new UnauthorizedAccessException();
        if (!await db.Organizations.AnyAsync(o => o.Id == request.OrganizationId, cancellationToken))
            throw new KeyNotFoundException("Organization not found.");

        await credits.CreditOrganizationAsync(
            request.OrganizationId,
            actorId,
            request.Amount,
            $"AdminOrgGrant:{request.Reason.Trim()}",
            cancellationToken);

        await audit.WriteAsync(
            "admin.org_credit_grant",
            actorId,
            "Organization",
            request.OrganizationId,
            new { request.Amount, request.Reason },
            ct: cancellationToken);

        var balance = await db.Organizations.Where(o => o.Id == request.OrganizationId)
            .Select(o => o.CreditBalance).FirstAsync(cancellationToken);
        return new { organizationId = request.OrganizationId, creditBalance = balance };
    }
}

public class ListAuditLogsHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<ListAuditLogsQuery, IReadOnlyList<AuditLogDto>>
{
    public async Task<IReadOnlyList<AuditLogDto>> Handle(ListAuditLogsQuery request, CancellationToken cancellationToken)
    {
        if (!current.IsAdmin) throw new UnauthorizedAccessException("Admin only.");
        var take = Math.Clamp(request.Take, 1, 500);
        var q = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Action))
            q = q.Where(a => a.Action == request.Action);

        return await q.OrderByDescending(a => a.CreatedAtUtc).Take(take)
            .Select(a => new AuditLogDto(a.Id, a.UserId, a.Action, a.EntityType, a.EntityId, a.MetadataJson, a.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}

public class MonthlyAdminReportHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<MonthlyAdminReportQuery, AdminReportResult>
{
    public async Task<AdminReportResult> Handle(MonthlyAdminReportQuery request, CancellationToken cancellationToken)
    {
        if (!current.IsAdmin) throw new UnauthorizedAccessException("Admin only.");
        if (request.Month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(request.Month));
        if (request.Year is < 2020 or > 2100) throw new ArgumentOutOfRangeException(nameof(request.Year));

        var start = new DateTime(request.Year, request.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);

        var revenue = await db.Payments.AsNoTracking()
            .Where(p => p.Status == "Paid" && p.CreatedAtUtc >= start && p.CreatedAtUtc < end)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Total = g.Sum(p => p.Amount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var creditBurn = await db.CreditLedgers.AsNoTracking()
            .Where(c => c.Delta < 0 && c.CreatedAtUtc >= start && c.CreatedAtUtc < end)
            .SumAsync(c => (int?)c.Delta, cancellationToken) ?? 0;

        var creditGrants = await db.CreditLedgers.AsNoTracking()
            .Where(c => c.Delta > 0 && c.CreatedAtUtc >= start && c.CreatedAtUtc < end)
            .SumAsync(c => (int?)c.Delta, cancellationToken) ?? 0;

        var logoCount = await db.Logos.AsNoTracking()
            .CountAsync(l => l.CreatedAtUtc >= start && l.CreatedAtUtc < end, cancellationToken);
        var mockupCount = await db.Mockups.AsNoTracking()
            .CountAsync(m => m.CreatedAtUtc >= start && m.CreatedAtUtc < end, cancellationToken);
        var jobCompleted = await db.GenerationJobs.AsNoTracking()
            .CountAsync(j => j.Status == "Completed" && j.CompletedAtUtc >= start && j.CompletedAtUtc < end, cancellationToken);
        var jobFailed = await db.GenerationJobs.AsNoTracking()
            .CountAsync(j => j.Status == "Failed" && j.CompletedAtUtc >= start && j.CompletedAtUtc < end, cancellationToken);
        var newUsers = await db.Users.AsNoTracking()
            .CountAsync(u => u.CreatedAtUtc >= start && u.CreatedAtUtc < end, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("metric,value");
        sb.AppendLine(Csv("period", $"{request.Year:D4}-{request.Month:D2}"));
        sb.AppendLine(Csv("paid_payment_count", (revenue?.Count ?? 0).ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine(Csv("revenue_inr", (revenue?.Total ?? 0m).ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine(Csv("credit_burn", Math.Abs(creditBurn).ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine(Csv("credit_grants", creditGrants.ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine(Csv("logos_generated", logoCount.ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine(Csv("mockups_generated", mockupCount.ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine(Csv("jobs_completed", jobCompleted.ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine(Csv("jobs_failed", jobFailed.ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine(Csv("new_users", newUsers.ToString(CultureInfo.InvariantCulture)));

        // Detail rows: daily revenue
        sb.AppendLine();
        sb.AppendLine("section,daily_paid_payments");
        sb.AppendLine("date,count,amount_inr");
        var daily = await db.Payments.AsNoTracking()
            .Where(p => p.Status == "Paid" && p.CreatedAtUtc >= start && p.CreatedAtUtc < end)
            .GroupBy(p => p.CreatedAtUtc.Date)
            .Select(g => new { Day = g.Key, Count = g.Count(), Amount = g.Sum(x => x.Amount) })
            .OrderBy(x => x.Day)
            .ToListAsync(cancellationToken);
        foreach (var d in daily)
            sb.AppendLine($"{d.Day:yyyy-MM-dd},{d.Count},{d.Amount.ToString(CultureInfo.InvariantCulture)}");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"admin-report-{request.Year:D4}-{request.Month:D2}.csv";
        return new AdminReportResult(fileName, "text/csv", bytes);
    }

    private static string Csv(string key, string value) =>
        $"{Escape(key)},{Escape(value)}";

    private static string Escape(string v) =>
        v.Contains(',') || v.Contains('"') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
}

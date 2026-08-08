using System.Text.Json;
using AiBrandStudio.Application.Common;
using AiBrandStudio.Domain.Entities;
using AiBrandStudio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiBrandStudio.Infrastructure.Services;

public class AuditService(AppDbContext db, IHttpContextAccessor http) : IAuditService
{
    public async Task WriteAsync(
        string action,
        Guid? actorUserId = null,
        string? entityType = null,
        Guid? entityId = null,
        object? metadata = null,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        var ip = ipAddress
                 ?? http.HttpContext?.Connection.RemoteIpAddress?.ToString();

        db.AuditLogsSet.Add(new AuditLog
        {
            UserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            IpAddress = ip,
            MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata)
        });
        await db.SaveChangesAsync(ct);
    }
}

public class TenantAccess(AppDbContext db) : ITenantAccess
{
    public async Task<Guid?> GetDefaultOrganizationIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.OrganizationMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Role == OrganizationRoles.Owner ? 0 : m.Role == OrganizationRoles.Admin ? 1 : 2)
            .ThenBy(m => m.CreatedAtUtc)
            .Select(m => (Guid?)m.OrganizationId)
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> IsMemberAsync(Guid userId, Guid organizationId, CancellationToken ct = default) =>
        db.OrganizationMembers.AnyAsync(m => m.UserId == userId && m.OrganizationId == organizationId, ct);

    public async Task EnsureCanAccessProjectAsync(Guid userId, Project project, CancellationToken ct = default)
    {
        if (project.UserId == userId) return;
        if (project.OrganizationId is Guid orgId && await IsMemberAsync(userId, orgId, ct)) return;
        throw new UnauthorizedAccessException("Not a member of this project's organization.");
    }

    public async Task<IReadOnlyList<Guid>> GetAccessibleOrganizationIdsAsync(Guid userId, CancellationToken ct = default) =>
        await db.OrganizationMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.OrganizationId)
            .ToListAsync(ct);
}

/// <summary>Advances Trial→PastDue/Canceled and Active→PastDue→Canceled (grace / dunning).</summary>
public class SubscriptionDunningWorker(IServiceScopeFactory scopes, ILogger<SubscriptionDunningWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
                var now = DateTime.UtcNow;
                var changed = 0;

                var due = await db.Subscriptions
                    .Include(s => s.Plan)
                    .Where(s => s.Status == SubscriptionStatuses.Active || s.Status == SubscriptionStatuses.Trial)
                    .Where(s =>
                        (s.CancelAtPeriodEnd && s.CurrentPeriodEndUtc != null && s.CurrentPeriodEndUtc <= now) ||
                        (s.Status == SubscriptionStatuses.Trial && s.TrialEndsUtc != null && s.TrialEndsUtc <= now) ||
                        (s.Status == SubscriptionStatuses.Active && s.CurrentPeriodEndUtc != null && s.CurrentPeriodEndUtc <= now && !s.CancelAtPeriodEnd))
                    .ToListAsync(stoppingToken);

                foreach (var sub in due)
                {
                    if (sub.CancelAtPeriodEnd && sub.CurrentPeriodEndUtc <= now)
                    {
                        sub.Status = SubscriptionStatuses.Canceled;
                        sub.CanceledAtUtc ??= now;
                        changed++;
                        await audit.WriteAsync("subscription.canceled", sub.UserId, "Subscription", sub.Id,
                            new { reason = "period_end" }, ct: stoppingToken);
                        continue;
                    }

                    if (sub.Status == SubscriptionStatuses.Trial && sub.TrialEndsUtc <= now)
                    {
                        if (sub.Plan.MonthlyPriceInr <= 0)
                        {
                            sub.Status = SubscriptionStatuses.Active;
                            sub.CurrentPeriodEndUtc = now.AddDays(30);
                            sub.TrialEndsUtc = null;
                            changed++;
                            await audit.WriteAsync("subscription.activated", sub.UserId, "Subscription", sub.Id,
                                new { reason = "trial_to_free_active" }, ct: stoppingToken);
                        }
                        else
                        {
                            sub.Status = SubscriptionStatuses.PastDue;
                            sub.GracePeriodEndsUtc = now.AddDays(7);
                            sub.DunningAttempts++;
                            changed++;
                            await audit.WriteAsync("subscription.past_due", sub.UserId, "Subscription", sub.Id,
                                new { reason = "trial_ended" }, ct: stoppingToken);
                        }
                        continue;
                    }

                    if (sub.Status == SubscriptionStatuses.Active && sub.CurrentPeriodEndUtc <= now && !sub.CancelAtPeriodEnd)
                    {
                        if (sub.Plan.MonthlyPriceInr <= 0)
                        {
                            sub.CurrentPeriodEndUtc = now.AddDays(30);
                            changed++;
                            continue;
                        }

                        sub.Status = SubscriptionStatuses.PastDue;
                        sub.GracePeriodEndsUtc = now.AddDays(7);
                        sub.DunningAttempts++;
                        changed++;
                        await audit.WriteAsync("subscription.past_due", sub.UserId, "Subscription", sub.Id,
                            new { reason = "period_lapsed", attempt = sub.DunningAttempts }, ct: stoppingToken);
                    }
                }

                var graceExpired = await db.Subscriptions
                    .Where(s => s.Status == SubscriptionStatuses.PastDue && s.GracePeriodEndsUtc != null && s.GracePeriodEndsUtc <= now)
                    .ToListAsync(stoppingToken);

                foreach (var sub in graceExpired)
                {
                    sub.Status = SubscriptionStatuses.Canceled;
                    sub.CanceledAtUtc ??= now;
                    changed++;
                    await audit.WriteAsync("subscription.canceled", sub.UserId, "Subscription", sub.Id,
                        new { reason = "dunning_grace_expired", attempts = sub.DunningAttempts }, ct: stoppingToken);
                }

                if (changed > 0)
                    await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Subscription dunning tick failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
}

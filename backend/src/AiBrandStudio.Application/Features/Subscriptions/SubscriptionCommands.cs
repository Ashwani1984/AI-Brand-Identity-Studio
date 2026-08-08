using AiBrandStudio.Application.Common;
using AiBrandStudio.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiBrandStudio.Application.Features.Subscriptions;

public record GetMySubscriptionQuery() : IRequest<SubscriptionDto?>;
public record CancelSubscriptionCommand(bool Immediately = false) : IRequest<SubscriptionDto>;
public record RenewSubscriptionCommand() : IRequest<SubscriptionDto>;

public class GetMySubscriptionHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<GetMySubscriptionQuery, SubscriptionDto?>
{
    public async Task<SubscriptionDto?> Handle(GetMySubscriptionQuery request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        return await db.Subscriptions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new SubscriptionDto(
                s.Id,
                s.Status,
                s.Plan.Code,
                s.Plan.Name,
                s.TrialEndsUtc,
                s.CurrentPeriodEndUtc,
                s.CancelAtPeriodEnd,
                s.GracePeriodEndsUtc,
                s.OrganizationId))
            .FirstOrDefaultAsync(cancellationToken);
    }
}

public class CancelSubscriptionHandler(IAppDbContext db, ICurrentUser current, IAuditService audit)
    : IRequestHandler<CancelSubscriptionCommand, SubscriptionDto>
{
    public async Task<SubscriptionDto> Handle(CancelSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var sub = await db.Subscriptions.Include(s => s.Plan)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("No subscription found.");

        if (sub.Status == SubscriptionStatuses.Canceled)
            throw new InvalidOperationException("Subscription is already canceled.");

        if (request.Immediately)
        {
            sub.Status = SubscriptionStatuses.Canceled;
            sub.CanceledAtUtc = DateTime.UtcNow;
            sub.CancelAtPeriodEnd = false;
        }
        else
        {
            sub.CancelAtPeriodEnd = true;
            sub.CanceledAtUtc = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(
            request.Immediately ? "subscription.canceled" : "subscription.cancel_scheduled",
            userId,
            "Subscription",
            sub.Id,
            new { immediate = request.Immediately, sub.CurrentPeriodEndUtc },
            ct: cancellationToken);

        return ToDto(sub);
    }

    internal static SubscriptionDto ToDto(Subscription s) => new(
        s.Id, s.Status, s.Plan.Code, s.Plan.Name, s.TrialEndsUtc, s.CurrentPeriodEndUtc,
        s.CancelAtPeriodEnd, s.GracePeriodEndsUtc, s.OrganizationId);
}

public class RenewSubscriptionHandler(IAppDbContext db, ICurrentUser current, IAuditService audit)
    : IRequestHandler<RenewSubscriptionCommand, SubscriptionDto>
{
    public async Task<SubscriptionDto> Handle(RenewSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var sub = await db.Subscriptions.Include(s => s.Plan)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("No subscription found.");

        if (sub.Status == SubscriptionStatuses.Canceled && sub.Plan.MonthlyPriceInr > 0)
            throw new InvalidOperationException("Canceled paid plans require a new payment to renew.");

        sub.CancelAtPeriodEnd = false;
        sub.CanceledAtUtc = null;
        sub.GracePeriodEndsUtc = null;
        sub.DunningAttempts = 0;

        if (sub.Status is SubscriptionStatuses.PastDue or SubscriptionStatuses.Canceled)
        {
            if (sub.Plan.MonthlyPriceInr > 0)
                throw new InvalidOperationException("Past-due paid plans require payment capture to renew.");

            sub.Status = SubscriptionStatuses.Active;
            sub.CurrentPeriodEndUtc = DateTime.UtcNow.AddDays(30);
        }
        else if (sub.Status == SubscriptionStatuses.Trial)
        {
            // Keep trial; clear cancel schedule only
        }
        else
        {
            sub.Status = SubscriptionStatuses.Active;
        }

        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("subscription.renewed", userId, "Subscription", sub.Id,
            new { sub.Status, sub.CurrentPeriodEndUtc }, ct: cancellationToken);

        return CancelSubscriptionHandler.ToDto(sub);
    }
}

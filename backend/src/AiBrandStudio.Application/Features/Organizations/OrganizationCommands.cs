using System.Text.RegularExpressions;
using AiBrandStudio.Application.Common;
using AiBrandStudio.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiBrandStudio.Application.Features.Organizations;

public record CreateOrganizationCommand(string Name) : IRequest<OrganizationDto>;
public record ListOrganizationsQuery() : IRequest<IReadOnlyList<OrganizationDto>>;
public record GetOrganizationQuery(Guid Id) : IRequest<OrganizationDto>;
public record ListOrganizationMembersQuery(Guid OrganizationId) : IRequest<IReadOnlyList<OrganizationMemberDto>>;
public record InviteOrganizationMemberCommand(Guid OrganizationId, string Email, string Role) : IRequest<OrganizationMemberDto>;
public record RemoveOrganizationMemberCommand(Guid OrganizationId, Guid UserId) : IRequest;

public class CreateOrganizationValidator : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
}

public static class OrganizationSlug
{
    public static string FromName(string name)
    {
        var slug = Regex.Replace(name.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(slug)) slug = "org";
        if (slug.Length > 60) slug = slug[..60];
        var candidate = $"{slug}-{Guid.NewGuid():N}";
        return candidate.Length <= 80 ? candidate : candidate[..80];
    }
}

public class CreateOrganizationHandler(IAppDbContext db, ICurrentUser current, IAuditService audit)
    : IRequestHandler<CreateOrganizationCommand, OrganizationDto>
{
    public async Task<OrganizationDto> Handle(CreateOrganizationCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var org = new Organization
        {
            Name = request.Name.Trim(),
            Slug = OrganizationSlug.FromName(request.Name),
            CreditBalance = 0
        };
        db.Add(org);
        db.Add(new OrganizationMember
        {
            OrganizationId = org.Id,
            UserId = userId,
            Role = OrganizationRoles.Owner,
            Organization = org
        });
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("organization.created", userId, "Organization", org.Id, new { org.Name }, ct: cancellationToken);
        return new OrganizationDto(org.Id, org.Name, org.Slug, org.CreditBalance, OrganizationRoles.Owner, org.CreatedAtUtc);
    }
}

public class ListOrganizationsHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<ListOrganizationsQuery, IReadOnlyList<OrganizationDto>>
{
    public async Task<IReadOnlyList<OrganizationDto>> Handle(ListOrganizationsQuery request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        return await db.OrganizationMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new OrganizationDto(
                m.Organization.Id,
                m.Organization.Name,
                m.Organization.Slug,
                m.Organization.CreditBalance,
                m.Role,
                m.Organization.CreatedAtUtc))
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);
    }
}

public class GetOrganizationHandler(IAppDbContext db, ICurrentUser current, ITenantAccess tenant)
    : IRequestHandler<GetOrganizationQuery, OrganizationDto>
{
    public async Task<OrganizationDto> Handle(GetOrganizationQuery request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        if (!await tenant.IsMemberAsync(userId, request.Id, cancellationToken))
            throw new UnauthorizedAccessException("Not a member of this organization.");

        var m = await db.OrganizationMembers.AsNoTracking()
            .Where(x => x.OrganizationId == request.Id && x.UserId == userId)
            .Select(x => new OrganizationDto(
                x.Organization.Id,
                x.Organization.Name,
                x.Organization.Slug,
                x.Organization.CreditBalance,
                x.Role,
                x.Organization.CreatedAtUtc))
            .FirstAsync(cancellationToken);
        return m;
    }
}

public class ListOrganizationMembersHandler(IAppDbContext db, ICurrentUser current, ITenantAccess tenant)
    : IRequestHandler<ListOrganizationMembersQuery, IReadOnlyList<OrganizationMemberDto>>
{
    public async Task<IReadOnlyList<OrganizationMemberDto>> Handle(ListOrganizationMembersQuery request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        if (!await tenant.IsMemberAsync(userId, request.OrganizationId, cancellationToken))
            throw new UnauthorizedAccessException("Not a member of this organization.");

        return await db.OrganizationMembers.AsNoTracking()
            .Where(m => m.OrganizationId == request.OrganizationId)
            .Select(m => new OrganizationMemberDto(m.UserId, m.User.Email, m.User.FullName, m.Role, m.CreatedAtUtc))
            .OrderBy(m => m.Role)
            .ThenBy(m => m.Email)
            .ToListAsync(cancellationToken);
    }
}

public class InviteOrganizationMemberHandler(IAppDbContext db, ICurrentUser current, IAuditService audit)
    : IRequestHandler<InviteOrganizationMemberCommand, OrganizationMemberDto>
{
    public async Task<OrganizationMemberDto> Handle(InviteOrganizationMemberCommand request, CancellationToken cancellationToken)
    {
        var actorId = current.UserId ?? throw new UnauthorizedAccessException();
        var actor = await db.OrganizationMembers.FirstOrDefaultAsync(
            m => m.OrganizationId == request.OrganizationId && m.UserId == actorId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Not a member of this organization.");

        if (actor.Role is not (OrganizationRoles.Owner or OrganizationRoles.Admin))
            throw new UnauthorizedAccessException("Only owners/admins can invite members.");

        var role = string.IsNullOrWhiteSpace(request.Role) ? OrganizationRoles.Member : request.Role.Trim();
        if (role is not (OrganizationRoles.Admin or OrganizationRoles.Member))
            throw new ArgumentException("Role must be Admin or Member.");

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken)
            ?? throw new KeyNotFoundException("User not found. They must register first.");

        if (await db.OrganizationMembers.AnyAsync(m => m.OrganizationId == request.OrganizationId && m.UserId == user.Id, cancellationToken))
            throw new InvalidOperationException("User is already a member.");

        var member = new OrganizationMember
        {
            OrganizationId = request.OrganizationId,
            UserId = user.Id,
            Role = role
        };
        db.Add(member);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("organization.member_added", actorId, "Organization", request.OrganizationId,
            new { user.Id, user.Email, role }, ct: cancellationToken);

        return new OrganizationMemberDto(user.Id, user.Email, user.FullName, role, member.CreatedAtUtc);
    }
}

public class RemoveOrganizationMemberHandler(IAppDbContext db, ICurrentUser current, IAuditService audit)
    : IRequestHandler<RemoveOrganizationMemberCommand>
{
    public async Task Handle(RemoveOrganizationMemberCommand request, CancellationToken cancellationToken)
    {
        var actorId = current.UserId ?? throw new UnauthorizedAccessException();
        var actor = await db.OrganizationMembers.FirstOrDefaultAsync(
            m => m.OrganizationId == request.OrganizationId && m.UserId == actorId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Not a member of this organization.");

        if (actor.Role is not (OrganizationRoles.Owner or OrganizationRoles.Admin) && actorId != request.UserId)
            throw new UnauthorizedAccessException("Only owners/admins can remove members.");

        var target = await db.OrganizationMembers.FirstOrDefaultAsync(
            m => m.OrganizationId == request.OrganizationId && m.UserId == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Member not found.");

        if (target.Role == OrganizationRoles.Owner)
            throw new InvalidOperationException("Cannot remove the organization owner.");

        db.Remove(target);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("organization.member_removed", actorId, "Organization", request.OrganizationId,
            new { request.UserId }, ct: cancellationToken);
    }
}

/// <summary>Creates a personal workspace org + trial subscription for a new user.</summary>
public static class PersonalOrgBootstrap
{
    public static async Task<(Organization Org, Subscription Sub)> CreateForUserAsync(
        IAppDbContext db,
        User user,
        CancellationToken ct)
    {
        var org = new Organization
        {
            Name = $"{user.FullName}'s Workspace",
            Slug = OrganizationSlug.FromName(user.FullName),
            CreditBalance = 50
        };
        db.Add(org);
        db.Add(new OrganizationMember
        {
            Organization = org,
            OrganizationId = org.Id,
            UserId = user.Id,
            Role = OrganizationRoles.Owner
        });

        var freePlan = await db.SubscriptionPlans.FirstAsync(p => p.Code == "free", ct);
        var sub = new Subscription
        {
            UserId = user.Id,
            OrganizationId = org.Id,
            PlanId = freePlan.Id,
            Status = SubscriptionStatuses.Trial,
            TrialEndsUtc = DateTime.UtcNow.AddDays(14),
            CurrentPeriodEndUtc = DateTime.UtcNow.AddDays(14),
            CancelAtPeriodEnd = false
        };
        db.Add(sub);
        await db.SaveChangesAsync(ct);
        return (org, sub);
    }
}

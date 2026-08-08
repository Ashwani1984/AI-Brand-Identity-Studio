using AiBrandStudio.Application.Common;
using AiBrandStudio.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiBrandStudio.Application.Features.Projects;

public record CreateProjectCommand(string Name, Guid? OrganizationId = null) : IRequest<ProjectDto>;
public record GetProjectsQuery(Guid? OrganizationId = null) : IRequest<IReadOnlyList<ProjectDto>>;
public record GetProjectQuery(Guid Id) : IRequest<ProjectDto>;
public record UpdateProjectCommand(Guid Id, string Name, string Status) : IRequest<ProjectDto>;
public record DeleteProjectCommand(Guid Id) : IRequest;

public class CreateProjectValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
}

public class CreateProjectHandler(IAppDbContext db, ICurrentUser current, ITenantAccess tenant)
    : IRequestHandler<CreateProjectCommand, ProjectDto>
{
    public async Task<ProjectDto> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var orgId = request.OrganizationId ?? await tenant.GetDefaultOrganizationIdAsync(userId, cancellationToken);
        if (orgId is Guid oid && !await tenant.IsMemberAsync(userId, oid, cancellationToken))
            throw new UnauthorizedAccessException("Not a member of this organization.");

        var project = new Project
        {
            UserId = userId,
            OrganizationId = orgId,
            Name = request.Name.Trim(),
            Status = "Draft"
        };
        db.Add(project);
        await db.SaveChangesAsync(cancellationToken);
        return new ProjectDto(project.Id, project.Name, project.Status, project.CreatedAtUtc, null, null, project.OrganizationId);
    }
}

public class GetProjectsHandler(IAppDbContext db, ICurrentUser current, ITenantAccess tenant)
    : IRequestHandler<GetProjectsQuery, IReadOnlyList<ProjectDto>>
{
    public async Task<IReadOnlyList<ProjectDto>> Handle(GetProjectsQuery request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var orgIds = await tenant.GetAccessibleOrganizationIdsAsync(userId, cancellationToken);

        var q = db.Projects.AsNoTracking().Include(p => p.Brand).AsQueryable();
        if (request.OrganizationId is Guid filterOrg)
        {
            if (!orgIds.Contains(filterOrg) && !current.IsAdmin)
                throw new UnauthorizedAccessException("Not a member of this organization.");
            q = q.Where(p => p.OrganizationId == filterOrg);
        }
        else
        {
            q = q.Where(p => p.UserId == userId || (p.OrganizationId != null && orgIds.Contains(p.OrganizationId.Value)));
        }

        return await q.OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new ProjectDto(
                p.Id, p.Name, p.Status, p.CreatedAtUtc,
                p.Brand != null ? p.Brand.Id : null,
                p.Brand != null ? p.Brand.BusinessName : null,
                p.OrganizationId))
            .ToListAsync(cancellationToken);
    }
}

public class GetProjectHandler(IAppDbContext db, ICurrentUser current, ITenantAccess tenant)
    : IRequestHandler<GetProjectQuery, ProjectDto>
{
    public async Task<ProjectDto> Handle(GetProjectQuery request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var p = await db.Projects.AsNoTracking().Include(x => x.Brand)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Project not found.");
        await tenant.EnsureCanAccessProjectAsync(userId, p, cancellationToken);
        return new ProjectDto(p.Id, p.Name, p.Status, p.CreatedAtUtc, p.Brand?.Id, p.Brand?.BusinessName, p.OrganizationId);
    }
}

public class UpdateProjectHandler(IAppDbContext db, ICurrentUser current, ITenantAccess tenant)
    : IRequestHandler<UpdateProjectCommand, ProjectDto>
{
    public async Task<ProjectDto> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var p = await db.Projects.Include(x => x.Brand)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Project not found.");
        await tenant.EnsureCanAccessProjectAsync(userId, p, cancellationToken);
        p.Name = request.Name.Trim();
        p.Status = request.Status;
        p.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new ProjectDto(p.Id, p.Name, p.Status, p.CreatedAtUtc, p.Brand?.Id, p.Brand?.BusinessName, p.OrganizationId);
    }
}

public class DeleteProjectHandler(IAppDbContext db, ICurrentUser current, ITenantAccess tenant)
    : IRequestHandler<DeleteProjectCommand>
{
    public async Task Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Project not found.");
        await tenant.EnsureCanAccessProjectAsync(userId, p, cancellationToken);
        db.Remove(p);
        await db.SaveChangesAsync(cancellationToken);
    }
}

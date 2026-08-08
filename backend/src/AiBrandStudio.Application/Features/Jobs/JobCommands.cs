using System.Text.Json;
using AiBrandStudio.Application.Common;
using AiBrandStudio.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AiBrandStudio.Application.Features.Jobs;

public record EnqueueLogoJobCommand(Guid BrandId, string? Style) : IRequest<GenerationJobDto>;
public record EnqueueMockupJobCommand(Guid BrandId, string? MockupType) : IRequest<GenerationJobDto>;
public record EnqueueExportJobCommand(Guid ProjectId, string? PackageType) : IRequest<GenerationJobDto>;
public record GetGenerationJobQuery(Guid JobId) : IRequest<GenerationJobDto>;

public class EnqueueLogoJobHandler(
    IAppDbContext db,
    ICurrentUser current,
    IGenerationJobQueue queue) : IRequestHandler<EnqueueLogoJobCommand, GenerationJobDto>
{
    public async Task<GenerationJobDto> Handle(EnqueueLogoJobCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        _ = await db.Brands.AsNoTracking().Include(b => b.Project)
            .FirstOrDefaultAsync(b => b.Id == request.BrandId && b.Project.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Brand not found.");

        return await JobEnqueue.EnqueueAsync(db, queue, userId, "LogoGenerate",
            JsonSerializer.Serialize(new { request.BrandId, Style = request.Style ?? "Minimal" }), cancellationToken);
    }
}

public class EnqueueMockupJobHandler(
    IAppDbContext db,
    ICurrentUser current,
    IGenerationJobQueue queue) : IRequestHandler<EnqueueMockupJobCommand, GenerationJobDto>
{
    public async Task<GenerationJobDto> Handle(EnqueueMockupJobCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        _ = await db.Brands.AsNoTracking().Include(b => b.Project)
            .FirstOrDefaultAsync(b => b.Id == request.BrandId && b.Project.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Brand not found.");

        return await JobEnqueue.EnqueueAsync(db, queue, userId, "MockupGenerate",
            JsonSerializer.Serialize(new { request.BrandId, MockupType = request.MockupType ?? "BusinessCard" }), cancellationToken);
    }
}

public class EnqueueExportJobHandler(
    IAppDbContext db,
    ICurrentUser current,
    IGenerationJobQueue queue) : IRequestHandler<EnqueueExportJobCommand, GenerationJobDto>
{
    public async Task<GenerationJobDto> Handle(EnqueueExportJobCommand request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        _ = await db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Project not found.");

        return await JobEnqueue.EnqueueAsync(db, queue, userId, "ExportZip",
            JsonSerializer.Serialize(new { request.ProjectId, request.PackageType }), cancellationToken);
    }
}

public class GetGenerationJobHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<GetGenerationJobQuery, GenerationJobDto>
{
    public async Task<GenerationJobDto> Handle(GetGenerationJobQuery request, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedAccessException();
        var job = await db.GenerationJobs.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == request.JobId && j.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Job not found.");
        return JobEnqueue.Map(job);
    }
}

file static class JobEnqueue
{
    public static async Task<GenerationJobDto> EnqueueAsync(
        IAppDbContext db,
        IGenerationJobQueue queue,
        Guid userId,
        string jobType,
        string payloadJson,
        CancellationToken ct)
    {
        var job = new GenerationJob
        {
            UserId = userId,
            JobType = jobType,
            Status = "Queued",
            PayloadJson = payloadJson
        };
        db.Add(job);
        await db.SaveChangesAsync(ct);
        await queue.EnqueueAsync(job.Id, ct);
        return Map(job);
    }

    public static GenerationJobDto Map(GenerationJob job) =>
        new(job.Id, job.JobType, job.Status, job.ResultJson, job.Error, job.CreatedAtUtc, job.CompletedAtUtc);
}

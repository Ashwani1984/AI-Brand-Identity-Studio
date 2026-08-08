using System.Text.Json;
using System.Threading.Channels;
using AiBrandStudio.Application.Common;
using AiBrandStudio.Application.Features.Generators;
using AiBrandStudio.Application.Features.Modules;
using AiBrandStudio.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiBrandStudio.Infrastructure.Jobs;

public class ChannelGenerationJobQueue : IGenerationJobQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = false
    });

    public ValueTask EnqueueAsync(Guid jobId, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(jobId, ct);

    public async IAsyncEnumerable<Guid> DequeueAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var id in _channel.Reader.ReadAllAsync(ct))
            yield return id;
    }
}

public class GenerationJobWorker(
    IServiceScopeFactory scopeFactory,
    IGenerationJobQueue queue,
    ILogger<GenerationJobWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverInterruptedJobsAsync(stoppingToken);
        await Task.WhenAll(
            ProcessChannelAsync(stoppingToken),
            PollQueuedJobsAsync(stoppingToken));
    }

    private async Task RecoverInterruptedJobsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stuck = await db.GenerationJobs.Where(j => j.Status == "Running").ToListAsync(ct);
        foreach (var job in stuck)
            job.Status = "Queued";
        if (stuck.Count > 0)
            await db.SaveChangesAsync(ct);

        var queued = await db.GenerationJobs.AsNoTracking()
            .Where(j => j.Status == "Queued")
            .OrderBy(j => j.CreatedAtUtc)
            .Select(j => j.Id)
            .ToListAsync(ct);
        foreach (var id in queued)
            await queue.EnqueueAsync(id, ct);
    }

    private async Task ProcessChannelAsync(CancellationToken ct)
    {
        await foreach (var jobId in queue.DequeueAllAsync(ct))
        {
            try { await ProcessJobAsync(jobId, ct); }
            catch (Exception ex) { logger.LogError(ex, "Unhandled error processing generation job {JobId}", jobId); }
        }
    }

    private async Task PollQueuedJobsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var ids = await db.GenerationJobs.AsNoTracking()
                    .Where(j => j.Status == "Queued")
                    .OrderBy(j => j.CreatedAtUtc)
                    .Take(20)
                    .Select(j => j.Id)
                    .ToListAsync(ct);
                foreach (var id in ids)
                    await queue.EnqueueAsync(id, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Generation job poll failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var notifier = scope.ServiceProvider.GetRequiredService<IGenerationNotifier>();

        var claimed = await db.GenerationJobs
            .Where(j => j.Id == jobId && j.Status == "Queued")
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, "Running")
                .SetProperty(j => j.StartedAtUtc, DateTime.UtcNow), ct);
        if (claimed == 0)
            return;

        var job = await db.GenerationJobs.FirstAsync(j => j.Id == jobId, ct);
        await notifier.NotifyUserAsync(job.UserId, new { jobId = job.Id, jobType = job.JobType, stage = "started" }, ct);

        JobUserAmbient.UserId = job.UserId;
        try
        {
            object result = job.JobType switch
            {
                "LogoGenerate" => await RunLogoAsync(mediator, job.PayloadJson, ct),
                "MockupGenerate" => await RunMockupAsync(mediator, job.PayloadJson, ct),
                "ExportZip" => await RunExportAsync(mediator, job.PayloadJson, ct),
                _ => throw new InvalidOperationException($"Unknown job type '{job.JobType}'.")
            };

            job.Status = "Completed";
            job.ResultJson = JsonSerializer.Serialize(result, JsonOpts);
            job.CompletedAtUtc = DateTime.UtcNow;
            job.Error = null;
            await db.SaveChangesAsync(ct);
            await notifier.NotifyUserAsync(job.UserId, new { jobId = job.Id, jobType = job.JobType, stage = "completed", result }, ct);
        }
        catch (Exception ex)
        {
            job.Status = "Failed";
            job.Error = ex.Message;
            job.CompletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await notifier.NotifyUserAsync(job.UserId, new { jobId = job.Id, jobType = job.JobType, stage = "failed", error = ex.Message }, ct);
            logger.LogWarning(ex, "Generation job {JobId} failed", jobId);
        }
        finally
        {
            JobUserAmbient.UserId = null;
        }
    }

    private static async Task<object> RunLogoAsync(IMediator mediator, string payloadJson, CancellationToken ct)
    {
        var p = JsonSerializer.Deserialize<LogoPayload>(payloadJson)
                ?? throw new InvalidOperationException("Invalid logo payload.");
        return await mediator.Send(new GenerateLogoCommand(p.BrandId, p.Style ?? "Minimal"), ct);
    }

    private static async Task<object> RunMockupAsync(IMediator mediator, string payloadJson, CancellationToken ct)
    {
        var p = JsonSerializer.Deserialize<MockupPayload>(payloadJson)
                ?? throw new InvalidOperationException("Invalid mockup payload.");
        return await mediator.Send(new GenerateMockupCommand(p.BrandId, p.MockupType ?? "BusinessCard"), ct);
    }

    private static async Task<object> RunExportAsync(IMediator mediator, string payloadJson, CancellationToken ct)
    {
        var p = JsonSerializer.Deserialize<ExportPayload>(payloadJson)
                ?? throw new InvalidOperationException("Invalid export payload.");
        return await mediator.Send(new ExportZipCommand(p.ProjectId, p.PackageType), ct);
    }

    private record LogoPayload(Guid BrandId, string? Style);
    private record MockupPayload(Guid BrandId, string? MockupType);
    private record ExportPayload(Guid ProjectId, string? PackageType);
}

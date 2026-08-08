using System.Diagnostics;
using System.Threading.RateLimiting;
using AiBrandStudio.Application;
using AiBrandStudio.Application.Common;
using AiBrandStudio.Infrastructure;
using AiBrandStudio.Infrastructure.Persistence;
using AiBrandStudio.Infrastructure.Services;
using AiBrandStudio.Api.Hubs;
using AiBrandStudio.Api.Observability;
using AiBrandStudio.Api.Services;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddSingleton<IGenerationNotifier, SignalRGenerationNotifier>();

var redis = builder.Configuration.GetConnectionString("Redis");
var signalRBuilder = builder.Services.AddSignalR();
if (!string.IsNullOrWhiteSpace(redis))
    signalRBuilder.AddStackExchangeRedis(redis);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddCors(o => o.AddPolicy("web", p =>
    p.WithOrigins(builder.Configuration["Cors:Origins"]?.Split(',') ?? ["http://localhost:3000"])
        .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 30;
        opt.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = feature?.Error;

        var (statusCode, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            PaymentSignatureException => (StatusCodes.Status400BadRequest, "Invalid payment signature"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            PaymentsNotConfiguredException => (StatusCodes.Status503ServiceUnavailable, "PAYMENTS_NOT_CONFIGURED"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Request conflict"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error")
        };

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = app.Environment.IsDevelopment() ? exception?.Message : null,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = traceId;

        if (exception is ValidationException validationException)
        {
            problem.Extensions["errors"] = validationException.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
        }

        if (statusCode >= 500 && exception is not null)
            SentrySdk.CaptureException(exception);

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(problem);
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();
app.UseCors("web");
app.UseAuthentication();
app.UseAuthorization();

var storageRoot = Path.GetFullPath(app.Configuration["Storage:LocalPath"] ?? "./storage");
Directory.CreateDirectory(storageRoot);

app.MapControllers();
app.MapHub<GenerationHub>("/hubs/generation");
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "AiBrandStudio.Api",
    traceId = Activity.Current?.Id
}));
app.MapGet("/health/ready", async (IServiceProvider services, CancellationToken ct) =>
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var canConnect = await db.Database.CanConnectAsync(ct);
    return canConnect
        ? Results.Ok(new { status = "ready", database = "ok", traceId = Activity.Current?.Id })
        : Results.Problem("Database connection failed.", statusCode: StatusCodes.Status503ServiceUnavailable);
});

await SeedData.EnsureSeedAsync(app.Services);

app.Run();

public partial class Program;

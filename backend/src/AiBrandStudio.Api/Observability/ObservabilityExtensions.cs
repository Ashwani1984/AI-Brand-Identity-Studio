using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AiBrandStudio.Api.Observability;

public static class ObservabilityExtensions
{
    public const string ServiceName = "AiBrandStudio.Api";

    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        if (!builder.Configuration.GetValue("Observability:Enabled", true))
            return builder;

        var aiConnection =
            builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
            ?? builder.Configuration["ApplicationInsights:ConnectionString"];

        var resource = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: ServiceName,
                serviceVersion: typeof(ObservabilityExtensions).Assembly.GetName().Version?.ToString() ?? "1.0.0");

        if (!string.IsNullOrWhiteSpace(aiConnection))
        {
            builder.Services.AddOpenTelemetry().UseAzureMonitor(o => o.ConnectionString = aiConnection);
        }
        else if (builder.Environment.IsDevelopment() || builder.Configuration.GetValue("Observability:ConsoleExporter", false))
        {
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(r => r.AddService(ServiceName))
                .WithTracing(t => t
                    .SetResourceBuilder(resource)
                    .AddAspNetCoreInstrumentation(o =>
                    {
                        o.RecordException = true;
                        o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
                    })
                    .AddHttpClientInstrumentation()
                    .AddConsoleExporter())
                .WithMetrics(m => m
                    .SetResourceBuilder(resource)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddConsoleExporter());
        }

        var sentryDsn = builder.Configuration["Sentry:Dsn"];
        if (!string.IsNullOrWhiteSpace(sentryDsn))
        {
            builder.WebHost.UseSentry(o =>
            {
                o.Dsn = sentryDsn;
                o.Environment = builder.Environment.EnvironmentName;
                o.Release = builder.Configuration["Sentry:Release"]
                            ?? $"aibrand-api@{Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "local"}";
                o.TracesSampleRate = builder.Configuration.GetValue("Sentry:TracesSampleRate", 0.1);
                o.SendDefaultPii = false;
            });
        }

        return builder;
    }
}

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Listmate.Api.Hosting;

internal static class Observability
{
    private const string ReadyTag = "ready";

    public static IHealthChecksBuilder AddReadyCheck<TContext>(this IHealthChecksBuilder health)
        where TContext : Microsoft.EntityFrameworkCore.DbContext =>
        health.AddCheck("self", () => HealthCheckResult.Healthy()).AddDbContextCheck<TContext>(tags: [ReadyTag]);

    /// <summary>/health/live answers while the process runs; /health/ready also checks the database.</summary>
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = c => c.Name == "self" });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Name == "self" || c.Tags.Contains(ReadyTag) });
        return app;
    }

    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(o =>
        {
            o.IncludeFormattedMessage = true;
            o.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("listmate-api"))
            .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddRuntimeInstrumentation().AddMeter("Microsoft.AspNetCore.Http.Connections"))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
                .AddSource("Npgsql", "Microsoft.AspNetCore.SignalR.Server"));

        // Export only when an OTLP collector (e.g. the Aspire dashboard) is configured.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }
}

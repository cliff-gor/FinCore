using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Shared service defaults for every FinCore host: OpenTelemetry (traces, metrics, logs), health checks,
/// service discovery and HTTP resilience (spec §27).
/// </summary>
public static class Extensions
{
    /// <summary>Activity sources and meters emitted by FinCore code.</summary>
    public static readonly string[] FinCoreTelemetrySources = ["FinCore.Outbox", "FinCore.Api", "FinCore.Bff"];

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        var entry = Assembly.GetEntryAssembly();
        var version = entry?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(
                serviceName: builder.Environment.ApplicationName,
                serviceVersion: version.Split('+')[0]))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(FinCoreTelemetrySources))
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                .AddSource(FinCoreTelemetrySources)
                .AddSource("Npgsql")
                .AddAspNetCoreInstrumentation(o =>
                    o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
                .AddHttpClientInstrumentation());

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        return builder;
    }

    /// <summary>
    /// <c>/health</c> and <c>/health/live</c>: process is up. <c>/health/ready</c>: dependencies tagged <c>ready</c>.
    /// Anonymous in every environment (spec 001).
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var live = new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") };
        app.MapHealthChecks("/health", live).AllowAnonymous().DisableHttpMetrics();
        app.MapHealthChecks("/health/live", live).AllowAnonymous().DisableHttpMetrics();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") })
            .AllowAnonymous()
            .DisableHttpMetrics();

        return app;
    }
}

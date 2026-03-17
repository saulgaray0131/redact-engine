using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            // http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        builder.AddKeyVaultIfConfigured();

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        // Suppress Npgsql verbose/debug logs from being exported to Application Insights.
        // Belt-and-suspenders: appsettings already sets Npgsql to Warning, but this ensures
        // the OpenTelemetry logging provider specifically respects that even if overridden.
        builder.Logging.AddFilter<OpenTelemetryLoggerProvider>("Npgsql", LogLevel.Warning);

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation()
                    .SetSampler(new NpgsqlFilteringSampler(
                        new ParentBasedSampler(new AlwaysOnSampler())));
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            builder.Services.AddOpenTelemetry()
               .UseAzureMonitor();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Health endpoints must be available in ALL environments.
        // Container Apps liveness/readiness/startup probes depend on these.
        // These are internal-only endpoints on the container network, not publicly routable.

        // All health checks must pass for app to be considered ready to accept traffic after starting
        app.MapHealthChecks(HealthEndpointPath);

        // Only health checks tagged with the "live" tag must pass for app to be considered alive
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return app;
    }

    // Add keyvault
    public static TBuilder AddKeyVaultIfConfigured<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var keyVaultEndpoint = builder.Configuration.GetConnectionString("keyvault");
        if (!string.IsNullOrEmpty(keyVaultEndpoint))
        {
            builder.Configuration.AddAzureKeyVault(new Uri(keyVaultEndpoint), new DefaultAzureCredential());
        }
        return builder;
    }
}

/// <summary>
/// Custom sampler that drops all Npgsql spans to prevent Azure Monitor from converting
/// Npgsql Activity events (e.g. "received-first-response") into verbose trace telemetry
/// that floods Application Insights and burns through the daily ingestion cap.
/// Primary suppression is via Aspire's DisableTracing setting; this sampler is a safety net.
/// </summary>
internal sealed class NpgsqlFilteringSampler(Sampler innerSampler) : Sampler
{
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        // Drop ALL Npgsql spans — both root-level (health checks, Hangfire polling)
        // and child spans within request traces. Azure Monitor exports each Activity.Event
        // as a separate trace telemetry item; dropping the span prevents event export.
        if (IsPostgresSpan(samplingParameters))
        {
            return new SamplingResult(SamplingDecision.Drop);
        }

        return innerSampler.ShouldSample(samplingParameters);
    }

    private static bool IsPostgresSpan(in SamplingParameters samplingParameters)
    {
        // Match by span name (Npgsql emits spans named "postgresql")
        if (string.Equals(samplingParameters.Name, "postgresql", StringComparison.OrdinalIgnoreCase))
            return true;

        // Fallback: check the db.system semantic convention tag
        if (samplingParameters.Tags is not null)
        {
            foreach (var tag in samplingParameters.Tags)
            {
                if (tag.Key == "db.system" && tag.Value is string system
                    && string.Equals(system, "postgresql", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}

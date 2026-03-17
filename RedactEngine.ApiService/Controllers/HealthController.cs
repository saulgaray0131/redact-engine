using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;
using System.Reflection;

namespace RedactEngine.ApiService.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;
    private readonly IHostEnvironment _hostEnvironment;

    public HealthController(HealthCheckService healthCheckService, IHostEnvironment hostEnvironment)
    {
        _healthCheckService = healthCheckService;
        _hostEnvironment = hostEnvironment;
    }

    [HttpGet]
    public ActionResult<HealthResponse> GetHealth()
    {
        return Ok(new HealthResponse(
            Status: "Healthy",
            Timestamp: DateTime.UtcNow
        ));
    }

    [HttpGet("detailed")]
    public async Task<ActionResult<DetailedHealthResponse>> GetDetailedHealth(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var report = await _healthCheckService.CheckHealthAsync(_ => true, cancellationToken);
        stopwatch.Stop();

        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

        return Ok(new DetailedHealthResponse(
            Status: report.Status.ToString(),
            Version: version,
            Environment: _hostEnvironment.EnvironmentName,
            Timestamp: DateTime.UtcNow,
            DurationMs: stopwatch.ElapsedMilliseconds,
            Checks: report.Entries
                .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
                .Select(e => new HealthCheckInfo(
                    Name: e.Key,
                    Status: e.Value.Status.ToString(),
                    DurationMs: (long)e.Value.Duration.TotalMilliseconds))
                .ToArray()
        ));
    }
}

public record HealthResponse(
    string Status,
    DateTime Timestamp
);

public record DetailedHealthResponse(
    string Status,
    string Version,
    string Environment,
    DateTime Timestamp,
    long DurationMs,
    IReadOnlyList<HealthCheckInfo> Checks
);

public record HealthCheckInfo(
    string Name,
    string Status,
    long DurationMs
);
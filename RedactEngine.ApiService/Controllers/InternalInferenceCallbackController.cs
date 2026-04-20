using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedactEngine.Application.Common.Interfaces;
using RedactEngine.Domain.Entities;
using RedactEngine.Domain.ValueObjects;

namespace RedactEngine.ApiService.Controllers;

/// <summary>
/// Internal callback surface used by the Python inference service to report
/// terminal state for an async /redact job. Authenticated via the shared
/// X-Inference-Key header (same secret the Worker sends outbound).
/// </summary>
[ApiController]
[Route("internal/redaction-jobs")]
public sealed class InternalInferenceCallbackController(
    IApplicationDbContext db,
    IConfiguration configuration,
    ILogger<InternalInferenceCallbackController> logger) : ControllerBase
{
    [HttpPost("{jobId:guid}/complete")]
    public async Task<IActionResult> CompleteAsync(
        Guid jobId,
        [FromBody] InferenceCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var expectedKey = configuration["InferenceService:ApiKey"]
            ?? Environment.GetEnvironmentVariable("INFERENCE_SERVICE_KEY");
        if (!string.IsNullOrEmpty(expectedKey))
        {
            var provided = Request.Headers["X-Inference-Key"].ToString();
            if (!string.Equals(provided, expectedKey, StringComparison.Ordinal))
            {
                logger.LogWarning("Inference callback for job {JobId} rejected: bad key", jobId);
                return Unauthorized();
            }
        }

        var job = await db.RedactionJobs
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            logger.LogWarning("Inference callback for unknown job {JobId}, acknowledging", jobId);
            return Ok(new { status = "unknown" });
        }

        // Idempotency: if the job has already reached a terminal state, the
        // callback was delivered more than once. Return 200 without mutating.
        if (job.Status is RedactionJobStatus.Completed
            or RedactionJobStatus.Failed
            or RedactionJobStatus.Cancelled)
        {
            logger.LogInformation(
                "Inference callback for job {JobId} ignored (already {Status})",
                jobId, job.Status);
            return Ok(new { status = job.Status.ToString() });
        }

        if (string.Equals(request.Status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.RedactedVideoUrl))
                return BadRequest("redactedVideoUrl is required for completed status.");

            var metrics = new ProcessingMetrics(
                totalProcessingTimeMs: request.Metrics?.TotalProcessingTimeMs ?? 0,
                framesProcessed: job.DetectionSummary?.SampledFrameCount ?? 0,
                objectsDetected: job.DetectionSummary?.TotalDetections ?? 0,
                redactionTimeMs: request.Metrics?.RedactionTimeMs);
            job.MarkCompleted(request.RedactedVideoUrl, metrics);
        }
        else if (string.Equals(request.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            var error = string.IsNullOrWhiteSpace(request.Error) ? "Inference failed" : request.Error;
            job.MarkFailed(error.Length > 2000 ? error[..2000] : error);
        }
        else
        {
            return BadRequest($"Unknown status '{request.Status}'. Expected 'completed' or 'failed'.");
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Inference callback for job {JobId} applied: {Status}",
            jobId, job.Status);
        return Ok(new { status = job.Status.ToString() });
    }
}

public sealed record InferenceCompletionRequest(
    string Status,
    string? RedactedVideoUrl,
    string? Error,
    InferenceMetricsPayload? Metrics);

public sealed record InferenceMetricsPayload(
    long TotalProcessingTimeMs,
    long? RedactionTimeMs);

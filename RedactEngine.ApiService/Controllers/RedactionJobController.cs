using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedactEngine.Application.Common;
using RedactEngine.Application.Common.Interfaces;
using RedactEngine.Domain.Entities;
using RedactEngine.Shared.PubSub;

namespace RedactEngine.ApiService.Controllers;

[ApiController]
[Route("api/redaction-jobs")]
public sealed class RedactionJobController(
    IApplicationDbContext db,
    IBlobService blobService,
    DaprClient daprClient) : ControllerBase
{
    /// <summary>
    /// Submit a new redaction job. Uploads the video to blob storage and queues detection.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(500 * 1024 * 1024)] // 500 MB
    public async Task<ActionResult<SubmitRedactionJobResponse>> SubmitAsync(
        IFormFile video,
        [FromForm] string prompt,
        [FromForm] string? redactionStyle,
        [FromForm] double? confidenceThreshold,
        CancellationToken cancellationToken)
    {
        if (video.Length == 0 || video.ContentType is null || !video.ContentType.StartsWith("video/"))
            return BadRequest("A valid video file is required.");

        if (string.IsNullOrWhiteSpace(prompt))
            return BadRequest("A text prompt is required.");

        var style = Enum.TryParse<RedactionStyle>(redactionStyle, ignoreCase: true, out var parsed)
            ? parsed
            : RedactionStyle.Blur;
        var threshold = confidenceThreshold ?? 0.3;

        // Upload original video to blob storage
        await using var stream = video.OpenReadStream();
        var videoUrl = await blobService.UploadAsync(stream, video.FileName, video.ContentType, cancellationToken);

        // Create job entity
        var job = new RedactionJob(prompt, videoUrl, video.FileName, style, threshold);
        db.RedactionJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        // Publish detection request for worker to pick up
        await daprClient.PublishEventAsync(
            DetectionPubSub.ComponentName,
            DetectionPubSub.TopicName,
            new DetectionRequestedMessage(
                job.Id,
                job.Prompt,
                job.ConfidenceThreshold,
                job.OriginalVideoUrl,
                job.OriginalFileName,
                DateTimeOffset.UtcNow),
            cancellationToken);

        return Accepted(new SubmitRedactionJobResponse(job.Id, job.Status.ToString()));
    }

    /// <summary>
    /// Get the status and result of a redaction job.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RedactionJobResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await db.RedactionJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

        if (job is null)
            return NotFound();

        return Ok(MapToResponse(job));
    }

    /// <summary>
    /// List all redaction jobs, newest first.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<RedactionJobResponse>>> ListAsync(CancellationToken cancellationToken)
    {
        var jobs = await db.RedactionJobs
            .AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        return Ok(jobs.Select(MapToResponse).ToList());
    }

    /// <summary>
    /// Confirm a job in AwaitingReview status and trigger redaction.
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<RedactionJobResponse>> ConfirmAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await db.RedactionJobs
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

        if (job is null)
            return NotFound();

        if (job.Status != RedactionJobStatus.AwaitingReview)
            return BadRequest($"Job must be in AwaitingReview status to confirm. Current status: {job.Status}");

        job.MarkRedacting();
        await db.SaveChangesAsync(cancellationToken);

        await daprClient.PublishEventAsync(
            RedactionExportPubSub.ComponentName,
            RedactionExportPubSub.TopicName,
            new RedactionExportRequestedMessage(
                job.Id,
                job.Prompt,
                job.RedactionStyle.ToString().ToLowerInvariant(),
                job.ConfidenceThreshold,
                job.OriginalVideoUrl,
                job.OriginalFileName,
                DateTimeOffset.UtcNow),
            cancellationToken);

        return Ok(MapToResponse(job));
    }

    /// <summary>
    /// Cancel a job that hasn't started redacting yet.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<RedactionJobResponse>> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await db.RedactionJobs
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

        if (job is null)
            return NotFound();

        try
        {
            job.MarkCancelled();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(MapToResponse(job));
    }

    /// <summary>
    /// Delete a redaction job and its associated blobs.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await db.RedactionJobs
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

        if (job is null)
            return NotFound();

        // Delete associated blobs
        if (!string.IsNullOrEmpty(job.OriginalVideoUrl))
        {
            await blobService.DeleteAsync(job.OriginalVideoUrl, cancellationToken);
        }
        if (!string.IsNullOrEmpty(job.RedactedVideoUrl))
        {
            await blobService.DeleteAsync(job.RedactedVideoUrl, cancellationToken);
        }

        // Remove job from database
        db.RedactionJobs.Remove(job);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static RedactionJobResponse MapToResponse(RedactionJob job) => new(
        job.Id,
        job.Prompt,
        job.RedactionStyle.ToString(),
        job.ConfidenceThreshold,
        job.OriginalVideoUrl,
        job.OriginalFileName,
        job.RedactedVideoUrl,
        job.Status.ToString(),
        job.ErrorMessage,
        job.DetectionPreviews?
            .Select(p => new DetectionPreviewResponse(p.FrameIndex, p.TimestampMs, p.Url))
            .ToList(),
        job.ProcessingMetrics?.TotalProcessingTimeMs,
        job.ProcessingMetrics?.ObjectsDetected,
        job.ProcessingMetrics?.FramesProcessed,
        job.CreatedAt,
        job.UpdatedAt);
}

public sealed record SubmitRedactionJobResponse(Guid JobId, string Status);

public sealed record DetectionPreviewResponse(int FrameIndex, int TimestampMs, string Url);

public sealed record RedactionJobResponse(
    Guid Id,
    string Prompt,
    string RedactionStyle,
    double ConfidenceThreshold,
    string OriginalVideoUrl,
    string OriginalFileName,
    string? RedactedVideoUrl,
    string Status,
    string? ErrorMessage,
    List<DetectionPreviewResponse>? DetectionPreviews,
    long? TotalProcessingTimeMs,
    int? ObjectsDetected,
    int? FramesProcessed,
    DateTime CreatedAt,
    DateTime UpdatedAt);

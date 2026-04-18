using Dapr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedactEngine.Application.Common;
using RedactEngine.Application.Common.Interfaces;
using RedactEngine.Domain.Entities;
using RedactEngine.Domain.ValueObjects;
using RedactEngine.Shared.Contracts;
using RedactEngine.Shared.PubSub;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RedactEngine.Worker.Controllers;

[ApiController]
public sealed class RedactionJobController(
    IApplicationDbContext db,
    IBlobService blobService,
    IHttpClientFactory httpClientFactory,
    ILogger<RedactionJobController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    // In-flight guard against Dapr pub/sub redelivery while a long-running
    // inference call is still in progress. Pub/sub brokers redeliver when the
    // subscriber HTTP call exceeds their visibility timeout; the status guard
    // below catches *post-completion* redeliveries, this catches *concurrent* ones.
    private static readonly ConcurrentDictionary<Guid, byte> InFlightJobs = new();

    [Topic(DetectionPubSub.ComponentName, DetectionPubSub.TopicName)]
    [HttpPost("redaction/detection/requested")]
    public async Task<IActionResult> ProcessDetectionAsync(
        [FromBody] DetectionRequestedMessage message,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing detection for job {JobId} with detection prompt: {DetectionPrompt}", message.JobId, message.DetectionPrompt);

        if (!InFlightJobs.TryAdd(message.JobId, 0))
        {
            logger.LogInformation("Detection for job {JobId} already in flight, ACKing duplicate delivery", message.JobId);
            return Ok();
        }

        var job = await db.RedactionJobs
            .FirstOrDefaultAsync(j => j.Id == message.JobId, cancellationToken);

        if (job is null)
        {
            logger.LogWarning("Redaction job {JobId} not found, skipping", message.JobId);
            InFlightJobs.TryRemove(message.JobId, out _);
            return Ok();
        }

        if (job.Status != RedactionJobStatus.Pending)
        {
            logger.LogInformation(
                "Detection for job {JobId} already past Pending (status: {Status}), ACKing redelivery",
                message.JobId, job.Status);
            InFlightJobs.TryRemove(message.JobId, out _);
            return Ok();
        }

        job.MarkDetecting();
        await db.SaveChangesAsync(cancellationToken);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var detectResult = await CallDetectAsync(message, cancellationToken);

            // Map all inference frames to count totals
            var allDetections = detectResult.Results
                .SelectMany(fr => fr.Detections)
                .ToList();

            var totalDetections = allDetections.Count;

            var allLabels = allDetections
                .Select(d => d.Label)
                .Distinct()
                .ToList();

            // Only store the first anchor frame's detections for preview (not all frames)
            var anchorDetections = detectResult.Results
                .Where(fr => fr.Detections.Count > 0)
                .Take(1)
                .Select(fr => new FrameDetection(
                    fr.FrameIndex,
                    fr.Detections.Select(d => new BoundingBox(d.X, d.Y, d.Width, d.Height, d.Confidence, d.Label)).ToList()))
                .ToList();

            var summary = new DetectionSummary(
                detectResult.FrameCount,
                totalDetections,
                allLabels,
                anchorDetections);

            var previews = await UploadDetectionPreviewsAsync(
                message.JobId, detectResult.Previews, cancellationToken);

            stopwatch.Stop();
            job.MarkDetectionComplete(summary, previews);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Detection for job {JobId} completed: {DetectionCount} detections, {PreviewCount} previews in {ElapsedMs}ms",
                message.JobId, totalDetections, previews?.Count ?? 0, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Detection for job {JobId} failed", message.JobId);
            job.MarkFailed(ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message);
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            InFlightJobs.TryRemove(message.JobId, out _);
        }

        return Ok();
    }

    [Topic(RedactionExportPubSub.ComponentName, RedactionExportPubSub.TopicName)]
    [HttpPost("redaction/export/requested")]
    public async Task<IActionResult> ProcessRedactionAsync(
        [FromBody] RedactionExportRequestedMessage message,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing redaction export for job {JobId}", message.JobId);

        if (!InFlightJobs.TryAdd(message.JobId, 0))
        {
            logger.LogInformation("Redaction for job {JobId} already in flight, ACKing duplicate delivery", message.JobId);
            return Ok();
        }

        var job = await db.RedactionJobs
            .FirstOrDefaultAsync(j => j.Id == message.JobId, cancellationToken);

        if (job is null)
        {
            logger.LogWarning("Redaction job {JobId} not found, skipping", message.JobId);
            InFlightJobs.TryRemove(message.JobId, out _);
            return Ok();
        }

        if (job.Status != RedactionJobStatus.Redacting)
        {
            logger.LogInformation(
                "Redaction for job {JobId} already past Redacting (status: {Status}), ACKing redelivery",
                message.JobId, job.Status);
            InFlightJobs.TryRemove(message.JobId, out _);
            return Ok();
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var redactedVideoBytes = await CallRedactAsync(message, cancellationToken);

            // Upload redacted video to blob storage
            using var resultStream = new MemoryStream(redactedVideoBytes);
            var redactedFileName = $"redacted_{message.OriginalFileName}";
            var redactedUrl = await blobService.UploadAsync(resultStream, redactedFileName, "video/mp4", cancellationToken);

            stopwatch.Stop();

            var metrics = new ProcessingMetrics(
                totalProcessingTimeMs: stopwatch.ElapsedMilliseconds,
                framesProcessed: job.DetectionSummary?.SampledFrameCount ?? 0,
                objectsDetected: job.DetectionSummary?.TotalDetections ?? 0,
                redactionTimeMs: stopwatch.ElapsedMilliseconds);

            job.MarkCompleted(redactedUrl, metrics);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Redaction job {JobId} completed. Result: {RedactedUrl}", message.JobId, redactedUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redaction job {JobId} failed", message.JobId);
            job.MarkFailed(ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message);
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            InFlightJobs.TryRemove(message.JobId, out _);
        }

        return Ok();
    }

    private async Task<List<DetectionPreview>?> UploadDetectionPreviewsAsync(
        Guid jobId,
        List<DetectionPreviewDto>? payloads,
        CancellationToken cancellationToken)
    {
        if (payloads is null || payloads.Count == 0)
            return null;

        var previews = new List<DetectionPreview>(payloads.Count);
        foreach (var payload in payloads)
        {
            try
            {
                var bytes = Convert.FromBase64String(payload.ImageBase64);
                using var stream = new MemoryStream(bytes);
                var url = await blobService.UploadAsync(
                    stream,
                    $"preview_{jobId}_{payload.FrameIndex}.jpg",
                    "image/jpeg",
                    cancellationToken);
                previews.Add(new DetectionPreview(payload.FrameIndex, payload.TimestampMs, url));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to upload preview frame {FrameIndex} for job {JobId}",
                    payload.FrameIndex, jobId);
            }
        }

        return previews.Count > 0 ? previews : null;
    }

    private async Task<DetectionResultDto> CallDetectAsync(
        DetectionRequestedMessage message,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("InferenceService");

        // Download original video from blob storage
        using var blobClient = new HttpClient();
        var videoBytes = await blobClient.GetByteArrayAsync(message.OriginalVideoUrl, cancellationToken);

        // Build multipart form for inference service
        using var content = new MultipartFormDataContent();
        var videoContent = new ByteArrayContent(videoBytes);
        videoContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        content.Add(videoContent, "video", message.OriginalFileName);
        content.Add(new StringContent(message.DetectionPrompt), "prompt");
        content.Add(new StringContent(message.ConfidenceThreshold.ToString()), "confidence_threshold");

        var response = await client.PostAsync("/detect", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<DetectionResultDto>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize detection response");
    }

    private async Task<byte[]> CallRedactAsync(
        RedactionExportRequestedMessage message,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("InferenceService");

        // Download original video from blob storage
        using var blobClient = new HttpClient();
        var videoBytes = await blobClient.GetByteArrayAsync(message.OriginalVideoUrl, cancellationToken);

        // Build multipart form for inference service
        using var content = new MultipartFormDataContent();
        var videoContent = new ByteArrayContent(videoBytes);
        videoContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        content.Add(videoContent, "video", message.OriginalFileName);
        content.Add(new StringContent(message.DetectionPrompt), "prompt");
        content.Add(new StringContent(message.RedactionStyle), "redaction_style");
        content.Add(new StringContent(message.ConfidenceThreshold.ToString()), "confidence_threshold");

        var response = await client.PostAsync("/redact", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}

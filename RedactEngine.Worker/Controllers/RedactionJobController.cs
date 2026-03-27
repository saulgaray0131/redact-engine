using Dapr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedactEngine.Application.Common;
using RedactEngine.Application.Common.Interfaces;
using RedactEngine.Domain.Entities;
using RedactEngine.Shared.PubSub;
using System.Net.Http.Headers;

namespace RedactEngine.Worker.Controllers;

[ApiController]
public sealed class RedactionJobController(
    IApplicationDbContext db,
    IBlobService blobService,
    IHttpClientFactory httpClientFactory,
    ILogger<RedactionJobController> logger) : ControllerBase
{
    [Topic(RedactionJobPubSub.ComponentName, RedactionJobPubSub.TopicName)]
    [HttpPost("redaction/job/submitted")]
    public async Task<IActionResult> ProcessAsync(
        [FromBody] RedactionJobSubmittedMessage message,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing redaction job {JobId} with prompt: {Prompt}", message.JobId, message.Prompt);

        var job = await db.RedactionJobs
            .FirstOrDefaultAsync(j => j.Id == message.JobId, cancellationToken);

        if (job is null)
        {
            logger.LogWarning("Redaction job {JobId} not found, skipping", message.JobId);
            return Ok(); // Acknowledge to avoid redelivery
        }

        job.MarkProcessing();
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var redactedVideoBytes = await CallInferenceServiceAsync(message, cancellationToken);

            // Upload redacted video to blob storage
            using var resultStream = new MemoryStream(redactedVideoBytes);
            var redactedFileName = $"redacted_{message.OriginalFileName}";
            var redactedUrl = await blobService.UploadAsync(resultStream, redactedFileName, "video/mp4", cancellationToken);

            job.MarkCompleted(redactedUrl);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Redaction job {JobId} completed. Result: {RedactedUrl}", message.JobId, redactedUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redaction job {JobId} failed", message.JobId);
            job.MarkFailed(ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok();
    }

    private async Task<byte[]> CallInferenceServiceAsync(
        RedactionJobSubmittedMessage message,
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
        content.Add(new StringContent(message.Prompt), "prompt");
        content.Add(new StringContent(message.RedactionStyle), "redaction_style");
        content.Add(new StringContent(message.ConfidenceThreshold.ToString()), "confidence_threshold");

        var response = await client.PostAsync("/redact", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}

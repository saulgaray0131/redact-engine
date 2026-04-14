using RedactEngine.Domain.Common;
using RedactEngine.Domain.Events;
using RedactEngine.Domain.ValueObjects;

namespace RedactEngine.Domain.Entities;

public class RedactionJob : Entity
{
    private const int MaxPromptLength = 1000;

    public string Prompt { get; private set; } = string.Empty;
    public RedactionStyle RedactionStyle { get; private set; }
    public double ConfidenceThreshold { get; private set; }
    public string OriginalVideoUrl { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string? RedactedVideoUrl { get; private set; }
    public RedactionJobStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public VideoMetadata? VideoMetadata { get; private set; }
    public DetectionSummary? DetectionSummary { get; private set; }
    public ProcessingMetrics? ProcessingMetrics { get; private set; }
    public string? DetectionPreviewUrl { get; private set; }

    private RedactionJob() { }

    public RedactionJob(
        string prompt,
        string originalVideoUrl,
        string originalFileName,
        RedactionStyle redactionStyle = RedactionStyle.Blur,
        double confidenceThreshold = 0.3)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt is required.", nameof(prompt));
        if (prompt.Trim().Length > MaxPromptLength)
            throw new ArgumentException($"Prompt must not exceed {MaxPromptLength} characters.", nameof(prompt));
        if (confidenceThreshold is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(confidenceThreshold), "Confidence threshold must be between 0 and 1.");

        Prompt = prompt.Trim();
        OriginalVideoUrl = string.IsNullOrWhiteSpace(originalVideoUrl)
            ? throw new ArgumentException("Original video URL is required.", nameof(originalVideoUrl))
            : originalVideoUrl;
        OriginalFileName = string.IsNullOrWhiteSpace(originalFileName)
            ? throw new ArgumentException("Original file name is required.", nameof(originalFileName))
            : originalFileName.Trim();
        RedactionStyle = redactionStyle;
        ConfidenceThreshold = confidenceThreshold;
        Status = RedactionJobStatus.Pending;

        AddDomainEvent(new RedactionJobCreatedEvent(Id, Prompt));
    }

    public void MarkDetecting()
    {
        EnsureStatus(RedactionJobStatus.Pending);
        Status = RedactionJobStatus.Detecting;
        UpdateTimestamp();
    }

    public void SetVideoMetadata(VideoMetadata metadata)
    {
        EnsureStatus(RedactionJobStatus.Detecting);
        VideoMetadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        UpdateTimestamp();
    }

    public void MarkDetectionComplete(DetectionSummary summary, string? previewUrl = null)
    {
        EnsureStatus(RedactionJobStatus.Detecting);
        DetectionSummary = summary ?? throw new ArgumentNullException(nameof(summary));
        DetectionPreviewUrl = previewUrl;
        Status = RedactionJobStatus.AwaitingReview;
        UpdateTimestamp();

        AddDomainEvent(new DetectionCompletedEvent(Id, summary.TotalDetections, summary.DetectedLabels));
    }

    public void MarkRedacting()
    {
        EnsureStatus(RedactionJobStatus.AwaitingReview);
        Status = RedactionJobStatus.Redacting;
        UpdateTimestamp();
    }

    public void MarkCompleted(string redactedVideoUrl, ProcessingMetrics metrics)
    {
        EnsureStatus(RedactionJobStatus.Redacting);
        RedactedVideoUrl = string.IsNullOrWhiteSpace(redactedVideoUrl)
            ? throw new ArgumentException("Redacted video URL is required.", nameof(redactedVideoUrl))
            : redactedVideoUrl;
        ProcessingMetrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        Status = RedactionJobStatus.Completed;
        UpdateTimestamp();

        AddDomainEvent(new RedactionCompletedEvent(Id, redactedVideoUrl, metrics.TotalProcessingTimeMs));
    }

    public void MarkCancelled()
    {
        if (Status is RedactionJobStatus.Completed or RedactionJobStatus.Failed or RedactionJobStatus.Cancelled
            or RedactionJobStatus.Redacting)
            throw new InvalidOperationException($"Cannot cancel a job in {Status} status.");

        Status = RedactionJobStatus.Cancelled;
        UpdateTimestamp();
    }

    public void MarkFailed(string errorMessage)
    {
        // Idempotent: if already failed, just update the error message
        if (Status == RedactionJobStatus.Failed)
        {
            ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "Unknown error" : errorMessage;
            UpdateTimestamp();
            return;
        }

        if (Status is RedactionJobStatus.Completed or RedactionJobStatus.Cancelled)
            throw new InvalidOperationException($"Cannot fail a job in {Status} status.");

        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "Unknown error" : errorMessage;
        Status = RedactionJobStatus.Failed;
        UpdateTimestamp();

        AddDomainEvent(new RedactionJobFailedEvent(Id, ErrorMessage));
    }

    private void EnsureStatus(RedactionJobStatus expected)
    {
        if (Status != expected)
            throw new InvalidOperationException(
                $"Expected job status '{expected}' but was '{Status}'.");
    }
}

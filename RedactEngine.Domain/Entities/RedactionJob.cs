using RedactEngine.Domain.Common;

namespace RedactEngine.Domain.Entities;

public class RedactionJob : Entity
{
    public string Prompt { get; private set; } = string.Empty;
    public RedactionStyle RedactionStyle { get; private set; }
    public double ConfidenceThreshold { get; private set; }
    public string OriginalVideoUrl { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string? RedactedVideoUrl { get; private set; }
    public RedactionJobStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }

    private RedactionJob() { }

    public RedactionJob(
        string prompt,
        string originalVideoUrl,
        string originalFileName,
        RedactionStyle redactionStyle = RedactionStyle.Blur,
        double confidenceThreshold = 0.3)
    {
        Prompt = string.IsNullOrWhiteSpace(prompt)
            ? throw new ArgumentException("Prompt is required.", nameof(prompt))
            : prompt.Trim();
        OriginalVideoUrl = string.IsNullOrWhiteSpace(originalVideoUrl)
            ? throw new ArgumentException("Original video URL is required.", nameof(originalVideoUrl))
            : originalVideoUrl;
        OriginalFileName = string.IsNullOrWhiteSpace(originalFileName)
            ? throw new ArgumentException("Original file name is required.", nameof(originalFileName))
            : originalFileName.Trim();
        RedactionStyle = redactionStyle;
        ConfidenceThreshold = confidenceThreshold;
        Status = RedactionJobStatus.Pending;
    }

    public void MarkProcessing()
    {
        Status = RedactionJobStatus.Processing;
        UpdateTimestamp();
    }

    public void MarkCompleted(string redactedVideoUrl)
    {
        RedactedVideoUrl = redactedVideoUrl;
        Status = RedactionJobStatus.Completed;
        UpdateTimestamp();
    }

    public void MarkFailed(string errorMessage)
    {
        ErrorMessage = errorMessage;
        Status = RedactionJobStatus.Failed;
        UpdateTimestamp();
    }
}

using RedactEngine.Domain.Common;
using RedactEngine.Domain.Entities;

namespace RedactEngine.Domain.Events;

public sealed class UserCreatedEvent : DomainEvent
{
    public UserCreatedEvent(Guid userId, string email, UserRole role)
    {
        UserId = userId;
        Email = email;
        Role = role;
    }

    public Guid UserId { get; }
    public string Email { get; }
    public UserRole Role { get; }
}

public sealed class RedactionJobCreatedEvent : DomainEvent
{
    public RedactionJobCreatedEvent(Guid jobId, string prompt)
    {
        JobId = jobId;
        Prompt = prompt;
    }

    public Guid JobId { get; }
    public string Prompt { get; }
}

public sealed class DetectionCompletedEvent : DomainEvent
{
    public DetectionCompletedEvent(Guid jobId, int totalDetections, IReadOnlyList<string> detectedLabels)
    {
        JobId = jobId;
        TotalDetections = totalDetections;
        DetectedLabels = detectedLabels;
    }

    public Guid JobId { get; }
    public int TotalDetections { get; }
    public IReadOnlyList<string> DetectedLabels { get; }
}

public sealed class RedactionCompletedEvent : DomainEvent
{
    public RedactionCompletedEvent(Guid jobId, string redactedVideoUrl, long totalProcessingTimeMs)
    {
        JobId = jobId;
        RedactedVideoUrl = redactedVideoUrl;
        TotalProcessingTimeMs = totalProcessingTimeMs;
    }

    public Guid JobId { get; }
    public string RedactedVideoUrl { get; }
    public long TotalProcessingTimeMs { get; }
}

public sealed class RedactionJobFailedEvent : DomainEvent
{
    public RedactionJobFailedEvent(Guid jobId, string errorMessage)
    {
        JobId = jobId;
        ErrorMessage = errorMessage;
    }

    public Guid JobId { get; }
    public string ErrorMessage { get; }
}

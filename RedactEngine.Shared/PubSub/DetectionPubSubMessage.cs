namespace RedactEngine.Shared.PubSub;

public static class DetectionPubSub
{
    public const string ComponentName = "pubsub";
    public const string TopicName = "redaction.detection.requested";
}

public sealed record DetectionRequestedMessage(
    Guid JobId,
    string Prompt,
    double ConfidenceThreshold,
    string OriginalVideoUrl,
    string OriginalFileName,
    DateTimeOffset CreatedAtUtc);

namespace RedactEngine.Shared.PubSub;

public static class RedactionJobPubSub
{
    public const string ComponentName = "pubsub";
    public const string TopicName = "redaction.job.submitted";
}

public sealed record RedactionJobSubmittedMessage(
    Guid JobId,
    string Prompt,
    string RedactionStyle,
    double ConfidenceThreshold,
    string OriginalVideoUrl,
    string OriginalFileName,
    DateTimeOffset CreatedAtUtc);

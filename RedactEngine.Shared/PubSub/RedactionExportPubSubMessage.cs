namespace RedactEngine.Shared.PubSub;

public static class RedactionExportPubSub
{
    public const string ComponentName = "pubsub";
    public const string TopicName = "redaction.export.requested";
}

public sealed record RedactionExportRequestedMessage(
    Guid JobId,
    string Prompt,
    string RedactionStyle,
    double ConfidenceThreshold,
    string OriginalVideoUrl,
    string OriginalFileName,
    DateTimeOffset CreatedAtUtc);

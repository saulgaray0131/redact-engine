namespace RedactEngine.Shared.PubSub;

public static class SamplePubSub
{
    public const string ComponentName = "pubsub";
    public const string TopicName = "samples.redactengine.pubsub";
}

public sealed record SamplePubSubMessage(
    Guid EventId,
    string Message,
    string Source,
    DateTimeOffset CreatedAtUtc);
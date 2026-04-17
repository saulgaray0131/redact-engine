namespace RedactEngine.Domain.ValueObjects;

public class DetectionPreview
{
    public int FrameIndex { get; init; }
    public int TimestampMs { get; init; }
    public string Url { get; init; } = string.Empty;

    private DetectionPreview() { }

    public DetectionPreview(int frameIndex, int timestampMs, string url)
    {
        FrameIndex = frameIndex;
        TimestampMs = timestampMs;
        Url = string.IsNullOrWhiteSpace(url)
            ? throw new ArgumentException("Url is required.", nameof(url))
            : url;
    }
}

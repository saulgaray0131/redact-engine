namespace RedactEngine.Domain.ValueObjects;

public class FrameDetection
{
    public int FrameIndex { get; init; }
    public List<BoundingBox> Detections { get; init; } = [];

    private FrameDetection() { }

    public FrameDetection(int frameIndex, List<BoundingBox> detections)
    {
        FrameIndex = frameIndex >= 0 ? frameIndex : throw new ArgumentOutOfRangeException(nameof(frameIndex));
        Detections = detections ?? throw new ArgumentNullException(nameof(detections));
    }
}

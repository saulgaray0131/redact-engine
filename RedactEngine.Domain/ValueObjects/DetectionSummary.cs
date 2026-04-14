namespace RedactEngine.Domain.ValueObjects;

public class DetectionSummary
{
    public int SampledFrameCount { get; init; }
    public int TotalDetections { get; init; }
    public List<string> DetectedLabels { get; init; } = [];
    public List<FrameDetection> AnchorDetections { get; init; } = [];

    private DetectionSummary() { }

    public DetectionSummary(
        int sampledFrameCount,
        int totalDetections,
        List<string> detectedLabels,
        List<FrameDetection> anchorDetections)
    {
        SampledFrameCount = sampledFrameCount;
        TotalDetections = totalDetections;
        DetectedLabels = detectedLabels ?? throw new ArgumentNullException(nameof(detectedLabels));
        AnchorDetections = anchorDetections ?? throw new ArgumentNullException(nameof(anchorDetections));
    }
}

namespace RedactEngine.Shared.Contracts;

public sealed record BoundingBoxDto(
    double X,
    double Y,
    double Width,
    double Height,
    double Confidence,
    string Label);

public sealed record FrameDetectionDto(
    int FrameIndex,
    List<BoundingBoxDto> Detections);

public sealed record DetectionPreviewDto(
    int FrameIndex,
    int TimestampMs,
    string ImageBase64);

public sealed record DetectionResultDto(
    string JobId,
    string Prompt,
    int FrameCount,
    List<FrameDetectionDto> Results,
    List<DetectionPreviewDto>? Previews);

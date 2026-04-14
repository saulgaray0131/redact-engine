namespace RedactEngine.Domain.ValueObjects;

public class ProcessingMetrics
{
    public long? DetectionTimeMs { get; init; }
    public long? RedactionTimeMs { get; init; }
    public long TotalProcessingTimeMs { get; init; }
    public int FramesProcessed { get; init; }
    public int ObjectsDetected { get; init; }

    private ProcessingMetrics() { }

    public ProcessingMetrics(
        long totalProcessingTimeMs,
        int framesProcessed,
        int objectsDetected,
        long? detectionTimeMs = null,
        long? redactionTimeMs = null)
    {
        TotalProcessingTimeMs = totalProcessingTimeMs;
        FramesProcessed = framesProcessed;
        ObjectsDetected = objectsDetected;
        DetectionTimeMs = detectionTimeMs;
        RedactionTimeMs = redactionTimeMs;
    }
}

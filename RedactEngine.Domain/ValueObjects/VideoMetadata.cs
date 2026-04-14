namespace RedactEngine.Domain.ValueObjects;

public class VideoMetadata
{
    public int Width { get; init; }
    public int Height { get; init; }
    public double Fps { get; init; }
    public double DurationSeconds { get; init; }
    public int TotalFrames { get; init; }
    public string? Codec { get; init; }

    private VideoMetadata() { }

    public VideoMetadata(int width, int height, double fps, double durationSeconds, int totalFrames, string? codec = null)
    {
        Width = width > 0 ? width : throw new ArgumentOutOfRangeException(nameof(width));
        Height = height > 0 ? height : throw new ArgumentOutOfRangeException(nameof(height));
        Fps = fps > 0 ? fps : throw new ArgumentOutOfRangeException(nameof(fps));
        DurationSeconds = durationSeconds >= 0 ? durationSeconds : throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        TotalFrames = totalFrames >= 0 ? totalFrames : throw new ArgumentOutOfRangeException(nameof(totalFrames));
        Codec = codec;
    }
}

namespace RedactEngine.Domain.ValueObjects;

public class BoundingBox
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double Confidence { get; init; }
    public string Label { get; init; } = string.Empty;

    private BoundingBox() { }

    public BoundingBox(double x, double y, double width, double height, double confidence, string label)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Confidence = confidence;
        Label = label ?? throw new ArgumentNullException(nameof(label));
    }
}

namespace DrawnUi.Infrastructure;

public struct MeasuringConstraints
{
    public Thickness Margins { get; set; }

    /// <summary>
    /// Include padding
    /// </summary>
    public Thickness TotalMargins { get; set; }

    public SKSize Request { get; set; }
    public SKRect Content { get; set; }
}
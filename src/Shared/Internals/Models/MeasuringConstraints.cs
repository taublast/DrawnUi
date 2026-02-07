namespace DrawnUi.Infrastructure;

[DebuggerDisplay("{ToString()}")]
public struct MeasuringConstraints
{
    public override string ToString()
    {
        return $"Content: {Content}";
    }
    public Thickness Margins { get; set; }

    /// <summary>
    /// Include padding
    /// </summary>
    public Thickness TotalMargins { get; set; }

    public SKSize Request { get; set; }
    public SKRect Content { get; set; }
}

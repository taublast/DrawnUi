namespace DrawnUi.Draw;

[DebuggerDisplay("{ToString(),nq}")]
public struct ScaledRect
{
    public override string ToString()
    {
        return $"Scale: {Scale}, Pixels: at {Pixels.Left},{Pixels.Top} {Pixels.Width}x{Pixels.Height}, Units: at {Units.Left},{Units.Top} {Units.Width}x{Units.Height}";
    }

    public ScaledRect()
    {
        Scale = 1;
        Units = new();
        Pixels = new();
    }
    public float Scale { get; set; }
    public SKRect Units { get; set; }
    public SKRect Pixels { get; set; }

    public bool Compare(DrawingRect b)
    {
        return Pixels.Left == b.Left && Pixels.Top == b.Top && Pixels.Right == b.Right && Pixels.Bottom == b.Bottom;
    }

    public bool Compare(ScaledRect b)
    {
        return Pixels.Left == b.Pixels.Left && Pixels.Top == b.Pixels.Top && Pixels.Right == b.Pixels.Right && Pixels.Bottom == b.Pixels.Bottom;
    }

    public static ScaledRect FromUnits(SKRect rect, float scale)
    {
        var original = rect.Clone();

        return new()
        {
            Scale = scale,
            Pixels = new(original.Left * scale, original.Top * scale, original.Right * scale, original.Bottom * scale),
            Units = new(original.Left, original.Top, original.Right, original.Bottom) //original
        };
    }
    public static ScaledRect FromPixels(SKRect rect, float scale)
    {
        var original = rect.Clone();

        return new ScaledRect()
        {
            Scale = scale,
            Units = new(original.Left / scale, original.Top / scale, original.Right / scale, original.Bottom / scale),
            Pixels = new(original.Left, original.Top, original.Right, original.Bottom) //original
        };
    }
    public static SKRect Clone(SKRect rect)
    {
        return new(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }
}

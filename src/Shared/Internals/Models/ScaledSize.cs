namespace DrawnUi.Draw;

[DebuggerDisplay("Pixels {Pixels},  Units {Units}")]

public class ScaledSize
{
    public ScaledSize()
    {
        Units = default;
        Pixels = default;
        WidthCut = false;
        HeightCut = false;
        Scale = 1;
    }

    public ScaledSize WithCut(bool widthCut, bool heightCut)
    {
        return new ScaledSize
        {
            Scale = this.Scale,
            Units = this.Units,
            Pixels = this.Pixels,
            WidthCut = widthCut,
            HeightCut = heightCut
        };
    }

    public static ScaledSize Default => new();

    public float _scale = 1;
    public float Scale
    {
        get
        {
            return _scale;
        }
        set
        {
            _scale = value;
        }
    }

    public SKSize Units { get; set; }
    public SKSize Pixels { get; set; }
    public bool WidthCut { get; set; }
    public bool HeightCut { get; set; }

    public ScaledSize Clone()
    {
        return new()
        {
            Scale = this.Scale,
            Units = this.Units,
            Pixels = this.Pixels,
            HeightCut = this.HeightCut,
            WidthCut = this.WidthCut
        };
    }

    public static ScaledSize CreateEmpty(float scale)
    {
        return new()
        {
            Scale = scale,
            Units = SKSize.Empty,
            Pixels = SKSize.Empty
        };
    }

    public bool IsEmpty
    {
        get
        {
            return Pixels.IsEmpty;
        }
    }

    public static float SnapToPixel(double point, double scale)
    {
        return (float)(Math.Round(point * scale) / scale);
    }


    public static ScaledSize FromUnits(float width, float height, float scale)
    {
        if (double.IsNaN(width))
            width = -1;
        if (double.IsNaN(height))
            height = -1;

        var nWidth = (float)(width * scale);
        if (nWidth < 0)
            nWidth = -1;
        if (float.IsInfinity(width))
        {
            nWidth = float.PositiveInfinity;
        }

        var nHeight = (float)(height * scale);
        if (nHeight < 0)
            nHeight = -1;

        if (float.IsInfinity(height))
        {
            nHeight = float.PositiveInfinity;
        }

        return new ScaledSize()
        {
            Scale = scale,
            Units = new SKSize(width, height),
            Pixels = new SKSize((float)Math.Round(nWidth), (float)Math.Round(nHeight))
        };
    }

    public static ScaledSize FromPixels(SKSize size, float scale)
    {
        return FromPixels((float)(size.Width), (float)(size.Height), false, false, scale);
    }

    public static ScaledSize FromPixels(float width, float height, float scale)
    {
        return FromPixels(width, height, false, false, scale);
    }

    public static ScaledSize FromPixels(float width, float height, bool widthCut, bool heighCut, float scale)
    {
        if (double.IsNaN(width))
            width = -1;
        if (double.IsNaN(height))
            height = -1;

        var nWidth = width / scale;
        if (nWidth < 0)
            nWidth = -1;

        var nHeight = height / scale;
        if (nHeight < 0)
            nHeight = -1;

#if disabledDEBUG
        if (Math.Round(width) != width || Math.Round(height) != height)
        {
            Debug.WriteLine($"[Warning] Width or height in PIXELS has values after decimal point.");
            width = (float)Math.Round(width);
            height = (float)Math.Round(height);
        }
#else
        width = (float)Math.Round(width);
        height = (float)Math.Round(height);
#endif

        return new ScaledSize()
        {
            Scale = scale,
            Pixels = new SKSize(width, height),
            Units = new SKSize(nWidth, nHeight),
            WidthCut = widthCut,
            HeightCut = heighCut
        };
    }
}

namespace DrawnUi.Draw;

public struct ScaledPoint
{
    public ScaledPoint()
    {
        Scale = 1;
        Units = SKPoint.Empty;
        Pixels = SKPoint.Empty;
    }
    public double Scale { get; set; }
    public SKPoint Units { get; set; }
    public SKPoint Pixels { get; set; }

    public static ScaledPoint FromUnits(float width, float height, float scale, bool roundPixels = true)
    {
        if (double.IsNaN(width))
            width = -1;
        if (double.IsNaN(height))
            height = -1;

        var nWidth = float.PositiveInfinity;
        if (!float.IsInfinity(width))
        {
            if (roundPixels)
                nWidth = (float)Math.Round(width * scale);
            else
                nWidth = width * scale;
        }

        var nHeight = float.PositiveInfinity;
        if (!float.IsInfinity(height))
        {
            if (roundPixels)
                nHeight = (float)Math.Round(height * scale);
            else
                nHeight = height * scale;
        }

        return new ScaledPoint()
        {
            Scale = scale,
            Units = new SKPoint(width, height),
            Pixels = new SKPoint(nWidth, nHeight)
        };
    }

    public static ScaledPoint FromPixels(SKPoint size, float scale)
    {
        return FromPixels(size.X, size.Y, scale);
    }

    public static ScaledPoint FromPixels(float width, float height, float scale)
    {
        if (double.IsNaN(width))
            width = -1;
        if (double.IsNaN(height))
            height = -1;

        var nWidth = width / scale;

        var nHeight = height / scale;

        return new ScaledPoint()
        {
            Scale = scale,
            Pixels = new SKPoint(width, height),
            Units = new SKPoint(nWidth, nHeight)
        };
    }
}

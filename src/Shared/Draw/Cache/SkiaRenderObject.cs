namespace DrawnUi.Draw;

public class CachedObject : IDisposable
{
    public SKPoint TranslateInputCoords(SKRect drawingRect)
    {
        //return new(0, 0);

        var offsetCacheX = drawingRect.Left - Bounds.Left;
        var offsetCacheY = drawingRect.Top - Bounds.Top;

        return new SKPoint(-offsetCacheX, -offsetCacheY);
    }

    public SKPoint CalculatePositionOffset(SKPoint drawingRect)
    {
        var offsetCacheX = drawingRect.X - Bounds.Left;
        var offsetCacheY = drawingRect.Y - Bounds.Top;

        return new SKPoint(offsetCacheX, offsetCacheY);
    }

    public SKPoint Test(SKRect drawingRect)
    {

        var offsetCacheX = Math.Abs(drawingRect.Left - Bounds.Left);
        var offsetCacheY = Math.Abs(drawingRect.Top - Bounds.Top);

        return new SKPoint(offsetCacheX, offsetCacheY);
    }

    /// <summary>
    /// This will draw with destination corrected by offset that it had when was recorded
    /// </summary>
    /// <param name="canvas"></param>
    /// <param name="destination"></param>
    /// <param name="paint"></param>
    public void Draw(SKCanvas canvas, SKRect destination, SKPaint paint)
    {
        try
        {
            if (Picture != null)
            {
                var moveY = Bounds.Top - RecordingArea.Top;
                var moveX = Bounds.Left - RecordingArea.Left;

                var x = (float)(destination.Left - Bounds.Left + moveX);
                var y = (float)(destination.Top - Bounds.Top + moveY);

                canvas.DrawPicture(Picture, x, y, paint);
                LastDrawnAt = new(x, y, Bounds.Width + x, Bounds.Height + y);
            }
            else
            if (Image != null)
            {
                var moveY = Bounds.Top - RecordingArea.Top;
                var moveX = Bounds.Left - RecordingArea.Left;

                var x = (float)(destination.Left + moveX);
                var y = (float)(destination.Top + moveY);

                canvas.DrawImage(Image, x, y, paint);
                LastDrawnAt = new(x, y, Bounds.Width + x, Bounds.Height + y);
            }
        }
        catch (Exception e)
        {
            Super.Log(e);
        }
    }

    public SKRect LastDrawnAt;

    /// <summary>
    /// Will draw at exact x,y coordinated without any adjustments
    /// </summary>
    /// <param name="canvas"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="paint"></param>
    public void Draw(SKCanvas canvas, float x, float y, SKPaint paint)
    {
        try
        {
            if (Picture != null)
            {
                canvas.DrawPicture(Picture, x, y, paint);
                LastDrawnAt = new(x, y, Bounds.Width + x, Bounds.Height + y);
            }
            else
            if (Image != null)
            {
                canvas.DrawImage(Image, x, y, paint);
                LastDrawnAt = new(x, y, Bounds.Width + x, Bounds.Height + y);
            }

        }
        catch (Exception e)
        {
            Super.Log(e);
        }
    }

    public CachedObject(SkiaCacheType type, SKPicture picture, SKRect bounds, SKRect recordingArea)
    {
        Type = type;
        Bounds = bounds;
        RecordingArea = recordingArea;
        Picture = picture;
    }

    public CachedObject(SkiaCacheType type, SKSurface surface, SKRect bounds, SKRect recordingArea)
    {
        Type = type;
        Surface = surface;
        Bounds = bounds;
        RecordingArea = recordingArea;
        Image = surface.Snapshot();
    }

    public Guid Id = Guid.NewGuid();
    private SKSurface surface;

    /// <summary>
    /// An existing surface was reused for creating this object
    /// </summary>
    public bool SurfaceIsRecycled { get; set; }

    public SKPicture Picture { get; set; }

    public SKImage Image { get; set; }

    public SKRect Bounds { get; set; }

    public SKRect RecordingArea { get; set; }

    public SkiaCacheType Type { get; protected set; }

    public void Dispose()
    {
        if (!IsDisposed)
        {
            IsDisposed = true;

            if (!PreserveSourceFromDispose)
            {
                Surface?.Dispose();
                Picture?.Dispose();
                Image?.Dispose(); // todo crash was here
            }
            Surface = null;
            Picture = null;
            Image = null;
        }
    }

    public bool IsDisposed { get; protected set; }

    public string Tag { get; set; }

    public bool PreserveSourceFromDispose { get; set; }

    public SKBitmap GetBitmap()
    {
        return SKBitmap.FromImage(Image);
    }

    public SKSurface Surface { get; set; }
   
    public List<VisualLayer> Children { get; set; }
}

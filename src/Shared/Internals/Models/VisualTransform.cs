namespace DrawnUi.Infrastructure;

/// <summary>
/// Will enhance this in the future to include more properties
/// </summary>
public class VisualTransform
{
    public VisualTransform()
    {
        IsVisible = true;
        Opacity = 1;
        Scale = new(1, 1);
    }

    public ScaledRect Frame { get; set; }

    public bool IsVisible { get; set; }

    public float Opacity { get; set; }

    public float Rotation { get; set; }

    /// <summary>
    /// Units as from TranslationX and TranslationY
    /// </summary>
    public SKPoint Translation { get; set; }

    /// <summary>
    /// Units as from ScaleX and ScaleY
    /// </summary>
    public SKPoint Scale { get; set; }

    public string Logs { get; set; }

    public int RenderedNodes { get; set; }

    /// <summary>
    /// All input rects are in pixels
    /// </summary>
    /// <param name="rect"></param>
    /// <param name="clipRect"></param>
    /// <param name="scale"></param>
    /// <returns></returns>
    public VisualTransformNative ToNative(SKRect rect, float scale)
    {

#if ANDROID

        //need pixels for android
        return new VisualTransformNative
        {
            IsVisible = IsVisible,
            Rect = rect,
            Translation = new SKPoint(Translation.X * scale, Translation.Y * scale),
            Rotation = Rotation,
            Opacity = Opacity,
            Scale = Scale
        };

#else 


        //need points 
        return new VisualTransformNative
        {
            IsVisible = IsVisible,
            //from pixels to points
            Rect = new(rect.Left / scale, rect.Top / scale, rect.Right / scale, rect.Bottom / scale),
            Translation = Translation,
            Rotation = Rotation,
            Scale = Scale,
            Opacity = Opacity
        };

#endif

        return new VisualTransformNative
        {
            IsVisible = IsVisible,
            Rect = rect,
            Translation = Translation,
            Rotation = Rotation,
            Opacity = Opacity,
            Scale = Scale,
        };
    }
}

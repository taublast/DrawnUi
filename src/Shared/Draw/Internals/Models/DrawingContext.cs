using System.Collections.Generic;
using System.Collections.Immutable;

namespace DrawnUi.Draw;

public struct DrawingContext
{

    public DrawingContext(SkiaDrawingContext ctx, SKRect destination, float scale, object arguments=null)
    {
        Context = ctx;
        Destination = destination;
        Scale = scale;
        Parameters = arguments;
    }

    public DrawingContext WithContext(SkiaDrawingContext ctx)
    {
        return new DrawingContext(ctx, Destination, this.Scale, this.Parameters);
    }

    public DrawingContext WithDestination(SKRect destination)
    {
        return new DrawingContext(this.Context, destination, this.Scale, this.Parameters);
    }

    public DrawingContext WithScale(float scale)
    {
        return new DrawingContext(Context, Destination, scale, Parameters);
    }

    public DrawingContext WithParameters(object arguments)
    {
        return new DrawingContext(Context, Destination, Scale, arguments);
    }

    /// <summary>
    /// Will add arguments to parameters, without removing those already existing if they have different keys. To replace all use `WithParameters`.
    /// </summary>
    /// <param name="values"></param>
    /// <returns></returns>
    public DrawingContext WithArguments(params KeyValuePair<string, object?>[]? values)
    {
        if (values == null)
        {
            return this;
        }

        IDictionary<string, object?> newParams;
        if (Parameters is IDictionary<string, object?> existingDict)
        {
            newParams = new Dictionary<string, object?>(existingDict);
        }
        else
        {
            newParams = new Dictionary<string, object?>();
        }

        foreach (var kvp in values)
        {
            newParams[kvp.Key] = kvp.Value;
        }

        return this.WithParameters(newParams);
    }

    public DrawingContext WithArgument(KeyValuePair<string, object?>? kvp)
    {
        if (kvp == null)
        {
            return this;
        }

        IDictionary<string, object?> newParams;
        if (Parameters is IDictionary<string, object?> existingDict)
        {
            newParams = new Dictionary<string, object?>(existingDict);
        }
        else
        {
            newParams = new Dictionary<string, object?>();
        }

        newParams[kvp.Value.Key] = kvp.Value.Value;

        return this.WithParameters(newParams);
    }

    /// <summary>
    /// Gets the argument associated with the specified key or returns null if not found.
    /// Example: `if (ctx.GetArgument(ContextArguments.Scale.ToString()) is float zoomedScale) {}`
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public object? GetArgument(string key)
    {
        if (Parameters is Dictionary<string, object?> dic)
        {
            return dic.GetValueOrDefault(key);
        }
        return null;
    }

    public DrawingContext CreateForRecordingImage(SKSurface surface, SKSize size)
    {
        var ctx = new SkiaDrawingContext()
        {
            IsVirtual = true,
            Width = size.Width,
            Height = size.Height,
            Superview = Context.Superview,
            Canvas = surface.Canvas,
            Surface = surface,
            FrameTimeNanos = Context.FrameTimeNanos,
        };
        return this.WithContext(ctx);
    }

    public DrawingContext CreateForRecordingOperations(SKPictureRecorder recorder, SKRect cacheRecordingArea)
    {
        var ctx = new SkiaDrawingContext()
        {
            IsVirtual = true,
            Width = cacheRecordingArea.Width,
            Height = cacheRecordingArea.Height,
            Superview = Context.Superview,
            Canvas = recorder.BeginRecording(cacheRecordingArea),
            Surface = Context.Surface,
            FrameTimeNanos = Context.FrameTimeNanos,
        };
        return this.WithContext(ctx);
    }

    /// <summary>
    /// Platform rendering context
    /// </summary>
    public SkiaDrawingContext Context {get; set; }

    /// <summary>
    /// Scale pixels/points to use for output
    /// </summary>
    public float Scale { get; set; }

    /// <summary>
    /// Destination to use for output
    /// </summary>
    public SKRect Destination { get; set; }

    /// <summary>
    /// Optional parameters
    /// </summary>
    public object? Parameters { get; set; }
}
 

public class RenderDrawingContext : SkiaDrawingContext
{

}

public enum ContextArguments
{
    Paint,
    Scale,
    Rect,
    Viewport,
    Plane,
    PlaneViewport,
    ShapePaint,
    Offset,
    Custom
}

public class SkiaDrawingContext
{
    public SKCanvas Canvas { get; set; }
    public SKSurface Surface { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public long FrameTimeNanos { get; set; }
    public DrawnView? Superview { get; set; }
    
    /// <summary>
    /// Recording cache
    /// </summary>
    public bool IsVirtual { get; set; }

    /// <summary>
    /// Reusing surface from previous cache
    /// </summary>
    public bool IsRecycled { get; set; }

    public SkiaDrawingContext Clone()
    {
        return new SkiaDrawingContext()
        {
            Superview = Superview,
            Width = Width,
            Height = Height,
            Canvas = this.Canvas,
            Surface = this.Surface,
            FrameTimeNanos = this.FrameTimeNanos,
        };
    }

    public SkiaDrawingContext CreateForRecordingImage(SKSurface surface, SKSize size)
    {
        return new SkiaDrawingContext()
        {
            IsVirtual = true,
            Width = size.Width,
            Height = size.Height,
            Superview = Superview,
            Canvas = surface.Canvas,
            Surface = surface,
            FrameTimeNanos = this.FrameTimeNanos,
        };
    }

    public SkiaDrawingContext CreateForRecordingOperations(SKPictureRecorder recorder, SKRect cacheRecordingArea)
    {
        return new SkiaDrawingContext()
        {
            IsVirtual = true,
            Width = cacheRecordingArea.Width,
            Height = cacheRecordingArea.Height,
            Superview = Superview,
            Canvas = recorder.BeginRecording(cacheRecordingArea),
            Surface = this.Surface,
            FrameTimeNanos = this.FrameTimeNanos,
        };
    }

    public static float DeviceDensity
    {
        get
        {
            return (float)Microsoft.Maui.Devices.DeviceDisplay.Current.MainDisplayInfo.Density;
        }
    }

    public static IServiceProvider Services
        =>
#if WINDOWS10_0_19041_0_OR_GREATER
            MauiWinUIApplication.Current.Services;
#elif ANDROID
            MauiApplication.Current.Services;
#elif IOS || MACCATALYST
            MauiUIApplicationDelegate.Current.Services;
#else
            null;


#endif


}

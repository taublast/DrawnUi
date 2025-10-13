using System.Numerics;

namespace DrawnUi.Draw;

public interface IDrawnBase : IDisposable, ICanBeUpdatedWithContext
{


    SKRect DrawingRect { get; }

    string Tag { get; }

    bool IsVisible { get; set; }

    bool IsDisposed { get; }

    bool IsDisposing { get; }

    bool IsVisibleInViewTree();

    /// <summary>
    /// For virtualization. For this method to be conditional we introduced the `pixelsDestination`
    /// parameter so that the Parent could return different visible areas upon context.
    /// Normally pass your current destination you are drawing into as this parameter. 
    /// </summary>
    /// <param name="pixelsDestination"></param>
    /// <param name="inflateByPixels"></param>
    /// <returns></returns>
    public ScaledRect GetOnScreenVisibleArea(DrawingContext context, Vector2 inflateByPixels = default);

    /// <summary>
    /// Invalidates the measured size. May or may not call Update() inside, depends on control
    /// </summary>
    void Invalidate();

    /// <summary>
    /// If need the re-measure all parents because child-auto-size has changed
    /// </summary>
    void InvalidateParents();

    /// <summary>
    /// Clip using internal custom settings of the control
    /// </summary>
    /// <param name="canvas"></param>
    /// <param name="path"></param>
    /// <param name="operation"></param>
    public void ClipSmart(SKCanvas canvas, SKPath path, SKClipOperation operation = SKClipOperation.Intersect);


    /// <summary>
    /// Creates a new disposable SKPath for clipping content according to the control shape and size.
    /// Create this control clip for painting content.
    /// Pass arguments if you want to use some time-frozen data for painting at any time from any thread..
    /// If applyPosition is false will create clip without using drawing posiition, like if was drawing at 0,0.
    /// </summary>
    /// <returns></returns>
    SKPath CreateClip(object arguments, bool usePosition, SKPath path = null);

    bool RegisterAnimator(ISkiaAnimator animator);

    void UnregisterAnimator(Guid uid);

    IEnumerable<ISkiaAnimator> UnregisterAllAnimatorsByType(Type type);

    public void RegisterGestureListener(ISkiaGestureListener gestureListener);

    public void UnregisterGestureListener(ISkiaGestureListener gestureListener);

    /// <summary>
    /// Executed after the rendering
    /// </summary>
    public List<IOverlayEffect> PostAnimators { get; }


    /// <summary>
    /// For code-behind access of children, XAML is using Children property
    /// </summary>
    List<SkiaControl> Views { get; }

    /// <summary>
    /// Directly adds a view to the control, without any layouting. Use this instead of Views.Add() to avoid memory leaks etc
    /// </summary>
    /// <param name="view"></param>
    public void AddSubView(SkiaControl view);

    /// <summary>
    /// Directly removes a view from the control, without any layouting.
    /// Use this instead of Views.Remove() to avoid memory leaks etc
    /// </summary>
    /// <param name="view"></param>
    public void RemoveSubView(SkiaControl view);

    public ScaledSize MeasuredSize { get; }

    SKRect Destination { get; }

    public double HeightRequest { get; set; }

    public double WidthRequest { get; set; }

    public double Height { get; }

    public double Width { get; }

    public double TranslationX { get; }

    public double TranslationY { get; }

    public bool InputTransparent { get; set; }

    bool IsClippedToBounds { get; set; }

    bool ClipEffects { get; set; }

    int UpdateLocks { get; }

    /// <summary>
    /// This is needed by layout to track which child changed to sometimes avoid recalculating other children
    /// </summary>
    /// <param name="skiaControl"></param>
    void InvalidateByChild(SkiaControl skiaControl);

    /// <summary>
    /// To track dirty area when Updating parent
    /// </summary>
    /// <param name="skiaControl"></param>
    void UpdateByChild(SkiaControl skiaControl);

    public bool ShouldInvalidateByChildren { get; }

    float RenderingScale { get; }

    double X { get; }

    double Y { get; }

    void InvalidateViewport();

    void Repaint();

    void InvalidateViewsList();

    void DisposeObject(IDisposable value, [CallerMemberName] string caller = null);
}

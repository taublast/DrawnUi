using System.Numerics;
using SkiaControl = DrawnUi.Draw.SkiaControl;

namespace DrawnUi.Controls;

/// <summary>
/// A <see cref="SkiaCarousel"/> whose slides do not slide: the move between two slides is rendered by a
/// transition shader (<see cref="ShaderTransitionEffect"/>) blending the cached images of the current
/// and the next cell, with <c>progress</c> following the scroll. Works with gestures, <see cref="SkiaCarousel.IsLooped"/>,
/// programmatic <see cref="SkiaCarousel.SelectedIndex"/> changes and ItemsSource resets.
/// Templated only (ItemsSource + ItemTemplate); recycling is disabled because every cell must keep its own
/// cached image for the effect to sample.
/// </summary>
public class SkiaShaderCarousel : SkiaCarousel
{
    public SkiaShaderCarousel()
    {
        // the effect samples cached images of specific cells, every index must keep its own view
        RecyclingTemplate = RecyclingTemplate.Disabled;

        TransitionEffect = new ShaderTransitionEffect();
    }

    #region PUBLIC API

    public static readonly BindableProperty TransitionShaderProperty = BindableProperty.Create(
        nameof(TransitionShader),
        typeof(string),
        typeof(SkiaShaderCarousel),
        null,
        propertyChanged: (b, o, n) =>
        {
            if (b is SkiaShaderCarousel control)
            {
                control.TransitionEffect.ShaderSource = (string)n;
            }
        });

    /// <summary>
    /// Path of the transition SkSL file inside the app package (Resources/Raw), e.g. <c>Shaders/transitions/cube.sksl</c>.
    /// The file is a gl-transitions style <c>transition(vec2 uv)</c> function; it is wrapped by
    /// <see cref="ShaderTransitionEffect.DefaultTemplate"/> unless <see cref="TransitionTemplate"/> is set.
    /// Can be changed at any time, the next transition uses the new shader.
    /// </summary>
    public string TransitionShader
    {
        get { return (string)GetValue(TransitionShaderProperty); }
        set { SetValue(TransitionShaderProperty, value); }
    }

    public static readonly BindableProperty TransitionTemplateProperty = BindableProperty.Create(
        nameof(TransitionTemplate),
        typeof(string),
        typeof(SkiaShaderCarousel),
        null,
        propertyChanged: (b, o, n) =>
        {
            if (b is SkiaShaderCarousel control)
            {
                control.TransitionEffect.ShaderTemplate = (string)n;
            }
        });

    /// <summary>
    /// Optional path of a custom template SkSL file (Resources/Raw) replacing the embedded gl-transitions
    /// adapter. It must declare the uniforms listed in <see cref="ShaderTransitionEffect.DefaultTemplate"/>
    /// and contain the <c>//script-goes-here</c> placeholder where <see cref="TransitionShader"/> is inserted.
    /// </summary>
    public string TransitionTemplate
    {
        get { return (string)GetValue(TransitionTemplateProperty); }
        set { SetValue(TransitionTemplateProperty, value); }
    }

    public static readonly BindableProperty InterruptedTransitionMsProperty = BindableProperty.Create(
        nameof(InterruptedTransitionMs),
        typeof(double),
        typeof(SkiaShaderCarousel),
        50.0);

    /// <summary>
    /// When a swipe interrupts a running transition, that transition is first wrapped up to its destination
    /// within this many milliseconds, then the new transition (one slide further in the swipe direction)
    /// plays at normal speed. Set to 0 to wrap up instantly. Default is 50.
    /// </summary>
    public double InterruptedTransitionMs
    {
        get { return (double)GetValue(InterruptedTransitionMsProperty); }
        set { SetValue(InterruptedTransitionMsProperty, value); }
    }

    /// <summary>
    /// The effect rendering the transition. Exposed for customization (extra uniforms, compilation errors via
    /// <see cref="SkiaShaderEffect.OnCompilationError"/>). Created by the carousel, do not replace.
    /// </summary>
    public ShaderTransitionEffect TransitionEffect { get; }

    /// <summary>
    /// Index of the slide the current transition starts from, -1 before the first layout.
    /// </summary>
    public int TransitionFromIndex => IndexFrom;

    /// <summary>
    /// Index of the slide the current transition goes to, -1 before the first layout.
    /// For a looped carousel this can be 0 while <see cref="TransitionFromIndex"/> is the last slide.
    /// </summary>
    public int TransitionToIndex => IndexTo;

    /// <summary>
    /// Raised when the pair of slides the transition blends between changes
    /// (<see cref="TransitionFromIndex"/>/<see cref="TransitionToIndex"/>).
    /// </summary>
    public event EventHandler FromToChanged;

    #endregion

    #region EFFECT

    private bool _effectAttached;
    private bool _retrySetup;
    private double _lastProgress;
    private bool _initialized;

    private int IndexFrom = -1;
    private int IndexTo = -1;
    private int IndexFromLast = -1;
    private int IndexToLast = -1;

    protected virtual void OnFromToChanged()
    {
        FromToChanged?.Invoke(this, EventArgs.Empty);
    }

    public override void Render(DrawingContext context)
    {
        if (!_effectAttached)
        {
            _effectAttached = true;
            VisualEffects.Add(TransitionEffect);
        }

        base.Render(context);

        if (_retrySetup && !_initialized && IndexFrom >= 0 && IndexTo >= 0 && IndexTo <= MaxIndex)
        {
            _initialized = SetupFromTo();
            if (_initialized)
            {
                TransitionEffect.Progress = _lastProgress;
                TransitionEffect.Update();
            }
        }
    }

    /// <summary>
    /// Points the effect at the views of <see cref="TransitionFromIndex"/> and <see cref="TransitionToIndex"/>.
    /// Returns false when a view does not exist yet; the setup is then retried from the next render pass
    /// (after an ItemsSource reset neighbor views are created by the render pass following this call).
    /// </summary>
    protected virtual bool SetupFromTo()
    {
        IndexToLast = IndexTo;
        IndexFromLast = IndexFrom;

        var viewFrom = ChildrenFactory.GetViewForIndex(IndexFrom);
        var viewTo = ChildrenFactory.GetViewForIndex(IndexTo);

        if (viewFrom == null || viewTo == null)
        {
            _retrySetup = true;
            Update();
            return false;
        }

        _retrySetup = false;
        TransitionEffect.ControlFrom = viewFrom;
        TransitionEffect.ControlTo = viewTo;
        return true;
    }

    public override ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
    {
        _initialized = false;

        return base.OnMeasuring(widthConstraint, heightConstraint, scale);
    }

    protected override void OnChildrenInitialized()
    {
        IndexFrom = -1;
        IndexTo = -1;
        IndexFromLast = -1;
        IndexToLast = -1;
        _initialized = false;

        base.OnChildrenInitialized();
    }

    protected override void OnScrollProgressChanged()
    {
        if (IsLooped && MaxIndex > 0)
        {
            // looped: position may sit in the virtual zone (<0 or >last), wrap into [0, slides)
            var slides = MaxIndex + 1;
            var scaled = ScrollProgress * MaxIndex;
            if (scaled < 0 || scaled > MaxIndex)
            {
                // beyond the real strip: a true wrap only when panning or snapping to a virtual anchor,
                // otherwise it's spring overshoot at an edge - clamp instead of showing the far slide
                var wrap = IsUserPanning;
                if (!wrap)
                {
                    var anchor = GetVirtualAnchor(CurrentSnap, Vector2.Zero);
                    wrap = scaled < 0 ? anchor.Id == -1 : anchor.Id == -2;
                }

                scaled = wrap ? ((scaled % slides) + slides) % slides : Math.Clamp(scaled, 0, MaxIndex);
            }

            var currentIndex = (int)Math.Floor(scaled);
            var progress = scaled - currentIndex;

            if (IndexFrom != currentIndex || !_initialized)
            {
                IndexFrom = currentIndex;
                IndexTo = (currentIndex + 1) % slides;

                if (!_initialized || IndexToLast != IndexTo || IndexFromLast != IndexFrom)
                {
                    _initialized = SetupFromTo();
                }

                OnFromToChanged();
            }

            _lastProgress = progress;
            TransitionEffect.Progress = progress;
            TransitionEffect.Update();
            return;
        }

        if (!_initialized || ScrollProgress >= 0 && ScrollProgress <= 1) //ignore bouncing
        {
            var currentIndex = 0;
            if (ScrollProgress > 0)
                currentIndex = (int)Math.Floor(MaxIndex * ScrollProgress);

            var progress = TransitionProgress;

            if (IndexFrom != currentIndex || !_initialized)
            {
                if (currentIndex < MaxIndex)
                {
                    IndexTo = currentIndex + 1;
                    IndexFrom = currentIndex;

                    if (!_initialized || IndexToLast != IndexTo || IndexFromLast != IndexFrom)
                    {
                        _initialized = SetupFromTo();
                    }
                }
                else
                {
                    progress = 1.0;
                }

                OnFromToChanged();
            }

            _lastProgress = progress;
            TransitionEffect.Progress = progress;
            TransitionEffect.Update();
        }
    }

    /// <summary>
    /// Slides never move: the transition effect renders the change.
    /// </summary>
    protected override void AnimateVisibleChild(SkiaControl view, Vector2 position)
    {
    }

    #endregion

    #region INTERRUPTED TRANSITION

    private bool _swipedDuringTransition;
    private Vector2 _interruptedTarget;
    private Vector2 _interruptedFrom;
    private Vector2? _pendingTarget;

    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        var wasInTransition = args.Type == TouchActionResult.Down && InTransition;
        var result = base.ProcessGestures(args, apply);
        if (wasInTransition)
        {
            // touched while a transition is still playing: remember where it was heading
            // (read after base: Down may have normalized a looped virtual position);
            // while phase 1 is still wrapping up, the real destination is the pending phase-2 slide
            _swipedDuringTransition = true;
            _interruptedTarget = _pendingTarget ?? CurrentSnap;
            _interruptedFrom = CurrentPosition;
            _pendingTarget = null;
        }

        return result;
    }

    /// <summary>
    /// A swipe that interrupts a running transition: wrap that transition up within
    /// <see cref="InterruptedTransitionMs"/>, then move one slide further in the swipe direction at normal speed.
    /// The destination is derived from the interrupted transition, not from the finger position: mid-flight
    /// the position is arbitrary and the nearest-anchor rule would lose the swipe or skip slides.
    /// </summary>
    protected override bool ScrollToOffset(Vector2 targetOffset, Vector2 velocity, bool animate)
    {
        if (!_swipedDuringTransition || !animate)
        {
            _swipedDuringTransition = false;
            return base.ScrollToOffset(targetOffset, velocity, animate);
        }

        _swipedDuringTransition = false;

        if (SnapPoints.Count > 1)
        {
            var swipe = IsVertical ? CurrentPosition.Y - _interruptedFrom.Y : CurrentPosition.X - _interruptedFrom.X;
            var step = SnapPoints[1] - SnapPoints[0];
            var stepAxis = IsVertical ? step.Y : step.X;
            targetOffset = _interruptedTarget;
            if (swipe != 0 && stepAxis != 0)
            {
                var beyond = _interruptedTarget + step * Math.Sign(swipe) * Math.Sign(stepAxis);
                var first = SnapPoints[0];
                var last = SnapPoints[^1];
                var lo = Vector2.Min(first, last);
                var hi = Vector2.Max(first, last);
                if (IsLooped || (beyond.X >= lo.X && beyond.X <= hi.X && beyond.Y >= lo.Y && beyond.Y <= hi.Y))
                    targetOffset = beyond; // looped: may be the virtual slot past the edge, the base wraps it
            }
        }

        _pendingTarget = targetOffset != _interruptedTarget ? targetOffset : null;

        if (InterruptedTransitionMs <= 0)
        {
            base.ScrollToOffset(_interruptedTarget, Vector2.Zero, false);
            if (_pendingTarget is Vector2 instant)
            {
                _pendingTarget = null;
                return base.ScrollToOffset(instant, velocity, true);
            }

            return true;
        }

        // phase 1: finish the interrupted transition fast. Speed derives from LinearSpeedMs
        // (ms per slide), so scale it to cover the remaining distance in InterruptedTransitionMs.
        var cell = IsVertical ? CellSize.Pixels.Height : CellSize.Pixels.Width;
        var remaining = IsVertical
            ? Math.Abs(_interruptedTarget.Y - CurrentPosition.Y)
            : Math.Abs(_interruptedTarget.X - CurrentPosition.X);
        var keep = LinearSpeedMs;
        LinearSpeedMs = remaining > 0 && cell > 0 ? InterruptedTransitionMs * cell / remaining : keep;
        bool started;
        try
        {
            started = base.ScrollToOffset(_interruptedTarget, Vector2.Zero, true);
        }
        finally
        {
            LinearSpeedMs = keep;
        }

        if (!started && _pendingTarget is Vector2 next)
        {
            _pendingTarget = null;
            return base.ScrollToOffset(next, velocity, true);
        }

        return started;
    }

    public override void OnTransitionChanged()
    {
        base.OnTransitionChanged();

        // phase 2: the interrupted transition is wrapped up, now go where the swipe pointed
        if (!InTransition && _pendingTarget is Vector2 next)
        {
            _pendingTarget = null;
            ScrollToOffset(next, Vector2.Zero, true);
        }
    }

    #endregion
}

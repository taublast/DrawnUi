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

        TransitionEffect = CreateTransitionEffect();
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

    public static readonly BindableProperty TransitionShaderCodeProperty = BindableProperty.Create(
        nameof(TransitionShaderCode),
        typeof(string),
        typeof(SkiaShaderCarousel),
        null,
        propertyChanged: (b, o, n) =>
        {
            if (b is SkiaShaderCarousel control)
            {
                control.TransitionEffect.ShaderCode = (string)n;
            }
        });

    /// <summary>
    /// Raw SkSL source of the transition, alternative to <see cref="TransitionShader"/> when the code does not
    /// come from a packaged file (generated, downloaded, user-edited). Used when <see cref="TransitionShader"/>
    /// is not set; wrapped by the same template. Can be changed at any time.
    /// </summary>
    public string TransitionShaderCode
    {
        get { return (string)GetValue(TransitionShaderCodeProperty); }
        set { SetValue(TransitionShaderCodeProperty, value); }
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
    public ShaderTransitionEffect TransitionEffect { get; protected set; }

    /// <summary>
    /// Factory for the effect that renders the transition, called once from the constructor.
    /// Override to supply a custom <see cref="ShaderTransitionEffect"/> subclass
    /// (extra uniforms, custom render-area clipping etc.).
    /// </summary>
    protected virtual ShaderTransitionEffect CreateTransitionEffect() => new ShaderTransitionEffect();

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
    private bool _wasWrapped;

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
                // beyond the real strip: a true wrap only when panning, snapping to a virtual anchor, or
                // continuing a wrap already shown (release-return from the virtual zone must animate the
                // same pair to its end, not jump). Otherwise it's spring overshoot at an edge - clamp
                // instead of flashing the far slide.
                var wrap = IsUserPanning || _wasWrapped;
                if (!wrap)
                {
                    var anchor = GetVirtualAnchor(CurrentSnap, Vector2.Zero);
                    wrap = scaled < 0 ? anchor.Id == -1 : anchor.Id == -2;
                }

                _wasWrapped = wrap;
                scaled = wrap ? ((scaled % slides) + slides) % slides : Math.Clamp(scaled, 0, MaxIndex);
            }
            else
            {
                _wasWrapped = false;
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

    #region GESTURE TARGETING

    private bool _wasInTransitionAtDown;
    private bool _inGestureRelease;
    private Vector2 _gestureOrigin;
    private Vector2 _gestureFrom;
    private Vector2? _pendingTarget;

    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        if (args.Type == TouchActionResult.Down)
        {
            var interrupted = InTransition;
            var result = base.ProcessGestures(args, apply);
            // read after base: Down may have normalized a looped virtual position;
            // while phase 1 is still wrapping up, the real origin is the pending phase-2 slide
            _wasInTransitionAtDown = interrupted;
            _gestureOrigin = _pendingTarget ?? CurrentSnap;
            _gestureFrom = CurrentPosition;
            _pendingTarget = null;
            return result;
        }

        if (args.Type == TouchActionResult.Up)
        {
            _inGestureRelease = true;
            try
            {
                return base.ProcessGestures(args, apply);
            }
            finally
            {
                _inGestureRelease = false;
            }
        }

        return base.ProcessGestures(args, apply);
    }

    /// <summary>
    /// Gesture targeting: one gesture moves AT MOST ONE slide from the slide the gesture started on
    /// (for an interrupted transition: from the slide it was heading to). The base nearest-anchor rule
    /// can pick a farther snap from a fast flick or an arbitrary mid-flight finger position; here its
    /// choice is kept only for direction and stay-or-move, then clamped to ±1 slide.
    /// A swipe that interrupted a running transition additionally wraps that transition up within
    /// <see cref="InterruptedTransitionMs"/> before the new one starts. Programmatic scrolls
    /// (<see cref="SkiaCarousel.SelectedIndex"/>, GoNext/GoPrev) are not affected.
    /// </summary>
    protected override bool ScrollToOffset(Vector2 targetOffset, Vector2 velocity, bool animate)
    {
        if (!_inGestureRelease || !animate || SnapPoints.Count < 2)
        {
            return base.ScrollToOffset(targetOffset, velocity, animate);
        }

        // One gesture = at most one slide from the gesture origin. The base nearest-anchor choice is
        // ignored entirely: mid-flight it works off an arbitrary finger position, and in the looped
        // virtual zone its pseudo-anchor mapping can even flip the direction. Deterministic instead:
        // a flick (velocity over the base's 100 units/s threshold) moves one slide in the flick
        // direction; a slow drag snaps to the nearest slide within one step of the origin.
        var step = SnapPoints[1] - SnapPoints[0];
        var stepAxis = IsVertical ? step.Y : step.X;
        if (stepAxis != 0)
        {
            var vel = IsVertical ? velocity.Y : velocity.X;
            var disp = IsVertical ? CurrentPosition.Y - _gestureFrom.Y : CurrentPosition.X - _gestureFrom.X;
            int k;
            if (Math.Abs(vel) >= 100)
            {
                k = Math.Sign(vel) * Math.Sign(stepAxis);
            }
            else
            {
                k = Math.Clamp((int)Math.Round(disp / stepAxis), -1, 1);
            }

            var capped = _gestureOrigin + step * k;
            if (!IsLooped)
            {
                var first = SnapPoints[0];
                var last = SnapPoints[^1];
                var lo = Vector2.Min(first, last);
                var hi = Vector2.Max(first, last);
                capped = Vector2.Max(lo, Vector2.Min(hi, capped));
            }

            targetOffset = capped; // looped: may be the virtual slot past the edge, the base wraps it
        }

        if (!_wasInTransitionAtDown)
        {
            return base.ScrollToOffset(targetOffset, velocity, animate);
        }

        _wasInTransitionAtDown = false;

        // the pan may have already carried the position PAST the interrupted transition's destination
        // toward the new target: wrapping it up would animate BACKWARD first (visible jump-back).
        // Only run phase 1 while that destination is still ahead in the direction of travel.
        var toOrigin = IsVertical ? _gestureOrigin.Y - CurrentPosition.Y : _gestureOrigin.X - CurrentPosition.X;
        var toTarget = IsVertical ? targetOffset.Y - CurrentPosition.Y : targetOffset.X - CurrentPosition.X;
        if (targetOffset != _gestureOrigin && (toOrigin == 0 || Math.Sign(toOrigin) != Math.Sign(toTarget)))
        {
            return base.ScrollToOffset(targetOffset, velocity, animate);
        }
        _pendingTarget = targetOffset != _gestureOrigin ? targetOffset : null;

        if (InterruptedTransitionMs <= 0)
        {
            base.ScrollToOffset(_gestureOrigin, Vector2.Zero, false);
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
            ? Math.Abs(_gestureOrigin.Y - CurrentPosition.Y)
            : Math.Abs(_gestureOrigin.X - CurrentPosition.X);
        var keep = LinearSpeedMs;
        LinearSpeedMs = remaining > 0 && cell > 0 ? InterruptedTransitionMs * cell / remaining : keep;
        bool started;
        try
        {
            started = base.ScrollToOffset(_gestureOrigin, Vector2.Zero, true);
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

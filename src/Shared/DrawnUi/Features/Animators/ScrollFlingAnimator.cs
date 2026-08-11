// Credits to https://github.com/super-ultra
 
namespace DrawnUi.Draw;

public class ScrollFlingAnimator : SkiaValueAnimator
{
    public bool SelfFinished { get; set; }
    public float ValueThreshold { get; set; } = 0.1f; // Stop when change per frame is less than this
    private float _lastValue;
    private long _lastUpdateTime;
    private int _belowThresholdFrames;
    private const int FRAMES_BELOW_THRESHOLD_TO_STOP = 3; // Stop after 3 consecutive frames below threshold

    /// <summary>
    /// Rendering scale (physical pixels per unit) of the parent surface. When > 0 enables the
    /// pixel-aware finish: the exponential deceleration has an asymptotic tail that spends up to
    /// a second below one pixel per frame — on low-density displays (desktop 100-150%) that renders
    /// as visible single-pixel hops with growing pauses ("jagged fling ending"). Instead, once the
    /// per-frame step falls under <see cref="FinishStepPixels"/> physical pixels, the remaining
    /// trajectory is replaced by a short velocity-continuous ease-out that lands on the pixel grid.
    /// Set by the owning scroll before each run. 0 = disabled (legacy tail).
    /// </summary>
    public float PixelsScale { get; set; }

    /// <summary>
    /// Physical-pixel per-frame step (at 60fps baseline) below which the finish phase engages.
    /// </summary>
    public float FinishStepPixels { get; set; } = 1f;

    /// <summary>
    /// Duration of the finishing ease-out in seconds.
    /// </summary>
    public float FinishDurationSecs { get; set; } = 0.25f;

    bool _finishing;
    float _finishFrom;
    float _finishTo;
    float _finishVelocity;
    long _finishStartFromStart;

    public Task RunAsync(float position, float velocity, float deceleration = 0.998f, CancellationToken cancellationToken = default)
    {
        return RunAsync(() => InitializeWithVelocity(position, velocity, deceleration), cancellationToken);
    }

    /// <summary>
    /// Initialize with velocity and optional value threshold for early termination
    /// </summary>
    /// <param name="position">Starting position</param>
    /// <param name="velocity">Initial velocity</param>
    /// <param name="deceleration">Deceleration rate</param>
    /// <param name="valueThreshold">Stop when value change per frame is below this</param>
    public void InitializeWithVelocity(float position, float velocity, float deceleration = 0.998f, float valueThreshold = 1.85f)
    {
        // Use a minimal velocity threshold just for duration calculation, real stopping is value-based
        Parameters = new(position, velocity, deceleration, 0.001f);
        Speed = Parameters.DurationSecs;
        ValueThreshold = valueThreshold;
        _lastValue = position;
        _belowThresholdFrames = 0;
        _finishing = false;
    }

    /// <summary>
    /// Initialize to reach a specific destination in given time
    /// </summary>
    /// <param name="position">Starting position</param>
    /// <param name="target">Target position</param>
    /// <param name="timeSecs">Duration in seconds</param>
    /// <param name="deceleration">Deceleration rate</param>
    /// <param name="valueThreshold">Stop when value change per frame is below this</param>
    public void InitializeWithDestination(float position, float target, float timeSecs, float deceleration = 0.998f, float valueThreshold = 0.1f)
    {
        Parameters = new(position, target, timeSecs, deceleration, 0.001f);
        Speed = Parameters.DurationSecs;
        ValueThreshold = valueThreshold;
        _lastValue = position;
        _belowThresholdFrames = 0;
        _finishing = false;
    }

    public DecelerationTimingParameters Parameters { get; set; }
    public float CurrentVelocity { get; protected set; }

    /// <summary>
    /// Translates the whole deceleration trajectory by <paramref name="delta"/> (same units as the
    /// animated value). Used when content above the viewport changes size mid-fling so the fling keeps
    /// targeting the same content instead of fighting a scroll-anchor offset correction.
    /// </summary>
    public void Shift(float delta)
    {
        if (Math.Abs(delta) < 0.0001f)
            return;

        if (Parameters != null)
            Parameters.InitialValue += delta; // ValueAt = InitialValue + velocity*factor -> shifts curve

        _lastValue += delta;

        if (_finishing)
        {
            _finishFrom += delta;
            _finishTo += delta;
        }
    }

    public override void Start(double delayMs = 0)
    {
        SelfFinished = false;
        _belowThresholdFrames = 0;
        _finishing = false;
        base.Start(delayMs);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override bool UpdateValue(long deltaT, long deltaFromStart)
    {
        // Pixel-aware finish phase: finite ease-out replacing the asymptotic exponential tail
        if (_finishing)
        {
            var tFinish = (deltaFromStart - _finishStartFromStart) / 1_000_000_000.0f / FinishDurationSecs;
            if (tFinish >= 1f)
            {
                mValue = _finishTo;
                CurrentVelocity = 0;
                SelfFinished = true;
                return true;
            }

            var ease = tFinish * (2f - tFinish); // quadratic ease-out, velocity-continuous at entry
            mValue = _finishFrom + (_finishTo - _finishFrom) * ease;
            CurrentVelocity = _finishVelocity * (1f - tFinish);
            _lastValue = (float)mValue;
            _lastUpdateTime = deltaFromStart;
            return false;
        }

        var secs = deltaFromStart / 1_000_000_000.0f;

        // Calculate new value and velocity
        mValue = Parameters.ValueAt(secs);
        CurrentVelocity = Parameters.VelocityAt(secs);

        // Check if we've reached the time-based duration
        if (secs > Speed)
        {
            SelfFinished = true;
            return true;
        }

        // Enter pixel-aware finish once the per-frame step falls below FinishStepPixels physical
        // pixels (60fps baseline). Skipped for duration-cut flings (Speed < full DurationSecs:
        // edge-targeted, must land exactly at ValueAt(Speed)) and when PixelsScale wasn't provided.
        if (PixelsScale > 0
            && Speed >= Parameters.DurationSecs - 0.01f
            && Math.Abs(CurrentVelocity) > 0.01f
            && Math.Abs(CurrentVelocity) * PixelsScale <= FinishStepPixels * 60f)
        {
            _finishing = true;
            _finishFrom = (float)mValue;
            _finishVelocity = CurrentVelocity;
            _finishStartFromStart = deltaFromStart;

            // quad ease-out with initial velocity v covers v*T/2; land on the physical pixel grid
            var target = _finishFrom + CurrentVelocity * FinishDurationSecs / 2f;
            _finishTo = (float)Math.Round(target * PixelsScale) / PixelsScale;

            _lastValue = (float)mValue;
            _lastUpdateTime = deltaFromStart;
            return false;
        }

        // Check value-based threshold (only after first frame)
        if (_lastUpdateTime > 0)
        {
            float deltaTime = (deltaT / 1_000_000_000.0f); // Convert to seconds
            float valueChange = (float)Math.Abs(mValue - _lastValue);
            float changeRate = deltaTime > 0 ? valueChange / deltaTime : 0;

            if (changeRate < ValueThreshold)
            {
                _belowThresholdFrames++;
                if (_belowThresholdFrames >= FRAMES_BELOW_THRESHOLD_TO_STOP)
                {
                    SelfFinished = true;
                    return true;
                }
            }
            else
            {
                _belowThresholdFrames = 0;
            }
        }

        _lastValue = (float)mValue;
        _lastUpdateTime = deltaFromStart;

        return false;
    }

    public ScrollFlingAnimator(IDrawnBase parent) : base(parent)
    {
        InitializeWithVelocity(0, 0);
    }
}

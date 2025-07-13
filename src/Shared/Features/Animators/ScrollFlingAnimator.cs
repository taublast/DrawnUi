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
    }

    public DecelerationTimingParameters Parameters { get; set; }
    public float CurrentVelocity { get; protected set; }

    public override void Start(double delayMs = 0)
    {
        SelfFinished = false;
        _belowThresholdFrames = 0;
        base.Start(delayMs);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override bool UpdateValue(long deltaT, long deltaFromStart)
    {
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

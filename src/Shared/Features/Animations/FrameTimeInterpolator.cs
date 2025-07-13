namespace DrawnUi.Draw;

/// <summary>
/// Interpolated time between frames, works in seconds. See examples..
/// </summary>
public class FrameTimeInterpolator
{
    // Default FPS values
    private const int DEFAULT_TARGET_FPS = 60;
    private const float DEFAULT_FPS_BUFFER = 10f;
    private const float MINIMUM_BUFFER = 1f; // Minimum buffer to prevent division issues

    // Performance monitoring constants
    //private const int MONITORING_FRAMES = 60; // Monitor over ~1 second at 60fps
    private const float QUALITY_CHANGE_THRESHOLD = 0.75f; // 75% of frames must exceed threshold

    // Quality reduction ratio from target FPS
    private const float QUALITY_REDUCTION_RATIO = 0.75f;

    private float _lastFrameTime;
    private float _currentTimeStep;

    // FPS and quality monitoring
    private float[] _frameTimeHistory;
    private int _frameHistoryIndex;
    private int _frameHistoryCount;

    // Target FPS configuration
    private float _targetFps = DEFAULT_TARGET_FPS;
    private float _fpsBuffer = DEFAULT_FPS_BUFFER;

    /// <summary>
    /// Gets or sets the target FPS for the application
    /// </summary>
    public float TargetFps
    {
        get => _targetFps;
        set
        {
            if (value != _targetFps && value > 0)
            {
                _targetFps = value;
                _currentTimeStep = 1f / _targetFps;

                // Adjust buffer if needed to ensure it's valid for the new target
                FpsBuffer = _fpsBuffer; // This will clamp the buffer to a valid range

                ResetMonitoring();
            }
        }
    }

    /// <summary>
    /// Gets or sets the FPS buffer below target where frame skipping begins
    /// </summary>
    public float FpsBuffer
    {
        get => _fpsBuffer;
        set
        {
            // Ensure buffer is within valid range (at least MINIMUM_BUFFER and less than target FPS)
            float newBuffer = Math.Max(MINIMUM_BUFFER, value);
            newBuffer = Math.Min(newBuffer, _targetFps - MINIMUM_BUFFER);

            if (newBuffer != _fpsBuffer)
            {
                _fpsBuffer = newBuffer;
                ResetMonitoring();
            }
        }
    }

    public float LastFrameStep { get; protected set; }
    public bool IsSkippingFrames { get; protected set; }
    public bool ShouldReduceQuality { get; private set; }

    /// <summary>
    /// Calculates the delta time for the current frame based on performance monitoring
    /// and the target FPS
    /// </summary>
    /// <param name="currentFrameTime">The current frame time from the game loop</param>
    /// <returns>The delta time to use for game updates</returns>
    public float GetDeltaTime(float currentFrameTime)
    {
        // First frame or reset
        if (_lastFrameTime == 0)
        {
            InitializeManager(currentFrameTime);
            return _currentTimeStep;
        }

        // Calculate actual frame time
        float actualFrameTime = currentFrameTime - _lastFrameTime;
        _lastFrameTime = currentFrameTime;

        // Add to frame history
        AddToFrameTimeHistory(actualFrameTime);

        // Calculate minimum frame time threshold (target FPS minus buffer)
        // Buffer is already clamped to valid range in the FpsBuffer setter
        float minFrameTimeThreshold = 1f / (_targetFps - _fpsBuffer);

        // Determine if we're skipping frames
        if (actualFrameTime < minFrameTimeThreshold)
        {
            // Performance is good, use the constant time step based on target FPS
            LastFrameStep = _currentTimeStep;
            IsSkippingFrames = false;
        }
        else
        {
            // Performance is below target minus buffer, use actual frame time
            LastFrameStep = actualFrameTime;
            IsSkippingFrames = true;

            if (!IsSkippingFrames)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Frame rate below {_targetFps - _fpsBuffer} FPS. Using actual delta: {actualFrameTime:F4}s"
                );
            }
        }

        // Monitor quality settings
        UpdateQualitySettings();

        return LastFrameStep;
    }

    /// <summary>
    /// Resets the time interpolation state
    /// </summary>
    public void Reset()
    {
        _lastFrameTime = 0;
        ShouldReduceQuality = false;
        IsSkippingFrames = false;
        LastFrameStep = 0;
        SetTargetFps();
        _currentTimeStep = 1f / _targetFps;
        _frameHistoryIndex = 0;
        _frameHistoryCount = 0;
    }

    /// <summary>
    /// Resets just the monitoring data to restart the performance evaluation period
    /// </summary>
    private void ResetMonitoring()
    {
        _frameHistoryIndex = 0;
        _frameHistoryCount = 0;
    }

    static int _framerate = 60;

    void SetTargetFps()
    {

#if ONPLATFORM
        _framerate = Super.GetDisplayRefreshRate(DEFAULT_TARGET_FPS);
#else
        _framerate = DEFAULT_TARGET_FPS;
#endif

        _targetFps = _framerate;
    }

    /// <summary>
    /// Initializes the manager with default settings
    /// </summary>
    /// <param name="currentFrameTime">Current frame time to initialize with</param>
    private void InitializeManager(float currentFrameTime)
    {
        SetTargetFps();

        _lastFrameTime = currentFrameTime;
        IsSkippingFrames = false;
        ShouldReduceQuality = false;
        _frameTimeHistory = new float[(int)_framerate];
        _frameHistoryIndex = 0;
        _frameHistoryCount = 0;
        _currentTimeStep = 1f / _targetFps;
    }


    /// <summary>
    /// Adds the current frame time to the history buffer
    /// </summary>
    /// <param name="frameTime">Current frame time to add</param>
    private void AddToFrameTimeHistory(float frameTime)
    {
        _frameTimeHistory[_frameHistoryIndex] = frameTime;
        _frameHistoryIndex = (_frameHistoryIndex + 1) % _framerate;

        if (_frameHistoryCount < _framerate)
        {
            _frameHistoryCount++;
        }
    }

    /// <summary>
    /// Updates the quality settings based on monitored performance
    /// </summary>
    private void UpdateQualitySettings()
    {
        // Only evaluate after collecting enough samples
        if (_frameHistoryCount < _framerate)
        {
            return;
        }

        // Calculate quality threshold relative to target FPS (e.g., 50% of target)
        float qualityThreshold = 1f / (_targetFps * QUALITY_REDUCTION_RATIO);

        int framesWithLowPerformance = 0;

        // Count frames that are below the quality threshold
        for (int i = 0; i < _framerate; i++)
        {
            if (_frameTimeHistory[i] > qualityThreshold)
            {
                framesWithLowPerformance++;
            }
        }

        float percentageLowPerformance = (float)framesWithLowPerformance / _framerate;
        float thresholdFps = 1f / qualityThreshold;

        // Update quality settings based on threshold
        if (!ShouldReduceQuality && percentageLowPerformance >= QUALITY_CHANGE_THRESHOLD)
        {
            ShouldReduceQuality = true;
            System.Diagnostics.Debug.WriteLine(
                $"Sustained low performance detected ({percentageLowPerformance:P0} of frames below {thresholdFps:F0} FPS). Reducing render quality."
            );
            ResetMonitoring();
        }
        else if (ShouldReduceQuality && percentageLowPerformance < (1 - QUALITY_CHANGE_THRESHOLD))
        {
            ShouldReduceQuality = false;
            System.Diagnostics.Debug.WriteLine(
                $"Performance recovered ({1 - percentageLowPerformance:P0} of frames above {thresholdFps:F0} FPS). Restoring render quality."
            );
            ResetMonitoring();
        }
    }

    // Singleton pattern
    private static FrameTimeInterpolator _instance;

    public static FrameTimeInterpolator Instance =>
        _instance ??= new FrameTimeInterpolator();
}

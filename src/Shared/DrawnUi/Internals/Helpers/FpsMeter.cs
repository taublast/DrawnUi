namespace DrawnUi.Draw;

/// <summary>
/// Rolling frames-per-second counter measured on the WALL clock.
/// Deliberately not fed from the animation clock: that one advances by exactly one
/// frame interval per draw (see SkiaViewAccelerated.NextFrameClock), so dividing by it
/// would always return the nominal rate instead of the measured one.
/// </summary>
public class FpsMeter
{
    /// <summary>
    /// Gap after which the meter treats the stream as restarted: the pause itself is
    /// not a slow frame, so it must not drag the average down.
    /// </summary>
    private const double IdleResetSeconds = 1.0;

    private long _lastTimestamp;
    private double _average;
    private int _count;
    private double _report;

    /// <summary>
    /// Frames per second, averaged over <see cref="AverageOver"/> samples.
    /// Zero until the first full window completes.
    /// </summary>
    public double Value => _report;

    /// <summary>Number of samples per reported value.</summary>
    public int AverageOver { get; set; } = 10;

    /// <summary>
    /// Registers one frame. Pass a wall-clock timestamp in nanoseconds
    /// (<see cref="Super.GetCurrentTimeNanos"/>).
    /// </summary>
    public void Tick(long nowNanos)
    {
        if (_lastTimestamp == 0)
        {
            _lastTimestamp = nowNanos;
            return;
        }

        var elapsedSeconds = (nowNanos - _lastTimestamp) / 1_000_000_000.0;
        _lastTimestamp = nowNanos;

        if (elapsedSeconds <= 0)
            return;

        if (elapsedSeconds > IdleResetSeconds)
        {
            // Was idle (no frames at all) — start a fresh window instead of averaging in
            // a multi-second "frame".
            _average = 0.0;
            _count = 0;
            return;
        }

        var currentFps = 1.0 / elapsedSeconds;
        _average = ((_average * _count) + currentFps) / (_count + 1);
        _count++;

        if (_count >= AverageOver)
        {
            _report = _average;
            _count = 0;
            _average = 0.0;
        }
    }

    /// <summary>
    /// Drops the reported value to zero and starts a fresh measurement window.
    /// </summary>
    public void Reset()
    {
        _lastTimestamp = 0;
        _average = 0.0;
        _count = 0;
        _report = 0.0;
    }
}

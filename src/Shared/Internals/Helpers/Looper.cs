using System;

namespace DrawnUi.Draw;

public class Looper : IDisposable
{
    public Looper()
    {

    }

    public Looper(Action onFrame)
    {
        OnFrame = onFrame;
    }

    public CancellationTokenSource Cancel { get; protected set; }

    public void Start(int targetFps, bool useLegacy = false)
    {
        var existingCanel = Cancel;
        existingCanel?.Cancel(); 
        existingCanel?.Dispose();
        Cancel = new();
        Tasks.StartDelayed(TimeSpan.FromMilliseconds(1), async () =>
        {
            SetTargetFps(targetFps);
            if (useLegacy)
            {
                await StartLegacyLooperAsync(Cancel.Token);
            }
            else
            {
                await StartLooperAsync(Cancel.Token);
            }
        });
        existingCanel?.Dispose();
    }

    bool _loopStarting = false;
    bool _loopStarted = false;

    public void StartOnMainThread(int targetFps, bool useLegacy = false)
    {
        Tasks.StartDelayed(TimeSpan.FromMilliseconds(1), async () =>
        {
            while (!_loopStarted)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (_loopStarting)
                        return;
                    _loopStarting = true;

                    if (MainThread.IsMainThread)
                    {
                        if (!_loopStarted)
                        {
                            _loopStarted = true;

                            var existingCanel = Cancel;
                            existingCanel?.Cancel();
                            existingCanel?.Dispose();
                            Cancel = new();
                            SetTargetFps(targetFps);
                            if (useLegacy)
                                await StartLegacyLooperAsync(Cancel.Token);
                            else
                                await StartLooperAsync(Cancel.Token);
                            existingCanel?.Dispose();
                        }
                    }
                    _loopStarting = false;
                });
                await Task.Delay(100);
            }
        });

    }

    double targetIntervalMs;

    public void SetTargetFps(int targetFps)
    {
        targetIntervalMs = 1000.0 / (double)targetFps;
    }

    public void Stop()
    {
        Cancel?.Cancel();
    }

    public void Dispose()
    {
        Stop();
        Tasks.StartDelayed(TimeSpan.FromSeconds(1), () =>
        {
            Cancel?.Dispose();
        });
    }

    public bool IsRunning { get; protected set; }

    protected async Task StartLegacyLooperAsync(CancellationToken cancellationToken)
    {
        loopStopwatch.Restart();
        long lastFrameEnd = loopStopwatch.ElapsedMilliseconds;

        while (!cancellationToken.IsCancellationRequested)
        {
            IsRunning = true;

            frameStopwatch.Restart();

            OnFrame?.Invoke();

            frameStopwatch.Stop();

            var frameExecutionTimeMs = frameStopwatch.Elapsed.TotalMilliseconds;
            var elapsedTimeSinceLastFrame = loopStopwatch.ElapsedMilliseconds - lastFrameEnd;
            var timeToWait = targetIntervalMs - elapsedTimeSinceLastFrame - frameExecutionTimeMs;

            if (timeToWait < 1)
            {
                timeToWait = 1;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(timeToWait));

            lastFrameEnd = loopStopwatch.ElapsedMilliseconds;
        }

        IsRunning = false;
    }

    protected async Task StartLooperAsync(CancellationToken cancellationToken)
    {
        // Initial timestamp
        long lastFrameTimestamp = Stopwatch.GetTimestamp();

        while (!cancellationToken.IsCancellationRequested)
        {
            IsRunning = true;

            var startFrameTimestamp = Stopwatch.GetTimestamp();

            OnFrame?.Invoke();

            var endFrameTimestamp = Stopwatch.GetTimestamp();
            var frameElapsedTicks = endFrameTimestamp - startFrameTimestamp;
            var frameElapsedMs = (frameElapsedTicks / (double)Stopwatch.Frequency) * 1_000.0;
            var totalElapsedMs = ((startFrameTimestamp - lastFrameTimestamp) / (double)Stopwatch.Frequency) * 1000;
            var timeToWaitMs = targetIntervalMs - totalElapsedMs - frameElapsedMs;

            if (timeToWaitMs < 1)
            {
                timeToWaitMs = 1;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(timeToWaitMs), cancellationToken);
            }
            catch
            {
                break;
            }

            lastFrameTimestamp = Stopwatch.GetTimestamp();
        }

        IsRunning = false;
    }

    public Action OnFrame { get; set; }

    static Stopwatch frameStopwatch = new();
    static Stopwatch loopStopwatch = new();
}

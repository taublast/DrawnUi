namespace DrawnUi.Models;

public class RestartingTimer<T> //where T:class 
{
    public bool IsRunning { get; protected set; }

    public T Context { get; protected set; }
    public void Kick(T param)
    {
        if (IsRunning)
        {
            Restart(param);
        }
        else
        {
            Start(param);
        }
    }

    private readonly TimeSpan timespan;

    private readonly Action<T> callback;

    private CancellationTokenSource cancellation;

    public RestartingTimer(uint ms, Action<T> callback)
    {
        this.timespan = TimeSpan.FromMilliseconds(ms);
        this.callback = callback;
        this.cancellation = new CancellationTokenSource();
    }
    public RestartingTimer(TimeSpan timespan, Action<T> callback)
    {
        this.timespan = timespan;
        this.callback = callback;
        this.cancellation = new CancellationTokenSource();
    }



    public void Restart(T param)
    {
        Stop();
        Start(param);
    }

    public void Start(T param)
    {
        IsRunning = true;
        Context = param;

        CancellationTokenSource cts = this.cancellation; // safe copy
        Tasks.StartDelayed(this.timespan, cts.Token, async () =>
            {
                if (cts.IsCancellationRequested) return;
                this.callback.Invoke(param);
                Stop();
                cts.Cancel();
                IsRunning = false;
            });
    }

    public void Stop()
    {
        Interlocked.Exchange(ref this.cancellation, new CancellationTokenSource()).Cancel();
    }

    protected bool disposed;
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        cancellation?.Cancel();
        cancellation?.Dispose();
    }

}

public class RestartingTimer : IDisposable
{
    /// <summary>
    /// Is actually running
    /// </summary>
    public bool IsRunning { get; protected set; }

 
    /// <summary>
    /// Starts the timer if not running or restarts it if already running,
    /// but only if the timer is active
    /// </summary>
    public void Kick()
    {
  
        if (IsRunning)
        {
            Restart();
        }
        else
        {
            Start();
        }
    }

    private readonly TimeSpan timespan;
    private readonly Action callback;
    private CancellationTokenSource cancellation;

    /// <summary>
    /// Creates a new timer with the specified millisecond delay and callback
    /// </summary>
    /// <param name="ms">Milliseconds to delay before invoking callback</param>
    /// <param name="callback">Action to execute when timer completes</param>
    public RestartingTimer(uint ms, Action callback)
    {
        this.timespan = TimeSpan.FromMilliseconds(ms);
        this.callback = callback;
        this.cancellation = new CancellationTokenSource();
    }

    /// <summary>
    /// Creates a new timer with the specified timespan delay and callback
    /// </summary>
    /// <param name="timespan">Time to delay before invoking callback</param>
    /// <param name="callback">Action to execute when timer completes</param>
    public RestartingTimer(TimeSpan timespan, Action callback)
    {
        this.timespan = timespan;
        this.callback = callback;
        this.cancellation = new CancellationTokenSource();
    }

    /// <summary>
    /// Restarts the timer by stopping it and starting it again
    /// </summary>
    public void Restart()
    {
        Stop();
        Start();
    }

    /// <summary>
    /// Starts the timer
    /// </summary>
    protected void Start()
    {
        IsRunning = true;
        CancellationTokenSource cts = this.cancellation; // safe copy
        Tasks.StartDelayed(this.timespan, cts.Token, async () =>
        {
            if (cts.IsCancellationRequested) return;
            this.callback.Invoke();
            IsRunning = false;
        });
    }

    /// <summary>
    /// Stops the timer
    /// </summary>
    public void Stop()
    {
        var oldCts = Interlocked.Exchange(ref this.cancellation, new CancellationTokenSource());
        if (oldCts != null && !oldCts.IsCancellationRequested)
        {
            oldCts.Cancel();
            oldCts.Dispose();
        }
        IsRunning = false;
    }

    protected bool disposed;

    /// <summary>
    /// Disposes the timer and releases resources
    /// </summary>
    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        Stop();
        cancellation?.Dispose();
        cancellation = null;
    }
}

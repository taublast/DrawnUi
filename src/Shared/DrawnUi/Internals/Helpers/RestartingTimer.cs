namespace DrawnUi.Models;

/// <summary>
/// A one-shot timer that can be postponed with Kick(). Kicking only moves the deadline,
/// it never cancels the pending wait, so kicking at gesture rate costs nothing.
/// </summary>
public class RestartingTimer<T>
{
    public bool IsRunning { get; protected set; }

    public T Context { get; protected set; }

    private readonly TimeSpan timespan;
    private readonly Action<T> callback;
    private CancellationTokenSource cancellation;
    private long dueAtMs;
    private int running;

    public RestartingTimer(uint ms, Action<T> callback) : this(TimeSpan.FromMilliseconds(ms), callback)
    {
    }

    public RestartingTimer(TimeSpan timespan, Action<T> callback)
    {
        this.timespan = timespan;
        this.callback = callback;
        this.cancellation = new CancellationTokenSource();
    }

    /// <summary>
    /// Starts the timer, or postpones it if already running
    /// </summary>
    public void Kick(T param)
    {
        Context = param;
        Volatile.Write(ref this.dueAtMs, Environment.TickCount64 + (long)this.timespan.TotalMilliseconds);

        if (Interlocked.CompareExchange(ref this.running, 1, 0) == 0)
        {
            IsRunning = true;
            RestartingTimer.Run(Volatile.Read(ref this.cancellation), this.DueAt, this.Ended, () => this.callback?.Invoke(Context));
        }
    }

    public void Restart(T param) => Kick(param);

    public void Start(T param) => Kick(param);

    public void Stop()
    {
        Volatile.Write(ref this.running, 0);
        IsRunning = false;
        Interlocked.Exchange(ref this.cancellation, new CancellationTokenSource()).Cancel();
    }

    private long DueAt() => Volatile.Read(ref this.dueAtMs);

    private void Ended(CancellationTokenSource cts)
    {
        //do not clear the flag of a loop that Stop() already replaced, it would allow a second loop
        if (ReferenceEquals(Volatile.Read(ref this.cancellation), cts))
        {
            Volatile.Write(ref this.running, 0);
            IsRunning = false;
        }
    }

    protected bool disposed;

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

/// <summary>
/// A one-shot timer that can be postponed with Kick(). Kicking only moves the deadline,
/// it never cancels the pending wait, so kicking at gesture rate costs nothing.
/// </summary>
public class RestartingTimer : IDisposable
{
    /// <summary>
    /// Is actually running
    /// </summary>
    public bool IsRunning { get; protected set; }

    private readonly TimeSpan timespan;
    private readonly Action callback;
    private CancellationTokenSource cancellation;
    private long dueAtMs;
    private int running;

    /// <summary>
    /// Creates a new timer with the specified millisecond delay and callback
    /// </summary>
    /// <param name="ms">Milliseconds to delay before invoking callback</param>
    /// <param name="callback">Action to execute when timer completes</param>
    public RestartingTimer(uint ms, Action callback) : this(TimeSpan.FromMilliseconds(ms), callback)
    {
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
    /// Starts the timer if not running, otherwise postpones it. Safe to call at gesture rate:
    /// this only moves the deadline, nothing is cancelled and nothing is allocated.
    /// </summary>
    public void Kick()
    {
        Volatile.Write(ref this.dueAtMs, Environment.TickCount64 + (long)this.timespan.TotalMilliseconds);

        if (Interlocked.CompareExchange(ref this.running, 1, 0) == 0)
        {
            IsRunning = true;
            Run(Volatile.Read(ref this.cancellation), this.DueAt, this.Ended, this.callback);
        }
    }

    /// <summary>
    /// Postpones the timer, starting it if needed
    /// </summary>
    public void Restart() => Kick();

    /// <summary>
    /// Starts the timer
    /// </summary>
    protected void Start() => Kick();

    /// <summary>
    /// Stops the timer
    /// </summary>
    public void Stop()
    {
        Volatile.Write(ref this.running, 0);
        IsRunning = false;
        Interlocked.Exchange(ref this.cancellation, new CancellationTokenSource()).Cancel();
    }

    private long DueAt() => Volatile.Read(ref this.dueAtMs);

    private void Ended(CancellationTokenSource cts)
    {
        //do not clear the flag of a loop that Stop() already replaced, it would allow a second loop
        if (ReferenceEquals(Volatile.Read(ref this.cancellation), cts))
        {
            Volatile.Write(ref this.running, 0);
            IsRunning = false;
        }
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

    /// <summary>
    /// Waits until the deadline stops moving, then fires. Shared by both timer flavors.
    /// </summary>
    internal static async void Run(CancellationTokenSource cts, Func<long> dueAt, Action<CancellationTokenSource> ended, Action fire)
    {
        var invoke = false;
        try
        {
            while (true)
            {
                //a Kick landing in the sub-ms window between this check and Ended() is lost and the
                //callback fires one cycle early, which is fine for inactivity/debounce use
                var wait = dueAt() - Environment.TickCount64;
                if (wait <= 0)
                {
                    invoke = !cts.IsCancellationRequested;
                    break;
                }

                await Task.Delay((int)Math.Min(wait, int.MaxValue), cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            ended(cts);
        }

        if (invoke)
        {
            fire?.Invoke();
        }
    }
}

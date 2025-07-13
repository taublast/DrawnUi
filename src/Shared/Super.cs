global using SkiaSharp;
global using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace DrawnUi.Draw;

public partial class Super
{

    public static bool UseFrozenVisualLayers = false;

    /// <summary>
    /// Since we removed IHttpClientFactory for faster app startup on mobile
    /// one can set this delegate to be used to create a custom client that would be used for loading internet sources.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static Func<IServiceProvider, HttpClient> CreateHttpClient;

    /// <summary>
    /// Used by Tapped event handler, default is false
    /// </summary>
    public static bool SendTapsOnMainThread = false;

    /// <summary>
    /// Experimental for dev use. Set OffscreenRenderingAtCanvasLevel to true when this is true.
    /// Default is False
    /// </summary>
    public static bool Multithreaded = false;

    public static bool PreloadRegisteredFonts = false;

    /// <summary>
    /// If set to True will process all ofscreen rendering in one background thread at canvas level, otherwise every control will launch its own background processing thread.
    /// Default is False
    /// </summary>
    public static bool OffscreenRenderingAtCanvasLevel { get; set; }

#if (!ONPLATFORM)

    protected static void SetupFrameLooper()
    {
        throw new NotImplementedException();
    }

#endif

    /// <summary>
    /// Can optionally disable hardware-acceleration with this flag, for example on iOS you would want to avoid creating many metal views.
    /// </summary>
    public static bool CanUseHardwareAcceleration = true;

    public static void Log(Exception e, [CallerMemberName] string caller = null)
    {
        //TODO use ILogger with levels etc

#if WINDOWS
        Trace.WriteLine(e);
#else
        Console.WriteLine(e);
#endif
    }


    public static void SetLocale(string lang)
    {
        var culture = CultureInfo.CreateSpecificCulture(lang);
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }


    public static bool Initialized
    {
        get => initialized;
        set
        {
            initialized = value;
            EnableRendering = value;
        }
    }

    protected static void InitShared()
    {
        SkiaFontManager.Instance.Initialize();

        if (DrawnExtensions.StartupSettings != null && DrawnExtensions.StartupSettings.Startup !=null)
        {
            DrawnExtensions.StartupSettings.Startup?.Invoke(Services);
        }
    }

    public static bool EnableRendering
    {
        get => enableRendering;
        set
        {
            enableRendering = value;
            if (value)
                NeedGlobalUpdate();
        }
    }

    private static bool _isRtl = false;
    /// <summary>
    /// RTL support UNDER CONSTRUCTION
    /// </summary>
    public static bool IsRtl
    {
        get => _isRtl;
        set
        {
            if (_isRtl != value)
            {
                _isRtl = value;
                NeedGlobalUpdate();
            }
        }
    }

    private static bool _fontSubPixels = true;
    /// <summary>
    /// Enables sub-pixel font rendering, might provide better antialiasing on some platforms. Default is True;
    /// </summary>
    public static bool FontSubPixelRendering
    {
        get => _fontSubPixels;
        set
        {
            if (_fontSubPixels != value)
            {
                _fontSubPixels = value;
                NeedGlobalUpdate();
            }
        }
    }




    /// <summary>
    /// Subscribe your navigation bar to react
    /// </summary>
    public static EventHandler InsetsChanged;

    private static IServiceProvider _services;
    private static bool _servicesFromHandler;

    private static Screen _screen;
    public static Screen Screen
    {
        get
        {
            if (_screen == null)
                _screen = new Screen();
            return _screen;
        }
    }

    /// <summary>
    /// Capping FPS,at 120
    /// </summary>
    public static float CapMicroSecs = 8333.333333f;

    public static long GetCurrentTimeMs()
    {
        double timestamp = Stopwatch.GetTimestamp();
        double nanoseconds = 1_000.0 * timestamp / Stopwatch.Frequency;
        return (long)nanoseconds;
    }

    public static long GetCurrentTimeNanos()
    {
        double timestamp = Stopwatch.GetTimestamp();
        double nanoseconds = 1_000_000_000.0 * timestamp / Stopwatch.Frequency;
        return (long)nanoseconds;
    }

    /// <summary>
    /// In DP
    /// </summary>
    public static double NavBarHeight { get; set; } = -1;

    /// <summary>
    /// In DP
    /// </summary>
    public static double StatusBarHeight { get; set; }

    /// <summary>
    /// In DP, actually Android only
    /// </summary>
    public static double NavigationBarHeight { get; set; }

    /// <summary>
    /// In DP
    /// </summary>
    public static double BottomTabsHeight { get; set; } = 56;



    public static double MaxFrameLengthMs { get; set; }

    /// <summary>
    /// App was launched and UI is ready to be created
    /// </summary>
    public static event EventHandler OnNativeAppCreated;

    public static event EventHandler OnNativeAppPaused;

    public static event EventHandler OnNativeAppResumed;

    public static event EventHandler OnNativeAppDestroyed;

    public static Size InitialWindowSize;


    static bool _attachedToWindow;


    public static void OnCreated()
    {
        InBackground = false;

        OnNativeAppCreated?.Invoke(null, EventArgs.Empty);
    }

    public static void OnWentBackground()
    {
        InBackground = true;
        OnNativeAppPaused?.Invoke(null, EventArgs.Empty);
    }

    public static void OnWentForeground()
    {
        InBackground = false;
        //NeedGlobalUpdate();
        OnNativeAppResumed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Maui App was launched and UI is ready to be consumed
    /// </summary>
    public static Action OnMauiAppCreated;


    /// <summary>
    /// This will force recalculate canvas visibility in ViewTree and update those visible. Also when GRContext changes, all references must be cleared to avoid crash on next draw
    /// </summary>
    public static event EventHandler NeedGlobalRefresh;


    /// <summary>
    /// This will force recalculate canvas visibility in ViewTree and update those visible. Also when GRContext changes, all references must be cleared to avoid crash on next draw
    /// </summary>
    public static void NeedGlobalUpdate()
    {
        NeedGlobalRefresh?.Invoke(null, null);
    }

    static RestartingTimer<object> _timerStopRenderingInBackground;

    private static bool _inBackground;
    private static bool initialized;
    private static bool enableRendering;

    public static bool InBackground
    {
        get
        {
            return _inBackground;
        }
        protected set
        {
            if (_inBackground != value)
            {
                _inBackground = value;
                if (value)
                {
                    SetStopRenderingInBackgroundWithDelay(2000);
                }
                else
                {
                    StopRenderingInBackground = false;
                }
            }
        }
    }

    static void SetStopRenderingInBackgroundWithDelay(int ms)
    {
        if (_timerStopRenderingInBackground == null)
        {
            _timerStopRenderingInBackground = new(TimeSpan.FromMilliseconds(ms), (arg) =>
            {
                if (InBackground)
                    StopRenderingInBackground = true;
            });
            _timerStopRenderingInBackground.Start(null);
        }
        else
        {
            _timerStopRenderingInBackground.Restart(null);
        }
    }

    public static bool StopRenderingInBackground { get; set; }

    private static void OnVisualTreeChanged(object sender, VisualTreeChangeEventArgs e)
    {
        //#if DEBUG
        //            Trace.WriteLine($"[VisualTreeChanged] parent {e.Parent} child {e.Child} index {e.ChildIndex}");
        //#endif

        //int failures = 0;
        //var parentSourInfo = e.Parent == null ? null : VisualDiagnostics.GetSourceInfo(e.Parent);
        //var childSourceInfo = VisualDiagnostics.GetSourceInfo(e.Child);
        //if (childSourceInfo == null)
        //    failures++;
        //if (e.Parent != null && parentSourInfo == null)
        //    failures++;
        //if (e.Parent != null && e.ChildIndex == -1)
        //    failures++;

        //if (failures > 0)
        //    Trace.WriteLine($"[VisualTreeChanged] failures: {failures}");

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RunOnMainThreadAndWait(Action action, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource();
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                action();
                tcs.SetResult();
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled(cancellationToken);
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
        });
        tcs.Task.Wait(cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task RunOnMainThreadAndWaitAsync(Func<Task> asyncAction, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (MainThread.IsMainThread)
        {
            await asyncAction();
        }
        else
        {
            var tcs = new TaskCompletionSource<bool>();
            // Register a callback to handle cancellation
            using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
            {
                MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await asyncAction();
                        tcs.TrySetResult(true);
                    }
                    catch (OperationCanceledException)
                    {
                        tcs.TrySetCanceled(cancellationToken);
                    }
                    catch (Exception e)
                    {
                        tcs.TrySetException(e);
                    }
                });

                await tcs.Task.ConfigureAwait(false);
            }
        }
    }

    public static bool CacheEnabled { get; set; } = true;

    public static bool GpuCacheEnabled { get; set; } = true;

    public static string UserAgent { get; set; } = "Mozilla/5.0 AppleWebKit Chrome Mobile Safari";

    public static int SkiaGeneration = 3;
    
    static readonly Queue<Func<Task>> _offscreenCacheRenderingQueue = new(1024);
    private static bool _processingOffscrenRendering;

    public static void EnqueueBackgroundTask(Func<Task> asyncAction)
    {
        _offscreenCacheRenderingQueue.Enqueue(asyncAction);
    }

    protected static async Task ProcessBackgroundQueue()
    {
        if (_processingOffscrenRendering)
            return;

        _processingOffscrenRendering = true;

        while (_processingOffscrenRendering)
        {
            _offscreenCacheRenderingQueue.TryDequeue(out var action);
            if (action != null)
            {
                action().ConfigureAwait(false);
            }

            await Task.Delay(1);
        }

    }
}

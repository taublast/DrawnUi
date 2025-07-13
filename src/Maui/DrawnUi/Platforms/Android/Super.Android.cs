using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Microsoft.Maui.Controls.Compatibility.Platform.Android;
using Canvas = Android.Graphics.Canvas;
using Context = Android.Content.Context;
using Platform = Microsoft.Maui.ApplicationModel.Platform;

namespace DrawnUi.Draw;

public partial class Super
{

    public static Android.App.Activity MainActivity { get; set; }

    private static FrameCallback _frameCallback;

    static bool _loopStarting = false;
    static bool _loopStarted = false;

    public static event EventHandler OnFrame;
    static Looper Looper { get; set; }

    public static int RefreshRate { get; protected set; }

    public static int GetDisplayRefreshRate(int fallback)
    {
        var ret = fallback;
        try
        {
            RefreshRate = 60;

            if (Platform.CurrentActivity?.WindowManager?.DefaultDisplay != null)
            {
                var display = Platform.CurrentActivity.WindowManager.DefaultDisplay;
                ret = (int)display.RefreshRate;
            }
        }
        catch
        {
        }
        return ret;
    }
    
    public static void Init(Android.App.Activity activity)
    {
        Initialized = true;

        MainActivity = activity;

        RefreshRate = GetDisplayRefreshRate(60);

        Super.Screen.Density = activity.Resources.DisplayMetrics.Density;

        Super.Screen.WidthDip = activity.Resources.DisplayMetrics.WidthPixels / Super.Screen.Density;
        Super.Screen.HeightDip = activity.Resources.DisplayMetrics.HeightPixels / Super.Screen.Density;

        if (Super.NavBarHeight < 0)
            Super.NavBarHeight = 45; //manual

        //var isFullscreen = (int)activity.Window.DecorView.SystemUiVisibility & (int)SystemUiFlags.LayoutStable;

        Super.StatusBarHeight = GetStatusBarHeight(activity) / Super.Screen.Density;

        Super.NavigationBarHeight = GetNavigationHeight(activity) / Super.Screen.Density;

        bool isRendering = false;
        object lockFrane = new();

        InitShared();

        Tasks.StartDelayed(TimeSpan.FromMilliseconds(250), async () =>
        {
            _frameCallback = new FrameCallback((nanos) =>
            {
                if (isRendering)
                    return;
                isRendering = true;
                OnFrame?.Invoke(null, null);
                Choreographer.Instance.PostFrameCallback(_frameCallback);
                isRendering = false;
            });

            while (!_loopStarted)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    lock (lockFrane)
                    {
                        if (_loopStarting)
                            return;

                        _loopStarting = true;

                        if (MainThread.IsMainThread) // Choreographer is available
                        {
                            if (!_loopStarted)
                            {
                                _loopStarted = true;
                                Choreographer.Instance.PostFrameCallback(_frameCallback);
                            }
                        }

                        _loopStarting = false;
                    }
                });

                if (_loopStarted)
                    break;

                await Task.Delay(100);
            }

        });

        ExecAfterInit?.Invoke(null, EventArgs.Empty);

        ExecAfterInit = null;
    }

    private static EventHandler ExecAfterInit;

    /// <summary>
    /// ToDo resolve obsolete for android api 30 and later
    /// </summary>
    /// <param name="activity"></param>
    public static void SetFullScreen(Android.App.Activity activity)
    {
        if (_insetsListener == null)
        {
            _insetsListener = new();
        }

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Kitkat)
        {
            // https://stackoverflow.com/a/33355089/7149454
            var uiOptions = (int)activity.Window.DecorView.SystemUiVisibility;
            uiOptions |= (int)SystemUiFlags.LayoutStable;
            uiOptions |= (int)SystemUiFlags.LayoutFullscreen;
            activity.Window.DecorView.SystemUiVisibility = (StatusBarVisibility)uiOptions;
            activity.Window.SetStatusBarColor(Android.Graphics.Color.Transparent);
            var contentView = activity.FindViewById(Android.Resource.Id.Content);
            if (contentView != null)
                contentView.SetOnApplyWindowInsetsListener(_insetsListener);
        }

    }

    static InsetsListener _insetsListener;



    public class InsetsListener : Java.Lang.Object, Android.Views.View.IOnApplyWindowInsetsListener
    {
        private WindowInsets _returnInsets;

        public WindowInsets OnApplyWindowInsets(Android.Views.View v, WindowInsets insets)
        {
            //we are saving system insets BEFORE the fullscreen flag was applied
            //and system insets became zero
            if (_returnInsets == null)
            {
                Super.Screen.TopInset = insets.SystemWindowInsetTop / Super.Screen.Density;

                bool invalidate = false;

                if (Super.StatusBarHeight != Super.Screen.TopInset)
                    invalidate = true;

                Super.StatusBarHeight = Super.Screen.TopInset;

                InsetsChanged?.Invoke(this, null);

                _returnInsets = insets.ReplaceSystemWindowInsets(
                    insets.SystemWindowInsetLeft,
                    0,
                    insets.SystemWindowInsetRight,
                    insets.SystemWindowInsetBottom
                );
            }
            return _returnInsets;
        }
    }

    public static int GetNavigationHeight(Context context)
    {

        int statusBarHeight = 0, totalHeight = 0, contentHeight = 0;
        int resourceId = context.Resources.GetIdentifier("navigation_bar_height", "dimen", "android");
        if (resourceId > 0)
        {
            statusBarHeight = context.Resources.GetDimensionPixelSize(resourceId);
            totalHeight = context.Resources.DisplayMetrics.HeightPixels;
            contentHeight = totalHeight - statusBarHeight;
        }

        return statusBarHeight;
    }

    public static int GetStatusBarHeight(Context context)
    {

        int statusBarHeight = 0, totalHeight = 0, contentHeight = 0;
        int resourceId = context.Resources.GetIdentifier("status_bar_height", "dimen", "android");
        if (resourceId > 0)
        {
            statusBarHeight = context.Resources.GetDimensionPixelSize(resourceId);
            totalHeight = context.Resources.DisplayMetrics.HeightPixels;
            contentHeight = totalHeight - statusBarHeight;
        }

        return statusBarHeight;
    }

    //public static void ClearImagesCache()
    //{
    //    var glide = Glide.Get(Platform.CurrentActivity);
    //    Task.Run(async () =>
    //    {
    //        glide.ClearDiskCache();
    //    }).ConfigureAwait(false);

    //    MainThread.BeginInvokeOnMainThread(() =>
    //    {
    //        glide.ClearMemory();
    //    });
    //}


    public static void SetNavigationBarColor(
        Microsoft.Maui.Graphics.Color colorBar,
        Microsoft.Maui.Graphics.Color colorSeparator,
        bool darkStatusBarTint)
    {
        if (Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.Lollipop)
            return;

        var activity = Platform.CurrentActivity;

        if (activity == null)
        {
            ExecAfterInit += (s, a) =>
            {
                SetNavigationBarColor(colorBar, colorSeparator, darkStatusBarTint);
            };
            return;
        }

        var window = activity.Window;

        window.ClearFlags(WindowManagerFlags.TranslucentNavigation);
        window.AddFlags(WindowManagerFlags.DrawsSystemBarBackgrounds);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            if (darkStatusBarTint)
            {
                if (Build.VERSION.SdkInt > (BuildVersionCodes)27)
                {
                    if (colorSeparator == Colors.Transparent)
                        window.NavigationBarDividerColor =
                            Microsoft.Maui.Graphics.Color.FromArgb("#FF222222").ToAndroid();
                    else
                        window.NavigationBarDividerColor = colorSeparator.ToAndroid();
                }

                window.SetNavigationBarColor(colorBar.ToAndroid());

                if (Build.VERSION.SdkInt > (BuildVersionCodes)26)
                {
                    // Fetch the current flags.
                    var lFlags = activity.Window.DecorView.SystemUiVisibility;

                    var mask = ~(StatusBarVisibility)SystemUiFlags.LightNavigationBar;

                    // Update the SystemUiVisibility dependening on whether we want a Light or Dark theme.
                    activity.Window.DecorView.SystemUiVisibility = lFlags & mask;
                }
            }
            else
            {
                //todo share everywhere !!!
                if (Build.VERSION.SdkInt > (BuildVersionCodes)27)
                {
                    if (colorSeparator == Colors.Transparent)
                        window.NavigationBarDividerColor =
                            Microsoft.Maui.Graphics.Color.FromArgb("#FFeeeeee").ToAndroid();
                    else
                        window.NavigationBarDividerColor = colorSeparator.ToAndroid();
                }

                window.SetNavigationBarColor(colorBar.ToAndroid());

                if (Build.VERSION.SdkInt > (BuildVersionCodes)26)
                {

                    // Fetch the current flags.
                    var lFlags = activity.Window.DecorView.SystemUiVisibility;
                    // Update the SystemUiVisibility dependening on whether we want a Light or Dark theme.
                    activity.Window.DecorView.SystemUiVisibility =
                        lFlags | (StatusBarVisibility)SystemUiFlags.LightNavigationBar;
                }

            }
        }
    }

    /// <summary>
    /// Completely hides the status bar on Android
    /// </summary>
    public static void HideStatusBar()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var activity = Platform.CurrentActivity as AndroidX.AppCompat.App.AppCompatActivity;
            if (activity?.Window != null)
            {
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R)
                {
                    // Android 11+ (API 30+)
                    var windowInsetsController = activity.Window.InsetsController;
                    if (windowInsetsController != null)
                    {
                        windowInsetsController.Hide(AndroidX.Core.View.WindowInsetsCompat.Type.StatusBars());
                        windowInsetsController.SystemBarsBehavior = AndroidX.Core.View.WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
                    }
                }
                else
                {
                    // Android 10 and below
                    var decorView = activity.Window.DecorView;
                    var uiOptions = (int)decorView.SystemUiVisibility;
                    uiOptions |= (int)Android.Views.SystemUiFlags.Fullscreen;
                    uiOptions |= (int)Android.Views.SystemUiFlags.HideNavigation;
                    decorView.SystemUiVisibility = (Android.Views.StatusBarVisibility)uiOptions;
                }
            }
        });
    }

    /// <summary>
    /// Shows the status bar on Android
    /// </summary>
    public static void ShowStatusBar()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
          

            var activity = Platform.CurrentActivity as AndroidX.AppCompat.App.AppCompatActivity;
            if (activity?.Window != null)
            {
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R)
                {
                    // Android 11+ (API 30+)
                    var windowInsetsController = activity.Window.InsetsController;
                    windowInsetsController?.Show(AndroidX.Core.View.WindowInsetsCompat.Type.StatusBars());
                }
                else
                {
                    // Android 10 and below
                    var decorView = activity.Window.DecorView;
                    var uiOptions = (int)decorView.SystemUiVisibility;
                    uiOptions &= ~(int)Android.Views.SystemUiFlags.Fullscreen;
                    uiOptions &= ~(int)Android.Views.SystemUiFlags.HideNavigation;
                    decorView.SystemUiVisibility = (Android.Views.StatusBarVisibility)uiOptions;
                }
            }
        });
    }

    public static void SetWhiteTextStatusBar()
    {
        if (Build.VERSION.SdkInt > Android.OS.BuildVersionCodes.M)
        {

            var activity = Platform.CurrentActivity;

            if (activity == null)
            {
                ExecAfterInit += (s, a) =>
                {
                    SetWhiteTextStatusBar();
                };
                return;
            }

            var window = activity.Window;

            // Fetch the current flags.
            var lFlags = window.DecorView.SystemUiVisibility;

            var mask = ~(StatusBarVisibility)SystemUiFlags.LightStatusBar;

            window.DecorView.SystemUiVisibility = lFlags & mask;
        }
    }

    public static void SetBlackTextStatusBar()
    {
        if (Build.VERSION.SdkInt > Android.OS.BuildVersionCodes.M)
        {
            var activity = Platform.CurrentActivity;

            if (activity == null)
            {
                ExecAfterInit += (s, a) =>
                {
                    SetBlackTextStatusBar();
                };
                return;
            }

            var window = activity.Window;

            // Fetch the current flags.
            var lFlags = window.DecorView.SystemUiVisibility;
            // Update the SystemUiVisibility dependening on whether we want a Light or Dark theme.
            window.DecorView.SystemUiVisibility = lFlags | (StatusBarVisibility)SystemUiFlags.LightStatusBar;
        }
    }

    public class FrameCallback : Java.Lang.Object, Choreographer.IFrameCallback
    {
        public FrameCallback(Action<long> callback)
        {
            _callback = callback;
        }

        Action<long> _callback;

        public void DoFrame(long frameTimeNanos)
        {
            _callback?.Invoke(frameTimeNanos);
        }

    }

    /// <summary>
    /// Opens web link in native browser
    /// </summary>
    /// <param name="link"></param>
    public static void OpenLink(string link)
    {
        try
        {
            var intent2 = new Intent(Intent.ActionView,
                Android.Net.Uri.Parse(link));
            intent2.AddFlags(ActivityFlags.NewTask);
            Android.App.Application.Context.StartActivity(intent2);
        }
        catch (Exception e)
        {
            Super.Log(e);
        }

    }

    /// <summary>
    /// Lists assets inside the Resources/Raw subfolder
    /// </summary>
    /// <param name="subfolder"></param>
    /// <returns></returns>
    public static IEnumerable<string> ListResources(string subfolder)
    {
        AssetManager assets = Platform.AppContext.Assets;
        string[] files = assets.List(subfolder);
        return files;
    }

    #region modern screenshot

    public static Task<byte[]> CaptureScreenshotAsync()
    {
        var rootView = Platform.CurrentActivity.Window.DecorView.RootView;
        return CaptureScreenshotAsync(rootView, Platform.CurrentActivity);
    }

    public static async Task<byte[]> CaptureScreenshotAsync(Android.Views.View view, Android.App.Activity activity)
    {
        if (view.Height < 1 || view.Width < 1)
            return null;

        byte[] buffer = null;

        if ((int)Build.VERSION.SdkInt < 24)
        {
            view.DrawingCacheEnabled = true;

            view.BuildDrawingCache(true);

            using (var screenshot = Bitmap.CreateBitmap(
                view.Width,
                view.Height,
                Bitmap.Config.Argb8888))
            {
                var canvas = new Canvas(screenshot);

                view.Draw(canvas);

                using (var stream = new MemoryStream())
                {
                    screenshot.Compress(Bitmap.CompressFormat.Png, 100, stream);
                    buffer = stream.ToArray();
                }
            }

            view.DrawingCacheEnabled = false;

            return buffer;
        }

        bool wait = true;

        using var helper = new ScreenshotHelper(view, activity);

        helper.Capture((Bitmap bitmap) =>
        {
            try
            {

                if (!helper.Error)
                {
                    using (var stream = new MemoryStream())
                    {
                        bitmap.Compress(Bitmap.CompressFormat.Png, 100, stream);
                        buffer = stream.ToArray();
                    }
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                wait = false;
            }
        });


        while (wait)
        {
            await Task.Delay(1);
        }

        return buffer;
    }

    public class ScreenshotHelper : Java.Lang.Object, PixelCopy.IOnPixelCopyFinishedListener
    {


        public void OnPixelCopyFinished(int copyResult)
        {
            var stop = true;
            if (copyResult == (int)PixelCopyResult.Success)
            {
                Error = false;
            }
            else
            {
                Error = true;
            }

            _callback(_bitmap);
            Task.Run(StopBackgroundThread);
        }

        public bool Error { get; protected set; }

        public ScreenshotHelper(Android.Views.View view, Android.App.Activity activity)
        {
            _view = view;
            _activity = activity;

            _bitmap = Bitmap.CreateBitmap(
                _view.Width,
                _view.Height,
                Bitmap.Config.Argb8888);
        }

        // Starts a background thread and its {@link Handler}.
        private void StartBackgroundThread()
        {
            _BackgroundThread = new HandlerThread("ScreeshotMakerBackground");
            _BackgroundThread.Start();
            _BackgroundHandler = new Handler(_BackgroundThread.Looper);
        }

        // Stops the background thread and its {@link Handler}.
        private void StopBackgroundThread()
        {
            try
            {
                _BackgroundThread.QuitSafely();
                _BackgroundThread.Join();
                _BackgroundThread = null;
                _BackgroundHandler = null;
            }
            catch (Exception)
            {
                //e.PrintStackTrace();
            }
        }

        public void Capture(Action<Bitmap> callback)
        {
            //var locationOfViewInWindow = new int[2];
            //_view.GetLocationInWindow(locationOfViewInWindow);
            _callback = callback;

            try
            {
                StartBackgroundThread();
                //todo could create-use background handler
                PixelCopy.Request(_activity.Window, _bitmap, this,
                    _BackgroundHandler);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Task.Run(StopBackgroundThread);
            }

        }

        private Android.Views.View _view;
        private Android.App.Activity _activity;
        private Bitmap _bitmap;
        private HandlerThread _BackgroundThread;
        private Handler _BackgroundHandler;
        private Action<Bitmap> _callback;


        public new void Dispose()
        {
            _bitmap?.Dispose();
            _bitmap = null;
            _activity = null;
            _view = null;
            _callback = null;

            base.Dispose();
        }

    }



    #endregion

    private static bool _keepScreenOn;
    /// <summary>
    /// Prevents display from auto-turning off  Everytime you set this the setting will be applied.
    /// </summary>
    public static bool KeepScreenOn
    {
        get
        {
            return _keepScreenOn;
        }
        set
        {
            if (value)
            {
                Platform.CurrentActivity.Window.AddFlags(WindowManagerFlags.KeepScreenOn);
            }
            else
            {
                Platform.CurrentActivity.Window.ClearFlags(WindowManagerFlags.KeepScreenOn);
            }
            _keepScreenOn = value;
        }
    }
}

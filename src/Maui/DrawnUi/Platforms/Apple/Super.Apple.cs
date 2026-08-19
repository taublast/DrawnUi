using System.Runtime.InteropServices;
using DrawnUi.Views;
using Foundation;
using SkiaSharp.Views.iOS;
using UIKit;

namespace DrawnUi.Draw;

public partial class Super
{

    public static float GetDisplayRefreshRate(float fallback)
    {
        return (int)UIScreen.MainScreen.MaximumFramesPerSecond;
    }

    public static float RefreshRate { get; protected set; }

    /// <summary>
    /// Opens web link in native browser
    /// </summary>
    /// <param name="link"></param>
    public static void OpenLink(string link)
    {
        try
        {
            var url = new NSUrl(link);
            var res = UIApplication.SharedApplication.OpenUrl(url);
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
        NSBundle mainBundle = NSBundle.MainBundle;
        string resourcesPath = mainBundle.ResourcePath;
        string subfolderPath = Path.Combine(resourcesPath, subfolder);

        if (Directory.Exists(subfolderPath))
        {
            string[] files = Directory.GetFiles(subfolderPath);
            return files.Select(Path.GetFileName).ToList();
        }
        else
        {
            return new List<string>();
        }
    }

    public static async Task<byte[]> CaptureScreenshotAsync()
    {
        UIWindow window;
        if (UIDevice.CurrentDevice.CheckSystemVersion(13, 0))
        {
            window = UIApplication.SharedApplication.Windows.First();
            if (window.GetType().Name.Contains("Popup"))
            {
                var maybe = UIApplication.SharedApplication.Windows.FirstOrDefault(x => x != window);
                if (maybe != null)
                {
                    window = maybe;
                }
            }
        }
        else
        {
            window = UIApplication.SharedApplication.KeyWindow;
        }

        UIGraphics.BeginImageContextWithOptions(window.Bounds.Size, false, UIScreen.MainScreen.Scale);
        window.Layer.RenderInContext(UIGraphics.GetCurrentContext());
        UIImage image = UIGraphics.GetImageFromCurrentImageContext();
        UIGraphics.EndImageContext();
        using (NSData data = image.AsPNG())
        {
            var bytes = new byte[data.Length];
            Marshal.Copy(data.Bytes, bytes, 0, Convert.ToInt32(data.Length));
            return bytes;
        }
    }

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
            UIApplication.SharedApplication.IdleTimerDisabled = value;
            _keepScreenOn = value;
        }
    }

    private static readonly object MetalViewsLock = new();
    private static readonly List<WeakReference<SKMetalViewRetained>> MetalViews = new();

    internal static void RegisterMetalView(SKMetalViewRetained view)
    {
        if (view == null)
            return;

        lock (MetalViewsLock)
        {
            PruneMetalViewsLocked();

            ApplyMetalViewPacing(view, MaxFps);

            MetalViews.Add(new WeakReference<SKMetalViewRetained>(view));
        }

    }

    internal static void UnregisterMetalView(SKMetalViewRetained view)
    {
        if (view == null)
            return;

        lock (MetalViewsLock)
        {
            for (var index = MetalViews.Count - 1; index >= 0; index--)
            {
                if (!MetalViews[index].TryGetTarget(out var attachedView) || ReferenceEquals(attachedView, view))
                {
                    MetalViews.RemoveAt(index);
                }
            }
        }
    }

    internal static void UpdateRegisteredMetalViewsPacing(int fps)
    {
        if (MainThread.IsMainThread)
        {
            UpdateRegisteredMetalViewsPacingOnMainThread(fps);
            return;
        }

        MainThread.BeginInvokeOnMainThread(() => UpdateRegisteredMetalViewsPacingOnMainThread(fps));
    }

    internal static void ApplyMetalViewPacing(SKMetalViewRetained view, int fps)
    {
        if (MainThread.IsMainThread)
        {
            ApplyMetalViewPacingOnMainThread(view, fps);
            return;
        }

        MainThread.BeginInvokeOnMainThread(() => ApplyMetalViewPacingOnMainThread(view, fps));
    }

    /// <summary>
    /// Picks who paces this view's frames.
    /// CAPPED (MaxFps > 0): the MTKView runs continuously at the capped rate and IS the pacer.
    /// On-demand mode cannot cap evenly — the next frame is scheduled by an invalidate issued
    /// from INSIDE the current draw and CoreAnimation honors it this vsync or the next, measured
    /// as 33/66ms alternation at MaxFps=30 while the tick loop itself was a perfect 33.4ms.
    /// UNCAPPED (MaxFps == 0): the shared CADisplayLink paces, already running at the display's
    /// own rate, and the view draws on demand. No frames-per-second value is written to the view
    /// at all — whatever the panel does is what happens. Writing one would mean inventing a
    /// number, and MTKView has no "native rate" sentinel: unlike CADisplayLink, 0 is not
    /// documented there and leaves the view with no valid draw interval, so a continuous view
    /// that got 0 never draws at all.
    /// </summary>
    private static void ApplyMetalViewPacingOnMainThread(SKMetalViewRetained view, int fps)
    {
        if (view == null || view.Handle == IntPtr.Zero)
            return;

        if (fps > 0)
        {
            view.Paused = false;
            view.EnableSetNeedsDisplay = false;
            view.PreferredFramesPerSecond = fps;
        }
        else
        {
            //display link drives it via SetNeedsDisplay, see SKGLViewHandlerRetained.OnInvalidateSurface
            view.Paused = true;
            view.EnableSetNeedsDisplay = true;
        }
    }

    private static void UpdateRegisteredMetalViewsPacingOnMainThread(int fps)
    {
        lock (MetalViewsLock)
        {
            for (var index = MetalViews.Count - 1; index >= 0; index--)
            {
                if (!MetalViews[index].TryGetTarget(out var attachedView) || attachedView?.Handle == IntPtr.Zero)
                {
                    MetalViews.RemoveAt(index);
                    continue;
                }

                ApplyMetalViewPacingOnMainThread(attachedView, fps);
            }
        }
    }

    private static void PruneMetalViewsLocked()
    {
        for (var index = MetalViews.Count - 1; index >= 0; index--)
        {
            if (!MetalViews[index].TryGetTarget(out var attachedView) || attachedView?.Handle == IntPtr.Zero)
            {
                MetalViews.RemoveAt(index);
            }
        }
    }
}

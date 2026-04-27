using DrawnUi.Views;

namespace DrawnUi.Draw;

public partial class Super
{
    private static readonly object MetalViewsLock = new();
    private static readonly List<WeakReference<SKMetalViewRetained>> MetalViews = new();

    internal static void RegisterMetalView(SKMetalViewRetained view)
    {
        if (view == null)
            return;

        lock (MetalViewsLock)
        {
            PruneMetalViewsLocked();

            if (MaxFps > 0)
            {
                ApplyMetalViewPreferredFramesPerSecond(view, MaxFps);
            }

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

    internal static void UpdateRegisteredMetalViewsPreferredFramesPerSecond(int fps)
    {
        if (MainThread.IsMainThread)
        {
            UpdateRegisteredMetalViewsPreferredFramesPerSecondOnMainThread(fps);
            return;
        }

        MainThread.BeginInvokeOnMainThread(() => UpdateRegisteredMetalViewsPreferredFramesPerSecondOnMainThread(fps));
    }

    private static void ApplyMetalViewPreferredFramesPerSecond(SKMetalViewRetained view, int fps)
    {
        if (MainThread.IsMainThread)
        {
            ApplyMetalViewPreferredFramesPerSecondOnMainThread(view, fps);
            return;
        }

        MainThread.BeginInvokeOnMainThread(() => ApplyMetalViewPreferredFramesPerSecondOnMainThread(view, fps));
    }

    private static void ApplyMetalViewPreferredFramesPerSecondOnMainThread(SKMetalViewRetained view, int fps)
    {
        if (view?.Handle == IntPtr.Zero)
            return;

        view.PreferredFramesPerSecond = fps > 0 ? fps : 0;
    }

    private static void UpdateRegisteredMetalViewsPreferredFramesPerSecondOnMainThread(int fps)
    {
        var preferredFramesPerSecond = fps > 0 ? fps : 0;

        lock (MetalViewsLock)
        {
            for (var index = MetalViews.Count - 1; index >= 0; index--)
            {
                if (!MetalViews[index].TryGetTarget(out var attachedView) || attachedView?.Handle == IntPtr.Zero)
                {
                    MetalViews.RemoveAt(index);
                    continue;
                }

                attachedView.PreferredFramesPerSecond = preferredFramesPerSecond;
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

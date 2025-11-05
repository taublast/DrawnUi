// ---------------------------------------------------------------
// Platforms/iOS/VisibilityAwarePlatformView.cs
// ---------------------------------------------------------------
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using CoreGraphics;
using Foundation;
using Microsoft.Maui.Platform;
using UIKit;
using ContentView = Microsoft.Maui.Platform.ContentView;

namespace DrawnUi.Controls;

[Register("VisibilityAwarePlatformView")]
public class VisibilityAwarePlatformView : ContentView
{
    private WeakReference<DrawnView>? _virtualView;

    // Weak collection that works in .NET MAUI
    private sealed class ObserverToken { }
    private readonly ConditionalWeakTable<UIView, ObserverToken> _ancestorObservers = new();



    public DrawnView? VirtualView
    {
        get => _virtualView?.TryGetTarget(out var v) == true ? v : null;
        set => _virtualView = value is null ? null : new WeakReference<DrawnView>(value);
    }

    public VisibilityAwarePlatformView(DrawnView parent)
    {
        VirtualView = parent;
        BackgroundColor = UIColor.Clear;
    }

    // -----------------------------------------------------------------
    // Lifecycle – we need the *correct* method names
    // -----------------------------------------------------------------
    public override void WillMoveToSuperview(UIView? newSuperview)
    {
        base.WillMoveToSuperview(newSuperview);
        UpdateAncestorObservers();
        CheckVisibility();
    }

    public override void MovedToSuperview()
    {
        base.MovedToSuperview();
        UpdateAncestorObservers();
        CheckVisibility();
    }

    public override void MovedToWindow()
    {
        base.MovedToWindow();
        CheckVisibility();
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        CheckVisibility();          // self-layout changes
    }

    public override bool Hidden
    {
        get => base.Hidden;
        set
        {
            if (base.Hidden != value)
            {
                base.Hidden = value;
                CheckVisibility();
            }
        }
    }

    // -----------------------------------------------------------------
    // KVO on every ancestor (isHidden + bounds + center → frame)
    // -----------------------------------------------------------------
    private void UpdateAncestorObservers()
    {
 

        // ---- remove old observers ------------------------------------------------
        foreach (var kvp in _ancestorObservers)
        {
            var view = kvp.Key;
            try
            {
                view.RemoveObserver(this, "hidden");
                view.RemoveObserver(this, "isHidden");
                view.RemoveObserver(this, "bounds");
                view.RemoveObserver(this, "center");
                view.RemoveObserver(this, "transform");
                view.RemoveObserver(this, "superview");
                view.RemoveObserver(this, "window");
            }
            catch { /* ignore – view may already be gone */ }
        }
        _ancestorObservers.Clear();

        // ---- add new observers ---------------------------------------------------
        UIView? current = Superview;
        int level = 1;
        while (current != null)
        {
            Debug.WriteLine($"[DrawnView] add new observers to {VirtualView.Tag} {level++}");
            try
            {
                current.AddObserver(this, new NSString("superview"), NSKeyValueObservingOptions.New, IntPtr.Zero);
                current.AddObserver(this, new NSString("window"), NSKeyValueObservingOptions.New, IntPtr.Zero);

                current
                    .AddObserver(this,
                                    new NSString("transform"),
                                    NSKeyValueObservingOptions.New,
                                    IntPtr.Zero);

                current.AddObserver(this,
                    new NSString("hidden"),
                    NSKeyValueObservingOptions.New,
                    IntPtr.Zero);

                current.AddObserver(this,
                                    new NSString("isHidden"),
                                    NSKeyValueObservingOptions.New,
                                    IntPtr.Zero);

                current.AddObserver(this,
                                    new NSString("bounds"),
                                    NSKeyValueObservingOptions.New,
                                    IntPtr.Zero);

                current.AddObserver(this,
                                    new NSString("center"),
                                    NSKeyValueObservingOptions.New,
                                    IntPtr.Zero);

                // store a weak reference
                _ancestorObservers.Add(current, new ObserverToken());
            }
            catch { /* ignore – e.g. view is not KVO-compliant */ }

            current = current.Superview;
        }
    }

    public override void ObserveValue(NSString keyPath,
                                      NSObject ofObject,
                                      NSDictionary change,
                                      IntPtr context)
    {
        Debug.WriteLine($"[DrawnView] ObserveValue {VirtualView.Tag} {keyPath}");

        if (keyPath == "hidden" || keyPath == "isHidden" || keyPath == "bounds" || keyPath == "center" || keyPath == "transform")
        {
            CheckVisibility();
        }
        else if (keyPath == "superview" || keyPath == "window")
        {
            // The ancestor tree changed → rebuild observers
            UpdateAncestorObservers();
            CheckVisibility();
        }
        else
        {
            base.ObserveValue(keyPath, ofObject, change, context);
        }
    }

    // -----------------------------------------------------------------
    // Visibility test
    // -----------------------------------------------------------------
    public void CheckVisibility()
    {
        if (VirtualView != null)
        {
            bool nowHidden = !IsVisibleOnScreen();
            VirtualView.IsHiddenInViewTree = nowHidden;
        }
    }

    private bool IsVisibleOnScreen()
    {
        // No window → definitely hidden
        if (Window == null || Hidden || Alpha <= 0.01f)
            return false;

        // Any ancestor hidden?
        UIView? cur = this;
        while (cur != null)
        {
            if (cur.Hidden) return false;
            cur = cur.Superview;
        }

        // Frame intersection with the screen
        var rectInWindow = ConvertRectToView(Bounds, Window);
        return CGRect.Intersect(rectInWindow, Window.Bounds) != CGRect.Empty;
    }

    // -----------------------------------------------------------------
    // Cleanup
    // -----------------------------------------------------------------
    ~VisibilityAwarePlatformView()
    {
        foreach (var kvp in _ancestorObservers)
        {
            var view = kvp.Key;
            try
            {
                view.RemoveObserver(this, "isHidden");
                view.RemoveObserver(this, "bounds");
                view.RemoveObserver(this, "center");
            }
            catch { }
        }
    }
}

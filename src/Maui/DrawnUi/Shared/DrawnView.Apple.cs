using System.Runtime.CompilerServices;
using CoreAnimation;
using CoreGraphics;
using Foundation;
using Microsoft.Maui.Handlers;
using UIKit;

namespace DrawnUi.Views;

public partial class DrawnView
{

    /// <summary>
    /// To optimize rendering and not update controls that are inside storyboard that is offscreen or hidden
    /// Apple - UI thread only !!!
    /// If you set 
    /// </summary>
    /// <param name="element"></param>
    public void CheckElementVisibility(VisualElement element)
    {
        NeedCheckParentVisibility = false;

        //Debug.WriteLine($"[DrawnView] CheckElementVisibility for {Tag} ");

        if (Handler?.PlatformView == null || !IsVisible)
        {
            IsHiddenInViewTree = true;
            return;
        }

#if NONE
        if (Handler != null && Handler.PlatformView is DrawnUi.Controls.VisibilityAwarePlatformView native)
        {
            native.CheckVisibility();
        }
#else
        IsHiddenInViewTree = !GetIsVisibleWithParent(this);
        //IsHiddenInViewTree = !IsElementVisibleInParentChain(Handler?.PlatformView as UIView);
#endif
    }

    #region native check

    private bool _wasVisible = true;
    private bool _checkVisibility;
    private DateTime _visibilityChangedTime;
    private readonly TimeSpan _visibilityCheckDelay = TimeSpan.FromSeconds(0.1);



    /// <summary>
    /// Check if element is visible through entire parent chain
    /// </summary>
    private bool IsElementVisibleInParentChain(UIView element)
    {
        //Debug.WriteLine($"[DrawnView] check {Tag}");

        // Quick checks first
        if (element.Hidden ||
            element.Alpha <= 0 ||
            element.Frame.Width <= 0 ||
            element.Frame.Height <= 0)
        {
            return false;
        }

        // Start with element bounds in its own coordinate space
        var currentBounds = new CGRect(0, 0, element.Frame.Width, element.Frame.Height);
        UIView current = element;

        // Walk up parent chain
        while (current.Superview != null)
        {
            UIView parent = current.Superview;

            // Check parent visibility
            if (parent.Hidden ||
                parent.Alpha <= 0 ||
                parent.Frame.Width <= 0 ||
                parent.Frame.Height <= 0)
            {
                return false;
            }

            try
            {
                // Convert current bounds to parent's coordinate system
                var transformedBounds = current.ConvertRectToView(currentBounds, parent);

                // Parent's bounds in its own coordinate space
                var parentBounds = new CGRect(0, 0, parent.Frame.Width, parent.Frame.Height);

                // Check if element is clipped by parent's ClipsToBounds
                if (parent.ClipsToBounds || parent is UIScrollView)
                {
                    if (!AreRectanglesIntersecting(transformedBounds, parentBounds))
                    {
                        return false;
                    }
                }

                // Special handling for UIScrollView
                if (parent is UIScrollView scrollView)
                {
                    var visibleBounds = new CGRect(
                        scrollView.ContentOffset.X,
                        scrollView.ContentOffset.Y,
                        scrollView.Frame.Width,
                        scrollView.Frame.Height
                    );

                    var contentBounds = current.ConvertRectToView(currentBounds, scrollView);

                    if (!AreRectanglesIntersecting(contentBounds, visibleBounds))
                    {
                        return false;
                    }
                }

                currentBounds = transformedBounds;
                current = parent;
            }
            catch
            {
                // Elements not properly connected
                return false;
            }
        }

        // Check if we're in a window
        return current.Window != null || current as UIWindow != null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AreRectanglesIntersecting(CGRect rect1, CGRect rect2)
    {
        return rect1.Left < rect2.Right &&
               rect1.Right > rect2.Left &&
               rect1.Top < rect2.Bottom &&
               rect1.Bottom > rect2.Top;
    }

    public virtual void OnLayoutChanged()
    {
        _checkVisibility = true;
        _visibilityChangedTime = DateTime.UtcNow;

        CheckElementVisibility(this);
    }

    private void OnChangesDetected()
    {
        if (!_checkVisibility)
            return;

        var delay = DateTime.UtcNow - _visibilityChangedTime;
        if (delay < _visibilityCheckDelay)
            return;

        if (Handler?.PlatformView is UIView view)
        {
            _checkVisibility = false;

            var hide = !IsElementVisibleInParentChain(view);
            if (hide != IsHiddenInViewTree)
            {
                IsHiddenInViewTree = hide;
            }

            UpdateBackend();
        }
    }

    #endregion

    protected virtual void OnSizeChanged()
    {
        if (Handler?.PlatformView is UIKit.UIView nativeView)
        {
            nativeView.ClipsToBounds = true;
        }

        UpdateBackend();

        Update();
    }

    void UpdateBackend()
    {
        if (((View)CanvasView)?.Handler is SKGLViewHandlerRetained handler && handler.PlatformView is SKMetalViewRetained platformView)
        {
            MetalBackend = platformView.MetalBackend;
        }
        else
        {
            MetalBackend = null;
        }
    }

    public GRMtlBackendContext MetalBackend { get; protected set; }

    protected virtual void PlatformHardwareAccelerationChanged()
    {
        UpdateBackend();
    }

    public virtual void SetupRenderingLoop()
    {
        Super.OnFrame -= OnFrame;
        Super.OnFrame += OnFrame;
    }

    private void OnFrame(object sender, EventArgs e)
    {
        if (CheckCanDraw())
        {
            if (NeedCheckParentVisibility)
            {
                CheckElementVisibility(this);
            }

            if (CanDraw)
            {
                CanvasView.Update();
            }

        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void UpdatePlatform()
    {
        IsDirty = true;
    }

    public bool CheckCanDraw()
    {
        return
            IsDirty &&
            CanvasView != null && this.Handler != null && this.Handler.PlatformView != null
                && !CanvasView.IsDrawing
               && !(UpdateLocks > 0 && StopDrawingWhenUpdateIsLocked)
               && IsVisible && Super.EnableRendering;
    }

    protected virtual void DisposePlatform()
    {
        Super.OnFrame -= OnFrame;
    }

    protected virtual void InitFrameworkPlatform(bool subscribe)
    {
        if (subscribe)
        {

        }
        else
        {

        }
    }

}


using System.Runtime.CompilerServices;
using CoreAnimation;
using Foundation;
using Microsoft.Maui.Handlers;

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
        IsHiddenInViewTree = !GetIsVisibleWithParent(this);

        //if (element != null)
        //{
        //    //WARNING this must be called form UI thread only!

        //    if (element.Handler != null)
        //    {
        //        if (element.Handler.PlatformView is UIKit.UIView iosView)
        //        {
        //            if (iosView.Hidden)
        //            {
        //                IsHiddenInViewTree = true;
        //                return;
        //            }
        //        }
        //    }
        //    else
        //    {
        //        if (element.GetVisualElementWindow() == null)
        //        {
        //            IsHiddenInViewTree = true;
        //            return;
        //        }
        //    }



        //}


        //IsHiddenInViewTree = false;
    }

    protected virtual void OnSizeChanged()
    {
        if (Handler?.PlatformView is UIKit.UIView nativeView)
        {
            nativeView.ClipsToBounds = true;
        }

        Update();
    }

    protected virtual void PlatformHardwareAccelerationChanged()
    {

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
               && !(UpdateLocks>0 && StopDrawingWhenUpdateIsLocked)
               && IsVisible && Super.EnableRendering;
    }

    protected virtual void DisposePlatform()
    {
        Super.OnFrame -= OnFrame;
    }
}


#define CHOREOGRAPHER //otherwise will use Looper like Windows 

using Android.Views;
using DrawnUi.Controls;
using View = Android.Views.View;

namespace DrawnUi.Views
{
    public partial class DrawnView
    {

        private bool IsElementVisibleInParentChain(View element)
        {
            // Safety: Check if view is attached to window before checking visibility
            if (element == null || element.WindowToken == null)
                return false;

            if (element.Visibility != ViewStates.Visible ||
                element.Width <= 0 ||
                element.Height <= 0)
                return false;

            var currentBounds = new RectF(0, 0, element.Width, element.Height);
            View current = element;

            while (true)
            {
                var parent = current.Parent;
                if (parent is not View parentView)
                    break; // Reached root 

                if (parentView.Visibility != ViewStates.Visible ||
                    parentView.Width <= 0 ||
                    parentView.Height <= 0)
                    return false;

                try
                {
                    int[] childLoc = new int[2];
                    int[] parentLoc = new int[2];

                    current.GetLocationInWindow(childLoc);
                    parentView.GetLocationInWindow(parentLoc);

                    float offsetX = childLoc[0] - parentLoc[0];
                    float offsetY = childLoc[1] - parentLoc[1];

                    var transformedBounds = new RectF(
                        currentBounds.Left + offsetX,
                        currentBounds.Top + offsetY,
                        currentBounds.Right + offsetX,
                        currentBounds.Bottom + offsetY
                    );

                    var parentBounds = new RectF(0, 0, parentView.Width, parentView.Height);

                    if (!AreRectanglesIntersecting(transformedBounds, parentBounds))
                        return false;

                    currentBounds = transformedBounds;
                    current = parentView;
                }
                catch
                {
                    return false; 
                }
            }

            return true;
        }

 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AreRectanglesIntersecting(RectF rect1, RectF rect2)
        {
            return rect1.Left < rect2.Right &&
                   rect1.Right > rect2.Left &&
                   rect1.Top < rect2.Bottom &&
                   rect1.Bottom > rect2.Top;
        }


        public class LayoutChangedListener : GlobalLayoutListener<DrawnView>
        {
            public LayoutChangedListener(View view, DrawnView control) : base(view, control)
            {
            }

            public override void OnGlobalLayout()
            {
                // Safety: Check if control is not disposed AND view is still attached to window
                if (Control != null && View != null && View.WindowToken != null)
                {
                    Control.NeedCheckParentVisibility = true;
                }
            }
        }

        LayoutChangedListener _layoutChangedListener;

        protected virtual void InitFrameworkPlatform(bool subscribe)
        {
            if (subscribe)
            {
                if (Handler?.PlatformView is Android.Views.View element)
                {
                    _layoutChangedListener = new LayoutChangedListener(element, this);
                }
            }
            else
            {
                _layoutChangedListener?.Release();
                _layoutChangedListener = null;
            }
        }

        public void ResetFocus()
        {
            if (this.Handler != null && Handler.PlatformView is Android.Views.View view)
            {
                var focused = view.FindFocus();
                if (focused != null)
                {
                    focused.ClearFocus();
                }
            }

            TouchEffect.CloseKeyboard();
        }

        protected virtual void OnSizeChanged()
        {
            if (Handler != null) //this is basically for clipping SkiaMauiElement
            {
                var layout = this.Handler.PlatformView as Android.Views.ViewGroup;
                layout.SetClipChildren(true);
                layout.ClipBounds = new Android.Graphics.Rect(0, 0, (int)(Width * RenderingScale), (int)(Height * RenderingScale));
            }

            Update();
        }

        /// <summary>
        /// To optimize rendering and not update controls that are inside storyboard that is offscreen or hidden
        /// Apple - UI thread only !!!
        /// If you set 
        /// </summary>
        /// <param name="element"></param>
        public void CheckElementVisibility(VisualElement element)
        {
            NeedCheckParentVisibility = false;

            if (Handler?.PlatformView is not View platformView)
            {
                IsHiddenInViewTree = true;
                return;
            }

            // Safety: Don't check visibility if view is detached from window
            if (platformView.WindowToken == null)
            {
                IsHiddenInViewTree = true;
                return;
            }

            // The heavy lifting is delegated to the Android-specific method above
            IsHiddenInViewTree = !IsElementVisibleInParentChain(platformView);

            //if (element != null)
            //{

            //    if (element.Handler != null)
            //    {
            //        if (element.Handler.PlatformView is Android.Views.View nativeView)
            //        {
            //            if (nativeView.Visibility != Android.Views.ViewStates.Visible)
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

#if CHOREOGRAPHER


        protected virtual void DisposePlatform()
        {
            Super.OnFrame -= OnChoreographer;
        }

        object lockFrame = new();

        private void OnChoreographer(object sender, EventArgs e)
        {
            //lock (lockFrame)
            {
                if (CheckCanDraw())
                {
                    if (NeedCheckParentVisibility)
                        CheckElementVisibility(this);

                    if (CanDraw)
                    {
                        CanvasView.Update();
                    }
                }
            }
        }


        public virtual void SetupRenderingLoop()
        {
            Super.OnFrame -= OnChoreographer;
            Super.OnFrame += OnChoreographer;
        }

        protected virtual void PlatformHardwareAccelerationChanged()
        {

        }

#else

        protected virtual void DisposePlatform()
        {
            Looper?.Dispose();
        }

        public virtual void SetupRenderingLoop()
        {
            Looper?.Dispose();
            Looper = new(OnFrame);
            Looper.Start(120);
        }

        protected virtual void PlatformHardwareAccelerationChanged()
        {
            if (Looper != null && Looper.IsRunning)
            {
                SetupRenderingLoop();
            }
        }

        Looper Looper { get; set; }

        public void OnFrame()
        {
            //background thread

            if (CheckCanDraw())
            {
                OrderedDraw = true;
                if (NeedCheckParentVisibility)
                    CheckElementVisibility(this);

                CanvasView?.Update();
            }
        }



#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void UpdatePlatform()
        {
            IsDirty = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckCanDraw()
        {
            return 

                CanvasView != null && this.Handler != null && this.Handler.PlatformView != null
               //&& !CanvasView.IsDrawing
               && IsDirty
               && !(UpdateLocks>0 && StopDrawingWhenUpdateIsLocked)
               && IsVisible && Super.EnableRendering;
        }

        protected void OnHandlerChangedInternal()
        {
            if (this.Handler != null)
            {
                //intercept focus other wise native entries above will not unfocus
                if (this.Handler?.PlatformView is Android.Views.View view)
                {
                    view.Focusable = true;
                    view.FocusableInTouchMode = true;
                    if (view is ViewGroup group)
                    {
                        group.DescendantFocusability = DescendantFocusability.BeforeDescendants;
                    }
                }
            }
        }



    }
}

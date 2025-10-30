using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Rect = Windows.Foundation.Rect;
using Visibility = Microsoft.UI.Xaml.Visibility;

namespace DrawnUi.Views
{
    public partial class DrawnView
    {
        private int _frameSkipCounter = 0;
        private DateTime _viewportChangedTime;
        private readonly TimeSpan _visibilityCheckDelay = TimeSpan.FromSeconds(0.1);
        private bool _wasVisible = true;

        /// <summary>
        /// Check if element is visible within all parent bounds
        /// </summary>
        public void CheckElementVisibility(VisualElement element)
        {
            NeedCheckParentVisibility = false;

            if (Handler?.PlatformView is not FrameworkElement platformElement)
            {
                IsHiddenInViewTree = true;
                return;
            }

            IsHiddenInViewTree = !IsElementVisibleInParentChain(platformElement);
        }

        /// <summary>
        /// Check if element is visible through entire parent chain
        /// </summary>
        private bool IsElementVisibleInParentChain(FrameworkElement element)
        {
            // Quick checks first
            if (element.Visibility == Visibility.Collapsed ||
                element.ActualWidth <= 0 ||
                element.ActualHeight <= 0)
            {
                return false;
            }

            // Start with element bounds
            var elementBounds = new Rect(0, 0, element.ActualWidth, element.ActualHeight);

            DependencyObject current = element;
            DependencyObject parent = VisualTreeHelper.GetParent(current);

            // Walk up parent chain
            while (parent != null)
            {
                if (parent is FrameworkElement parentElement)
                {
                    // Check parent visibility
                    if (parentElement.Visibility == Visibility.Collapsed ||
                        parentElement.ActualWidth <= 0 ||
                        parentElement.ActualHeight <= 0)
                    {
                        return false;
                    }

                    try
                    {
                        // Transform to parent space
                        if (current is UIElement currentUI)
                        {
                            var transform = currentUI.TransformToVisual(parentElement);
                            elementBounds = transform.TransformBounds(elementBounds);
                        }

                        // Check if within parent bounds
                        var parentBounds = new Rect(0, 0, parentElement.ActualWidth, parentElement.ActualHeight);

                        if (!AreRectanglesIntersecting(elementBounds, parentBounds))
                        {
                            return false;
                        }

                        // Special handling for ScrollViewer
                        if (parent is Microsoft.UI.Xaml.Controls.ScrollViewer scrollViewer)
                        {
                            var viewportBounds = new Rect(0, 0, scrollViewer.ViewportWidth, scrollViewer.ViewportHeight);
                            if (!AreRectanglesIntersecting(elementBounds, viewportBounds))
                            {
                                return false;
                            }
                        }
                    }
                    catch
                    {
                        // Elements not in same visual tree
                        return false;
                    }
                }

                current = parent;
                parent = VisualTreeHelper.GetParent(current);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool AreRectanglesIntersecting(Rect rect1, Rect rect2)
        {
            return rect1.Left < rect2.Right &&
                   rect1.Right > rect2.Left &&
                   rect1.Top < rect2.Bottom &&
                   rect1.Bottom > rect2.Top;
        }

        protected virtual void InitFrameworkPlatform(bool subscribe)
        {
            if (subscribe)
            {
                if (Handler?.PlatformView is FrameworkElement element)
                {
                    element.EffectiveViewportChanged += ElementOnEffectiveViewportChanged;
                }
                CompositionTarget.Rendering += OnRendering;
            }
            else
            {
                CompositionTarget.Rendering -= OnRendering;
                if (Handler?.PlatformView is FrameworkElement element)
                {
                    element.EffectiveViewportChanged -= ElementOnEffectiveViewportChanged;
                }
            }
        }

        private bool _checkVisibility;

        private void ElementOnEffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {
            _checkVisibility = true;
            _viewportChangedTime = DateTime.UtcNow;
            //Debug.WriteLine($"[DrawnView] CHANGED {Tag}");
        }

        private void OnRendering(object sender, object e)
        {
            if (!_checkVisibility)
                return;

            var delay = DateTime.UtcNow - _viewportChangedTime;
            if (delay < _visibilityCheckDelay)
                return;

            if (Handler?.PlatformView is FrameworkElement element)
            {
                _checkVisibility = false;

                var hide = ! IsElementVisibleInParentChain(element);
                if (hide != IsHiddenInViewTree)
                {
                    IsHiddenInViewTree = hide;
                }
            }
        }


        protected virtual void OnSizeChanged()
        {
            if (Handler?.PlatformView is ContentPanel layout)
            {
                layout.Clip = new RectangleGeometry
                {
                    Rect = new Rect(0, 0, Width, Height)
                };
            }
            Update();
        }

        public virtual void SetupRenderingLoop()
        {
#if !LEGACY
            Super.OnFrame -= OnSuperFrame;
            Super.OnFrame += OnSuperFrame;
#endif
        }

        protected virtual void PlatformHardwareAccelerationChanged()
        {
        }

#if LEGACY
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckCanDraw()
        {
            if (UpdateLocked && StopDrawingWhenUpdateIsLocked)
                return false;

            return CanvasView != null
                   && !IsRendering
                   && IsDirty
                   && IsVisible
                   && !IsHiddenInViewTree; // Added check
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void UpdatePlatform()
        {
            IsDirty = true;
            if (!OrderedDraw && CheckCanDraw())
            {
                OrderedDraw = true;
                InvalidateCanvas();
            }
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void UpdatePlatform()
        {
            IsDirty = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckCanDraw()
        {
            return CanvasView != null
                   && this.Handler != null
                   && this.Handler.PlatformView != null
                   && !CanvasView.IsDrawing
                   && IsDirty
                   && !(UpdateLocks > 0 && StopDrawingWhenUpdateIsLocked)
                   && IsVisible
                   && Super.EnableRendering
                   && !IsHiddenInViewTree; // Added check
        }
#endif

        private void OnSuperFrame(object sender, EventArgs e)
        {
            if (CheckCanDraw())
            {
                if (NeedCheckParentVisibility)
                {
                    CheckElementVisibility(this);
                }
                if (CanDraw && !IsHiddenInViewTree)
                {
                    CanvasView.Update();
                }
            }
        }

        protected virtual void DisposePlatform()
        {
            InitFrameworkPlatform(false); // Unsubscribe from rendering
            Super.OnFrame -= OnSuperFrame;
        }
    }
}

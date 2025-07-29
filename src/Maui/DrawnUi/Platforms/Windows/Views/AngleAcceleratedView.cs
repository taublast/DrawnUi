using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.UI.Core;
using Binding = Microsoft.UI.Xaml.Data.Binding;
using DesignMode = Windows.ApplicationModel.DesignMode;
using Rect = Windows.Foundation.Rect;
using ThreadPool = Windows.System.Threading.ThreadPool;
using Visibility = Microsoft.UI.Xaml.Visibility;

namespace DrawnUi.Views
{
    // main fix: EnsureRenderSurface() should be called inside Invalidate, otherwise the canvas might never be re-rendered if returning to visible tree out of being hidden

    public class AngleAcceleratedView : SwapChainPanel
    {
        private static readonly DependencyProperty ProxyVisibilityProperty =
            DependencyProperty.Register(
                "ProxyVisibility",
                typeof(Visibility),
                typeof(AngleAcceleratedView),
                new PropertyMetadata(Visibility.Visible, OnVisibilityChanged));

        private static readonly bool designMode = DesignMode.DesignModeEnabled;

        private readonly object locker = new object();

        private bool isVisible = true;
        private bool isLoaded = false;

        private GlesContext glesContext;

        private IAsyncAction renderLoopWorker;
        private IAsyncAction renderOnceWorker;

        private bool enableRenderLoop;

        private double lastCompositionScaleX = 0.0;
        private double lastCompositionScaleY = 0.0;

        private bool pendingSizeChange = false;

        public AngleAcceleratedView()
        {
            lastCompositionScaleX = CompositionScaleX;
            lastCompositionScaleY = CompositionScaleY;

            glesContext = null;

            renderLoopWorker = null;
            renderOnceWorker = null;

            DrawInBackground = false;
            EnableRenderLoop = false;

            ContentsScale = CompositionScaleX;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            CompositionScaleChanged += OnCompositionChanged;
            SizeChanged += OnSizeChanged;

            var binding = new Binding
            {
                Path = new PropertyPath(nameof(Visibility)),
                Source = this
            };
            SetBinding(ProxyVisibilityProperty, binding);
        }

        public bool DrawInBackground { get; set; }

        public double ContentsScale { get; private set; }

        public bool EnableRenderLoop
        {
            get => enableRenderLoop;
            set
            {
                if (enableRenderLoop != value)
                {
                    enableRenderLoop = value;
                    UpdateRenderLoop(value);
                }
            }
        }

        public void Invalidate()
        {
            if (!isLoaded || EnableRenderLoop)
                return;

            EnsureRenderSurface();

            if (DrawInBackground)
            {
                lock (locker)
                {
                    // if we haven't fired a render thread, start one
                    if (renderOnceWorker == null)
                    {
                        renderOnceWorker = ThreadPool.RunAsync(RenderOnce);
                    }
                }
            }
            else
            {
                // draw on this thread, blocking
                RenderFrame();
            }
        }

        protected virtual void OnRenderFrame(Rect rect)
        {
        }

        protected virtual void OnDestroyingContext()
        {
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            glesContext = new GlesContext();

            isLoaded = true;

            ContentsScale = CompositionScaleX;

            //EnsureRenderSurface();
            UpdateRenderLoop(EnableRenderLoop);
            Invalidate();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            OnDestroyingContext();

            CompositionScaleChanged -= OnCompositionChanged;
            SizeChanged -= OnSizeChanged;

            UpdateRenderLoop(false);
            DestroyRenderSurface();

            isLoaded = false;

            glesContext?.Dispose();
            glesContext = null;
        }

        private static void OnVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AngleAcceleratedView panel && e.NewValue is Visibility visibility)
            {
                panel.isVisible = visibility == Visibility.Visible;
                panel.UpdateRenderLoop(panel.isVisible && panel.EnableRenderLoop);
                panel.Invalidate();
            }
        }

        private void OnCompositionChanged(SwapChainPanel sender, object args)
        {
            if (lastCompositionScaleX == CompositionScaleX &&
                lastCompositionScaleY == CompositionScaleY)
            {
                return;
            }

            lastCompositionScaleX = CompositionScaleX;
            lastCompositionScaleY = CompositionScaleY;

            pendingSizeChange = true;

            ContentsScale = CompositionScaleX;

            DestroyRenderSurface();

            Invalidate();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            pendingSizeChange = true;

 
            Invalidate();
        }

        private void EnsureRenderSurface()
        {
            if (isLoaded && glesContext?.HasSurface != true && ActualWidth > 0 && ActualHeight > 0)
            {
                // detach and re-attach the size events as we need to go after the event added by ANGLE
                // otherwise our size will still be the old size

                SizeChanged -= OnSizeChanged;
                CompositionScaleChanged -= OnCompositionChanged;

                glesContext.CreateSurface(this, null, CompositionScaleX);

                SizeChanged += OnSizeChanged;
                CompositionScaleChanged += OnCompositionChanged;
            }
        }

        private void DestroyRenderSurface()
        {
            glesContext?.DestroySurface();
        }

        private void RenderFrame()
        {
            if (designMode || !isLoaded || !isVisible || glesContext?.HasSurface != true)
                return;

            glesContext.MakeCurrent();

            if (pendingSizeChange)
            {
                pendingSizeChange = false;

                if (!EnableRenderLoop)
                    glesContext.SwapBuffers();
            }

            glesContext.GetSurfaceDimensions(out var panelWidth, out var panelHeight);
            glesContext.SetViewportSize(panelWidth, panelHeight);

            OnRenderFrame(new Rect(0, 0, panelWidth, panelHeight));

            if (!glesContext.SwapBuffers())
            {
                // The call to eglSwapBuffers might not be successful (i.e. due to Device Lost)
                // If the call fails, then we must reinitialize EGL and the GL resources.
            }
        }

        private void UpdateRenderLoop(bool start)
        {
            if (!isLoaded)
                return;

            lock (locker)
            {
                if (start)
                {
                    // if the render loop is not running, start it
                    if (renderLoopWorker?.Status != AsyncStatus.Started)
                    {
                        renderLoopWorker = ThreadPool.RunAsync(RenderLoop);
                    }
                }
                else
                {
                    // stop the current render loop
                    renderLoopWorker?.Cancel();
                    renderLoopWorker = null;
                }
            }
        }

        private void RenderOnce(IAsyncAction action)
        {
            if (DrawInBackground)
            {
                // run on this background thread
                RenderFrame();
            }
            else
            {
                // run in the main thread, block this one
                Dispatcher.RunAsync(CoreDispatcherPriority.Normal, RenderFrame).AsTask().Wait();
            }

            lock (locker)
            {
                // we are finished, so null out
                renderOnceWorker = null;
            }
        }

        private void RenderLoop(IAsyncAction action)
        {
            while (action.Status == AsyncStatus.Started)
            {
                if (DrawInBackground)
                {
                    // run on this background thread
                    RenderFrame();
                }
                else
                {
                    // run in the main thread, block this one
                    var tcs = new TaskCompletionSource();
                    DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                    {
                        RenderFrame();
                        tcs.SetResult();
                    });
                    tcs.Task.Wait();
                }
            }
        }
    }
}





 

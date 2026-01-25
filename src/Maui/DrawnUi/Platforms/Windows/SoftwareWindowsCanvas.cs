using System;
using System.Buffers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel;
using Windows.Storage.Streams;
using WinRT;
using Binding = Microsoft.UI.Xaml.Data.Binding;
using Canvas = Microsoft.UI.Xaml.Controls.Canvas;
using DesignMode = Windows.ApplicationModel.DesignMode;
using Stretch = Microsoft.UI.Xaml.Media.Stretch;
using Visibility = Microsoft.UI.Xaml.Visibility;


namespace DrawnUi.Controls
{
    public partial class SoftwareWindowsCanvas : Canvas, IDisposable
    {
        public void Dispose()
        {
            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;
            SizeChanged -= OnSizeChanged;
            FreeBitmap();
        }

        private const float DpiBase = 96.0f;

        private static readonly DependencyProperty ProxyVisibilityProperty =
            DependencyProperty.Register(
                "ProxyVisibility",
                typeof(Visibility),
                typeof(SoftwareWindowsCanvas),
                new PropertyMetadata(Visibility.Visible, OnVisibilityChanged));

        private static bool designMode = DesignMode.DesignModeEnabled;

        private IntPtr _pixels;
        private WriteableBitmap bitmap;
        private ImageBrush brush;
        private bool ignorePixelScaling;
        private bool isVisible = true;

        // workaround for https://github.com/mono/SkiaSharp/issues/1118
        private int loadUnloadCounter = 0;

        public SoftwareWindowsCanvas()
        {
            if (designMode)
                return;

#if !WINDOWS
			var display = DisplayInformation.GetForCurrentView();
			OnDpiChanged(display);
#endif

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;

            var binding = new Binding
            {
                Path = new PropertyPath(nameof(Visibility)),
                Source = this
            };
            SetBinding(ProxyVisibilityProperty, binding);
        }

        public SKSize CanvasSize { get; private set; }

        public bool IgnorePixelScaling
        {
            get => ignorePixelScaling;
            set
            {
                ignorePixelScaling = value;
                Invalidate();
            }
        }

        public double Dpi { get; private set; } = 1;

        public event EventHandler<SkiaSharp.Views.Windows.SKPaintSurfaceEventArgs> PaintSurface;

        protected virtual void OnPaintSurface(SkiaSharp.Views.Windows.SKPaintSurfaceEventArgs e)
        {
            PaintSurface?.Invoke(this, e);
        }

        private static void OnVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SoftwareWindowsCanvas canvas && e.NewValue is Visibility visibility)
            {
                canvas.isVisible = visibility == Visibility.Visible;
                canvas.Invalidate();
            }
        }

#if WINDOWS
        private void OnXamlRootChanged(XamlRoot xamlRoot = null, XamlRootChangedEventArgs e = null)
        {
            var root = xamlRoot ?? XamlRoot;
            var newDpi = root?.RasterizationScale ?? 1.0;
            if (newDpi != Dpi)
            {
                Dpi = newDpi;
                UpdateBrushScale();
                Invalidate();
            }
        }
#else
		private void OnDpiChanged(DisplayInformation sender, object args = null)
		{
			Dpi = sender.LogicalDpi / DpiBase;
			Invalidate();
		}
#endif

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Invalidate();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            loadUnloadCounter++;
            if (loadUnloadCounter != 1)
                return;

#if WINDOWS
            XamlRoot.Changed += OnXamlRootChanged;
            OnXamlRootChanged();
#else
			var display = DisplayInformation.GetForCurrentView();
			display.DpiChanged += OnDpiChanged;

			OnDpiChanged(display);
#endif

        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            loadUnloadCounter--;
            if (loadUnloadCounter != 0)
                return;

#if WINDOWS
            if (XamlRoot != null)
            {
                XamlRoot.Changed -= OnXamlRootChanged;
            }
#else
			var display = DisplayInformation.GetForCurrentView();
			display.DpiChanged -= OnDpiChanged;
#endif

            FreeBitmap();
        }

        public void Invalidate()
        {
#if WINDOWS
            DispatcherQueue?.TryEnqueue(DispatcherQueuePriority.Normal, DoInvalidate);
#else
			Dispatcher?.RunAsync(CoreDispatcherPriority.Normal, DoInvalidate);
#endif
        }

        private void DoInvalidate()
        {
            if (designMode)
                return;

            if (!isVisible)
                return;

            var (info, viewSize, dpi) = CreateBitmap();

            if (info.Width <= 0 || info.Height <= 0)
            {
                CanvasSize = SKSize.Empty;
                return;
            }

            // This is here because the property name is confusing and backwards.
            // True actually means to ignore the pixel scaling of the raw pixel
            // size and instead use the view size such that sizes match the XAML
            // elements.
            var matchUI = IgnorePixelScaling;

            var userVisibleSize = matchUI ? viewSize : info.Size;
            CanvasSize = userVisibleSize;

            using (var surface = SKSurface.Create(info, _pixels, info.RowBytes))
            {
                if (matchUI)
                {
                    var canvas = surface.Canvas;
                    canvas.Scale(dpi);
                    canvas.Save();
                }

                OnPaintSurface(new SkiaSharp.Views.Windows.SKPaintSurfaceEventArgs(surface, info.WithSize(userVisibleSize), info));
            }
            bitmap.Invalidate();
        }

        private (SKSizeI ViewSize, SKSizeI PixelSize, float Dpi) CreateSize()
        {
            var w = ActualWidth;
            var h = ActualHeight;

            if (!IsPositive(w) || !IsPositive(h))
                return (SKSizeI.Empty, SKSizeI.Empty, 1);

            var dpi = (float)Dpi;
            var viewSize = new SKSizeI((int)w, (int)h);
            var pixelSize = new SKSizeI((int)(w * dpi), (int)(h * dpi));

            return (viewSize, pixelSize, dpi);

            static bool IsPositive(double value)
            {
                return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
            }
        }

        [ComImport]
        [Guid("905a0fef-bc53-11df-8c49-001e4fc686da")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IBufferByteAccess
        {
            void GetBuffer(out IntPtr buffer, out uint capacity);
        }

        /// <summary>
        /// Gets the native pixel buffer pointer from a WriteableBitmap's PixelBuffer
        /// </summary>
        /// <param name="buffer">The IBuffer from WriteableBitmap.PixelBuffer</param>
        /// <returns>IntPtr pointing to the native pixel data</returns>
        unsafe IntPtr todoGetPixelsFrom(IBuffer buffer)
        {
            var bufferByteAccess = buffer.As<IBufferByteAccess>();
            bufferByteAccess.GetBuffer(out IntPtr pixelBuffer, out uint capacity);
            return pixelBuffer;
        }

        private (SKImageInfo Info, SKSizeI PixelSize, float Dpi) CreateBitmap()
        {
            var (viewSize, pixelSize, dpi) = CreateSize();
            var info = new SKImageInfo(pixelSize.Width, pixelSize.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

            if (bitmap?.PixelWidth != info.Width || bitmap?.PixelHeight != info.Height)
                FreeBitmap();

            if (bitmap == null && info.Width > 0 && info.Height > 0)
            {
                bitmap = new WriteableBitmap(info.Width, info.Height);
                _pixels = (IntPtr)todoGetPixelsFrom(bitmap.PixelBuffer);

                brush = new ImageBrush
                {
                    ImageSource = bitmap,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top,
                    Stretch = Stretch.None
                };
                UpdateBrushScale();

                Background = brush;
            }

            return (info, viewSize, dpi);
        }

        private void UpdateBrushScale()
        {
            if (brush == null)
                return;

            var scale = 1.0 / Dpi;

            brush.Transform = new ScaleTransform
            {
                ScaleX = scale,
                ScaleY = scale
            };
        }

        private void FreeBitmap()
        {
            Background = null;
            brush = null;
            bitmap = null;
            _pixels = IntPtr.Zero;
        }


    }
}

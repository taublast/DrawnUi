// Forked from SkiaSharp (MIT, https://github.com/mono/SkiaSharp) SkiaSharp.Views.Windows 4.148:
// the stock AngleSwapChainPanel recreates its EGL surface on every CompositionScaleChanged and sizes it by
// CompositionScale (DPI x ancestor scale transforms), which breaks accelerated canvases under animated
// XAML transforms (popup zoom: half-size buffer, 2x zoomed crop, visual detached from the animation).
// DrawnUi's fork sizes the surface by the real DPI only. Kept as close to upstream as possible.
using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using DrawnUi.Views.Gles;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.System.Threading;
using Windows.UI.Core;

namespace DrawnUi.Views;

public class DrawnSwapChainPanel : SwapChainPanel
{
	private static readonly DependencyProperty ProxyVisibilityProperty = DependencyProperty.Register("ProxyVisibility", typeof(Microsoft.UI.Xaml.Visibility), typeof(DrawnSwapChainPanel), new PropertyMetadata(Microsoft.UI.Xaml.Visibility.Visible, OnVisibilityChanged));

	private static readonly bool designMode = Windows.ApplicationModel.DesignMode.DesignModeEnabled;

	private readonly object locker = new object();

	private bool isVisible = true;

	private bool isLoaded;

	private global::DrawnUi.Views.Gles.GlesContext glesContext;

	private IAsyncAction renderLoopWorker;

	private IAsyncAction renderOnceWorker;

	private bool enableRenderLoop;

	private double lastCompositionScaleX;

	private double lastCompositionScaleY;

	private bool pendingSizeChange;

	public bool DrawInBackground { get; set; }

	public double ContentsScale { get; private set; }

	public bool EnableRenderLoop
	{
		get
		{
			return enableRenderLoop;
		}
		set
		{
			if (enableRenderLoop != value)
			{
				enableRenderLoop = value;
				UpdateRenderLoop(value);
			}
		}
	}

	public DrawnSwapChainPanel()
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
		Microsoft.UI.Xaml.Data.Binding val = new Microsoft.UI.Xaml.Data.Binding
		{
			Path = new PropertyPath("Visibility"),
			Source = this
		};
		SetBinding(ProxyVisibilityProperty, val);
	}

	public void Invalidate()
	{
		if (!isLoaded || EnableRenderLoop)
		{
			return;
		}
		if (DrawInBackground)
		{
			lock (locker)
			{
				if (renderOnceWorker == null)
				{
					renderOnceWorker = Windows.System.Threading.ThreadPool.RunAsync(RenderOnce);
				}
				return;
			}
		}
		RenderFrame();
	}

	protected virtual void OnRenderFrame(Windows.Foundation.Rect rect)
	{
	}

	protected virtual void OnDestroyingContext()
	{
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		try
		{
			glesContext = new global::DrawnUi.Views.Gles.GlesContext();
		}
		catch (Exception ex)
		{
			Super.Log(ex);
			return;
		}
		isLoaded = true;
		ContentsScale = RealDpi;
		lastCompositionScaleX = ContentsScale;
		lastCompositionScaleY = ContentsScale;
		EnsureRenderSurface();
		UpdateRenderLoop(EnableRenderLoop);
		Invalidate();
	}

	private void OnUnloaded(object sender, RoutedEventArgs e)
	{
		OnDestroyingContext();
		CompositionScaleChanged -= OnCompositionChanged;
		SizeChanged -= OnSizeChanged;
		UpdateRenderLoop(start: false);
		DestroyRenderSurface();
		isLoaded = false;
		glesContext?.Dispose();
		glesContext = null;
	}

	private static void OnVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is DrawnSwapChainPanel angleSwapChainPanel && e.NewValue is Microsoft.UI.Xaml.Visibility val)
		{
			angleSwapChainPanel.isVisible = val == Microsoft.UI.Xaml.Visibility.Visible;
			angleSwapChainPanel.UpdateRenderLoop(angleSwapChainPanel.isVisible && angleSwapChainPanel.EnableRenderLoop);
			angleSwapChainPanel.Invalidate();
		}
	}

	/// <summary>
	/// Real display scale for this panel. CompositionScale also folds in ancestor XAML scale transforms
	/// (animated popups, ScaleTo), which must NOT resize the GL surface.
	/// </summary>
	protected double RealDpi => XamlRoot?.RasterizationScale ?? CompositionScaleX;

	private void OnCompositionChanged(SwapChainPanel sender, object args)
	{
		var dpi = RealDpi;
		if (Math.Abs(CompositionScaleX - dpi) > 0.01)
		{
			// ancestor transform in flight: keep the DPI-sized surface, the composition scales the visual
			return;
		}

		if (lastCompositionScaleX != dpi || lastCompositionScaleY != dpi)
		{
			lastCompositionScaleX = dpi;
			lastCompositionScaleY = dpi;
			pendingSizeChange = true;
			ContentsScale = dpi;
			DestroyRenderSurface();
			EnsureRenderSurface();
			Invalidate();
		}
	}

	private void OnSizeChanged(object sender, SizeChangedEventArgs e)
	{
		pendingSizeChange = true;
		EnsureRenderSurface();
		Invalidate();
	}

	private void EnsureRenderSurface()
	{
		if (isLoaded)
		{
			global::DrawnUi.Views.Gles.GlesContext obj = glesContext;
			if ((obj == null || !obj.HasSurface) && ActualWidth > 0.0 && ActualHeight > 0.0)
			{
				SizeChanged -= OnSizeChanged;
				CompositionScaleChanged -= OnCompositionChanged;
				try
				{
					glesContext.CreateSurface(this, null, (float)RealDpi);
				}
				catch (Exception e)
				{
					Super.Log(e);
				}
				SizeChanged += OnSizeChanged;
				CompositionScaleChanged += OnCompositionChanged;
			}
		}
	}

	private void DestroyRenderSurface()
	{
		glesContext?.DestroySurface();
	}

	private void RenderFrame()
	{
		if (designMode || !isLoaded || !isVisible)
		{
			return;
		}
		global::DrawnUi.Views.Gles.GlesContext obj = glesContext;
		if (obj == null || !obj.HasSurface)
		{
			return;
		}
		glesContext.MakeCurrent();
		if (pendingSizeChange)
		{
			pendingSizeChange = false;
			if (!EnableRenderLoop)
			{
				glesContext.SwapBuffers();
			}
		}
		glesContext.GetSurfaceDimensions(out var width, out var height);
		glesContext.SetViewportSize(width, height);
		OnRenderFrame(new Windows.Foundation.Rect(0f, 0f, (float)width, (float)height));
		glesContext.SwapBuffers();
	}

	private void UpdateRenderLoop(bool start)
	{
		if (!isLoaded)
		{
			return;
		}
		lock (locker)
		{
			if (start)
			{
				IAsyncAction obj = renderLoopWorker;
				if (obj == null || obj.Status != AsyncStatus.Started)
				{
					renderLoopWorker = Windows.System.Threading.ThreadPool.RunAsync(RenderLoop);
				}
				return;
			}
			IAsyncAction obj2 = renderLoopWorker;
			if (obj2 != null)
			{
				obj2.Cancel();
			}
			renderLoopWorker = null;
		}
	}

	private void RenderOnce(IAsyncAction action)
	{
		if (DrawInBackground)
		{
			RenderFrame();
		}
		else
		{
			var tcsOnce = new TaskCompletionSource();
			DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () => { RenderFrame(); tcsOnce.SetResult(); });
			tcsOnce.Task.Wait();
		}
		lock (locker)
		{
			renderOnceWorker = null;
		}
	}

	private void RenderLoop(IAsyncAction action)
	{
		while (action.Status == AsyncStatus.Started)
		{
			if (DrawInBackground)
			{
				RenderFrame();
				continue;
			}
			TaskCompletionSource tcs = new TaskCompletionSource();
			DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
			{
				RenderFrame();
				tcs.SetResult();
			});
			tcs.Task.Wait();
		}
	}
}

// Forked from SkiaSharp (MIT, https://github.com/mono/SkiaSharp) SkiaSharp.Views.Windows 4.148:
// the stock AngleSwapChainPanel recreates its EGL surface on every CompositionScaleChanged and sizes it by
// CompositionScale (DPI x ancestor scale transforms), which breaks accelerated canvases under animated
// XAML transforms (popup zoom: half-size buffer, 2x zoomed crop, visual detached from the animation).
// DrawnUi's fork sizes the surface by the real DPI only. Kept as close to upstream as possible.
using System;
using Microsoft.UI.Xaml.Controls;
using WinRT;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace DrawnUi.Views.Gles;

internal class GlesContext : IDisposable
{
	private static nint eglDisplay = Egl.EGL_NO_DISPLAY;

	private bool isDisposed;

	private nint eglContext;

	private nint eglSurface;

	private nint eglConfig;

	public bool HasSurface => eglSurface != Egl.EGL_NO_SURFACE;

	// ANGLE reads EGLRenderResolutionScaleProperty / EGLRenderSurfaceSizeProperty through IPropertyValue.
	// A float/Size boxed by CsWinRT is NOT such an object (eglCreateWindowSurface hits an access violation),
	// which is why SkiaSharp ships a C++/WinRT helper: SkiaSharp.Views.WinUI.Native.PropertySetExtensions.
	// That helper is a runtime-only projection (SkiaSharp.NativeAssets.WinUI, always present next to
	// SkiaSharp.Views.Windows.dll) so it is bound by reflection; without it the property is skipped and
	// ANGLE falls back to the panel's own CompositionScale (stock SkiaSharp behavior, logged once).
	private static readonly Type NativePropertySetExtensions =
		Type.GetType("SkiaSharp.Views.WinUI.Native.PropertySetExtensions, SkiaSharp.Views.WinUI.Native.Projection", throwOnError: false);
	private static readonly System.Reflection.MethodInfo NativeAddSingle =
		NativePropertySetExtensions?.GetMethod("AddSingle", new[] { typeof(PropertySet), typeof(string), typeof(float) });
	private static readonly System.Reflection.MethodInfo NativeAddSize =
		NativePropertySetExtensions?.GetMethod("AddSize", new[] { typeof(PropertySet), typeof(string), typeof(Windows.Foundation.Size) });
	private static bool _warnedNoNativeHelper;

	private static void AddSingle(PropertySet set, string key, float value)
	{
		if (NativeAddSingle != null)
		{
			NativeAddSingle.Invoke(null, new object[] { set, key, value });
			return;
		}
		WarnNoNativeHelper();
	}

	private static void AddSize(PropertySet set, string key, Windows.Foundation.Size value)
	{
		if (NativeAddSize != null)
		{
			NativeAddSize.Invoke(null, new object[] { set, key, value });
			return;
		}
		WarnNoNativeHelper();
	}

	private static void WarnNoNativeHelper()
	{
		if (_warnedNoNativeHelper) return;
		_warnedNoNativeHelper = true;
		Super.Log("[DrawnUi] SkiaSharp.Views.WinUI.Native.Projection not found: accelerated canvas surface will follow CompositionScale (animated scale transforms may zoom the content)");
	}


	public GlesContext()
	{
		eglConfig = Egl.EGL_NO_CONFIG;
		eglContext = Egl.EGL_NO_CONTEXT;
		eglSurface = Egl.EGL_NO_SURFACE;
		InitializeDisplay();
		Initialize();
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!isDisposed)
		{
			DestroySurface();
			Cleanup();
			isDisposed = true;
		}
	}

	~GlesContext()
	{
		Dispose(disposing: false);
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	public void CreateSurface(SwapChainPanel panel, Windows.Foundation.Size? renderSurfaceSize, float? resolutionScale)
	{
		if (panel == null)
		{
			throw new ArgumentNullException("SwapChainPanel parameter is invalid");
		}
		nint eGL_NO_SURFACE = Egl.EGL_NO_SURFACE;
		int[] attrib_list = new int[1] { 12344 };
		PropertySet val = new PropertySet();
		val.Add("EGLNativeWindowTypeProperty", panel);
		if (renderSurfaceSize.HasValue)
		{
			AddSize(val, "EGLRenderSurfaceSizeProperty", renderSurfaceSize.Value);
		}
		if (resolutionScale.HasValue)
		{
			AddSingle(val, "EGLRenderResolutionScaleProperty", resolutionScale.Value);
		}
		var inspectable = val.As<IInspectable>();
		eGL_NO_SURFACE = Egl.eglCreateWindowSurface(eglDisplay, eglConfig, inspectable.ThisPtr, attrib_list);
		GC.KeepAlive(inspectable);
		GC.KeepAlive(val);
		if (eGL_NO_SURFACE == Egl.EGL_NO_SURFACE)
		{
			throw new Exception("Failed to create EGL surface");
		}
		eglSurface = eGL_NO_SURFACE;
	}

	public void GetSurfaceDimensions(out int width, out int height)
	{
		Egl.eglQuerySurface(eglDisplay, eglSurface, 12375, out width);
		Egl.eglQuerySurface(eglDisplay, eglSurface, 12374, out height);
	}

	public void SetViewportSize(int width, int height)
	{
		Gles.glViewport(0, 0, width, height);
	}

	public void DestroySurface()
	{
		if (eglDisplay != Egl.EGL_NO_DISPLAY && eglSurface != Egl.EGL_NO_SURFACE)
		{
			Egl.eglDestroySurface(eglDisplay, eglSurface);
			eglSurface = Egl.EGL_NO_SURFACE;
		}
	}

	public void MakeCurrent()
	{
		if (Egl.eglMakeCurrent(eglDisplay, eglSurface, eglSurface, eglContext) == 0)
		{
			throw new Exception("Failed to make EGLSurface current");
		}
	}

	public bool SwapBuffers()
	{
		return Egl.eglSwapBuffers(eglDisplay, eglSurface) == 1;
	}

	public void Reset()
	{
		Cleanup();
		Initialize();
	}

	private void InitializeDisplay()
	{
		if (eglDisplay != Egl.EGL_NO_DISPLAY)
		{
			return;
		}
		int[] attrib_list = new int[7] { 12803, 12808, 13220, 13226, 12815, 1, 12344 };
		int[] attrib_list2 = new int[11]
		{
			12803, 12808, 12804, 9, 12805, 3, 13220, 13226, 12815, 1,
			12344
		};
		int[] attrib_list3 = new int[9] { 12803, 12808, 12809, 12811, 13220, 13226, 12815, 1, 12344 };
		nint zero = IntPtr.Zero;
		eglDisplay = Egl.eglGetPlatformDisplayEXT(12802u, Egl.EGL_DEFAULT_DISPLAY, attrib_list);
		if (eglDisplay == Egl.EGL_NO_DISPLAY)
		{
			throw new Exception("Failed to get EGL display");
		}
		if (Egl.eglInitialize(eglDisplay, out var major, out var minor) != 0)
		{
			return;
		}
		eglDisplay = Egl.eglGetPlatformDisplayEXT(12802u, Egl.EGL_DEFAULT_DISPLAY, attrib_list2);
		if (eglDisplay == Egl.EGL_NO_DISPLAY)
		{
			throw new Exception("Failed to get EGL display");
		}
		if (Egl.eglInitialize(eglDisplay, out major, out minor) == 0)
		{
			eglDisplay = Egl.eglGetPlatformDisplayEXT(12802u, Egl.EGL_DEFAULT_DISPLAY, attrib_list3);
			if (eglDisplay == Egl.EGL_NO_DISPLAY)
			{
				throw new Exception("Failed to get EGL display");
			}
			if (Egl.eglInitialize(eglDisplay, out major, out minor) == 0)
			{
				throw new Exception("Failed to initialize EGL");
			}
		}
	}

	public void Initialize()
	{
		int[] attrib_list = new int[13]
		{
			12324, 8, 12323, 8, 12322, 8, 12321, 8, 12325, 8,
			12326, 8, 12344
		};
		int[] attrib_list2 = new int[3] { 12440, 2, 12344 };
		nint[] array = new nint[1];
		if (Egl.eglChooseConfig(eglDisplay, attrib_list, array, array.Length, out var num_config) == 0 || num_config == 0)
		{
			throw new Exception("Failed to choose first EGLConfig");
		}
		eglConfig = array[0];
		eglContext = Egl.eglCreateContext(eglDisplay, eglConfig, Egl.EGL_NO_CONTEXT, attrib_list2);
		if (eglContext == Egl.EGL_NO_CONTEXT)
		{
			throw new Exception("Failed to create EGL context");
		}
	}

	private void Cleanup()
	{
		if (eglDisplay != Egl.EGL_NO_DISPLAY && eglContext != Egl.EGL_NO_CONTEXT)
		{
			Egl.eglDestroyContext(eglDisplay, eglContext);
			eglContext = Egl.EGL_NO_CONTEXT;
		}
	}
}

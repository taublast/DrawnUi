// Forked from SkiaSharp (MIT, https://github.com/mono/SkiaSharp) SkiaSharp.Views.Windows 4.148:
// the stock AngleSwapChainPanel recreates its EGL surface on every CompositionScaleChanged and sizes it by
// CompositionScale (DPI x ancestor scale transforms), which breaks accelerated canvases under animated
// XAML transforms (popup zoom: half-size buffer, 2x zoomed crop, visual detached from the animation).
// DrawnUi's fork sizes the surface by the real DPI only. Kept as close to upstream as possible.
using System;
using System.Runtime.InteropServices;

namespace DrawnUi.Views.Gles;

internal static class Egl
{
	private const string libEGL = "libEGL.dll";

	public static readonly nint EGL_DEFAULT_DISPLAY = IntPtr.Zero;

	public static readonly nint EGL_NO_CONFIG = IntPtr.Zero;

	public static readonly nint EGL_NO_DISPLAY = IntPtr.Zero;

	public static readonly nint EGL_NO_CONTEXT = IntPtr.Zero;

	public static readonly nint EGL_NO_SURFACE = IntPtr.Zero;

	public const int EGL_FALSE = 0;

	public const int EGL_TRUE = 1;

	public const int EGL_SUCCESS = 12288;

	public const int EGL_BUFFER_SIZE = 12320;

	public const int EGL_ALPHA_SIZE = 12321;

	public const int EGL_BLUE_SIZE = 12322;

	public const int EGL_GREEN_SIZE = 12323;

	public const int EGL_RED_SIZE = 12324;

	public const int EGL_DEPTH_SIZE = 12325;

	public const int EGL_STENCIL_SIZE = 12326;

	public const int EGL_HEIGHT = 12374;

	public const int EGL_WIDTH = 12375;

	public const int EGL_NONE = 12344;

	public const int EGL_CONTEXT_CLIENT_VERSION = 12440;

	public const int EGL_SWAP_BEHAVIOR = 12435;

	public const int EGL_BUFFER_PRESERVED = 12436;

	public const int EGL_BUFFER_DESTROYED = 12437;

	public const int EGL_OPENGL_ES_API = 12448;

	public const int EGL_RENDERABLE_TYPE = 12352;

	public const int EGL_OPENGL_ES2_BIT = 4;

	public const int EGL_SURFACE_TYPE = 12339;

	public const int EGL_PBUFFER_BIT = 1;

	public const int EGL_SWAP_BEHAVIOR_PRESERVED_BIT = 1024;

	public const int EGL_EXPERIMENTAL_PRESENT_PATH_ANGLE = 13220;

	public const int EGL_EXPERIMENTAL_PRESENT_PATH_FAST_ANGLE = 13226;

	public const int EGL_EXPERIMENTAL_PRESENT_PATH_COPY_ANGLE = 13226;

	public const int EGL_PLATFORM_ANGLE_TYPE_ANGLE = 12803;

	public const int EGL_PLATFORM_ANGLE_MAX_VERSION_MAJOR_ANGLE = 12804;

	public const int EGL_PLATFORM_ANGLE_MAX_VERSION_MINOR_ANGLE = 12805;

	public const int EGL_PLATFORM_ANGLE_TYPE_DEFAULT_ANGLE = 12806;

	public const int EGL_PLATFORM_ANGLE_ANGLE = 12802;

	public const int EGL_PLATFORM_ANGLE_TYPE_D3D9_ANGLE = 12807;

	public const int EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE = 12808;

	public const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_ANGLE = 12809;

	public const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_HARDWARE_ANGLE = 12810;

	public const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_D3D_WARP_ANGLE = 12811;

	public const int EGL_PLATFORM_ANGLE_DEVICE_TYPE_D3D_REFERENCE_ANGLE = 12812;

	public const int EGL_PLATFORM_ANGLE_ENABLE_AUTOMATIC_TRIM_ANGLE = 12815;

	public const int EGL_FIXED_SIZE_ANGLE = 12801;

	public const string EGLNativeWindowTypeProperty = "EGLNativeWindowTypeProperty";

	public const string EGLRenderSurfaceSizeProperty = "EGLRenderSurfaceSizeProperty";

	public const string EGLRenderResolutionScaleProperty = "EGLRenderResolutionScaleProperty";

	[DllImport("libEGL.dll")]
	private static extern nint eglGetProcAddress([MarshalAs(UnmanagedType.LPStr)] string procname);

	[DllImport("libEGL.dll")]
	public static extern nint eglGetPlatformDisplayEXT(uint platform, nint native_display, int[] attrib_list);

	[DllImport("libEGL.dll")]
	public static extern int eglInitialize(nint dpy, out int major, out int minor);

	[DllImport("libEGL.dll")]
	public static extern int eglChooseConfig(nint dpy, int[] attrib_list, [In][Out] nint[] configs, int config_size, out int num_config);

	[DllImport("libEGL.dll")]
	public static extern nint eglCreateContext(nint dpy, nint config, nint share_context, int[] attrib_list);

	[DllImport("libEGL.dll")]
	public static extern nint eglCreateWindowSurface(nint dpy, nint config, nint win, int[] attrib_list);

	[DllImport("libEGL.dll")]
	public static extern int eglQuerySurface(nint dpy, nint surface, int attribute, out int value);

	[DllImport("libEGL.dll")]
	public static extern int eglDestroySurface(nint dpy, nint surface);

	[DllImport("libEGL.dll")]
	public static extern int eglMakeCurrent(nint dpy, nint draw, nint read, nint ctx);

	[DllImport("libEGL.dll")]
	public static extern int eglSwapBuffers(nint dpy, nint surface);

	[DllImport("libEGL.dll")]
	public static extern int eglDestroyContext(nint dpy, nint ctx);

	[DllImport("libEGL.dll")]
	public static extern int eglTerminate(nint dpy);

	[DllImport("libEGL.dll")]
	public static extern int eglGetError();

	[DllImport("libEGL.dll")]
	public static extern int eglBindAPI(uint api);

	[DllImport("libEGL.dll")]
	public static extern nint eglCreatePbufferSurface(nint dpy, nint config, int[] attrib_list);

	[DllImport("libEGL.dll")]
	public static extern int eglSurfaceAttrib(nint dpy, nint surface, int attribute, int value);
}

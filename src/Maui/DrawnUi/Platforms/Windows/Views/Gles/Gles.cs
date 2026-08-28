// Forked from SkiaSharp (MIT, https://github.com/mono/SkiaSharp) SkiaSharp.Views.Windows 4.148:
// the stock AngleSwapChainPanel recreates its EGL surface on every CompositionScaleChanged and sizes it by
// CompositionScale (DPI x ancestor scale transforms), which breaks accelerated canvases under animated
// XAML transforms (popup zoom: half-size buffer, 2x zoomed crop, visual detached from the animation).
// DrawnUi's fork sizes the surface by the real DPI only. Kept as close to upstream as possible.
using System.Runtime.InteropServices;

namespace DrawnUi.Views.Gles;

internal static class Gles
{
	private const string libGLESv2 = "libGLESv2.dll";

	public const int GL_FRAMEBUFFER_BINDING = 36006;

	public const int GL_RENDERBUFFER_BINDING = 36007;

	public const int GL_BGRA8_EXT = 37793;

	public const int GL_VERSION = 7938;

	public const int GL_EXTENSIONS = 7939;

	public const int GL_SUBPIXEL_BITS = 3408;

	public const int GL_RED_BITS = 3410;

	public const int GL_GREEN_BITS = 3411;

	public const int GL_BLUE_BITS = 3412;

	public const int GL_ALPHA_BITS = 3413;

	public const int GL_DEPTH_BITS = 3414;

	public const int GL_STENCIL_BITS = 3415;

	public const int GL_SAMPLES = 32937;

	public const int GL_DEPTH_BUFFER_BIT = 256;

	public const int GL_STENCIL_BUFFER_BIT = 1024;

	public const int GL_COLOR_BUFFER_BIT = 16384;

	public const int GL_NEAREST = 9728;

	public const int GL_READ_FRAMEBUFFER_ANGLE = 36008;

	public const int GL_DRAW_FRAMEBUFFER_ANGLE = 36009;

	public const int GL_DRAW_FRAMEBUFFER_BINDING_ANGLE = 36006;

	public const int GL_READ_FRAMEBUFFER_BINDING_ANGLE = 36010;

	public const int GL_FRAMEBUFFER = 36160;

	public const int GL_RENDERBUFFER = 36161;

	public const int GL_RENDERBUFFER_WIDTH = 36162;

	public const int GL_RENDERBUFFER_HEIGHT = 36163;

	public const int GL_RENDERBUFFER_INTERNAL_FORMAT = 36164;

	public const int GL_RENDERBUFFER_RED_SIZE = 36176;

	public const int GL_RENDERBUFFER_GREEN_SIZE = 36177;

	public const int GL_RENDERBUFFER_BLUE_SIZE = 36178;

	public const int GL_RENDERBUFFER_ALPHA_SIZE = 36179;

	public const int GL_RENDERBUFFER_DEPTH_SIZE = 36180;

	public const int GL_RENDERBUFFER_STENCIL_SIZE = 36181;

	public const int GL_COLOR_ATTACHMENT0 = 36064;

	public const int GL_DEPTH_ATTACHMENT = 36096;

	public const int GL_STENCIL_ATTACHMENT = 36128;

	public const int GL_DEPTH_COMPONENT16 = 33189;

	public const int GL_DEPTH_STENCIL_OES = 34041;

	public const int GL_UNSIGNED_INT_24_8_OES = 34042;

	public const int GL_DEPTH24_STENCIL8_OES = 35056;

	[DllImport("libGLESv2.dll")]
	public static extern void glGetIntegerv(uint pname, out int data);

	[DllImport("libGLESv2.dll")]
	public static extern nint glGetString(uint value);

	[DllImport("libGLESv2.dll")]
	public static extern void glViewport(int x, int y, int width, int height);

	[DllImport("libGLESv2.dll")]
	public static extern void glClearColor(float red, float green, float blue, float alpha);

	[DllImport("libGLESv2.dll")]
	public static extern void glClear(uint mask);

	[DllImport("libGLESv2.dll")]
	public static extern void glGenRenderbuffers(int n, [In][Out] uint[] buffers);

	[DllImport("libGLESv2.dll")]
	public static extern void glGenRenderbuffers(int n, ref uint buffer);

	[DllImport("libGLESv2.dll")]
	public static extern void glGenFramebuffers(int n, [In][Out] uint[] buffers);

	[DllImport("libGLESv2.dll")]
	public static extern void glGenFramebuffers(int n, ref uint buffer);

	[DllImport("libGLESv2.dll")]
	public static extern void glGetRenderbufferParameteriv(uint target, int pname, out int param);

	[DllImport("libGLESv2.dll")]
	public static extern void glBindRenderbuffer(uint target, uint buffer);

	[DllImport("libGLESv2.dll")]
	public static extern void glBindFramebuffer(uint target, uint framebuffer);

	[DllImport("libGLESv2.dll")]
	public static extern void glDeleteFramebuffers(int n, [In][Out] uint[] framebuffers);

	[DllImport("libGLESv2.dll")]
	public static extern void glDeleteFramebuffers(int n, ref uint framebuffer);

	[DllImport("libGLESv2.dll")]
	public static extern void glDeleteRenderbuffers(int n, [In][Out] uint[] renderbuffers);

	[DllImport("libGLESv2.dll")]
	public static extern void glDeleteRenderbuffers(int n, ref uint renderbuffer);

	[DllImport("libGLESv2.dll")]
	public static extern void glFramebufferRenderbuffer(uint target, uint attachment, uint renderbuffertarget, uint renderbuffer);
}

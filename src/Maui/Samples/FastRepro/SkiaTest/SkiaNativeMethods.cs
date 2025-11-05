using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Sandbox
{
    internal static partial class SkiaNativeMethods
    {
        private const string LibraryName = "libSkiaSharp";

        [LibraryImport(LibraryName, EntryPoint = "sk_canvas_save")]
        internal static partial void CanvasSave(IntPtr canvas);

        [LibraryImport(LibraryName, EntryPoint = "sk_canvas_restore")]
        internal static partial void CanvasRestore(IntPtr canvas);

        [LibraryImport(LibraryName, EntryPoint = "sk_canvas_translate")]
        internal static partial void CanvasTranslate(IntPtr canvas, float dx, float dy);

        [LibraryImport(LibraryName, EntryPoint = "sk_canvas_scale")]
        internal static partial void CanvasScale(IntPtr canvas, float sx, float sy);

        [LibraryImport(LibraryName, EntryPoint = "sk_canvas_clip_rect_with_operation")]
        internal static partial void CanvasClipRectWithOperation(
            IntPtr canvas,
            ref SkRectNative rect,
            SKClipOperation operation,
            [MarshalAs(UnmanagedType.I1)] bool doAA);

        [LibraryImport(LibraryName, EntryPoint = "sk_canvas_clear")]
        internal static partial void CanvasClear(IntPtr canvas, uint color);

        [LibraryImport(LibraryName, EntryPoint = "sk_canvas_draw_path")]
        internal static partial void CanvasDrawPath(
            IntPtr canvas,
            IntPtr path,
            IntPtr paint);

        [LibraryImport(LibraryName, EntryPoint = "sk_path_new")]
        internal static partial IntPtr PathNew();

        [LibraryImport(LibraryName, EntryPoint = "sk_path_delete")]
        internal static partial void PathDelete(IntPtr path);

        [LibraryImport(LibraryName, EntryPoint = "sk_path_reset")]
        internal static partial void PathReset(IntPtr path);

        [LibraryImport(LibraryName, EntryPoint = "sk_path_move_to")]
        internal static partial void PathMoveTo(IntPtr path, float x, float y);

        [LibraryImport(LibraryName, EntryPoint = "sk_path_line_to")]
        internal static partial void PathLineTo(IntPtr path, float x, float y);

        [LibraryImport(LibraryName, EntryPoint = "sk_path_quad_to")]
        internal static partial void PathQuadTo(IntPtr path, float x0, float y0, float x1, float y1);

        [LibraryImport(LibraryName, EntryPoint = "sk_path_cubic_to")]
        internal static partial void PathCubicTo(IntPtr path, float x0, float y0, float x1, float y1, float x2, float y2);

        [LibraryImport(LibraryName, EntryPoint = "sk_paint_new")]
        internal static partial IntPtr PaintNew();

        [LibraryImport(LibraryName, EntryPoint = "sk_paint_delete")]
        internal static partial void PaintDelete(IntPtr paint);

        [LibraryImport(LibraryName, EntryPoint = "sk_paint_set_antialias")]
        internal static partial void PaintSetAntialias(IntPtr paint, [MarshalAs(UnmanagedType.I1)] bool antialias);

        [LibraryImport(LibraryName, EntryPoint = "sk_paint_set_style")]
        internal static partial void PaintSetStyle(IntPtr paint, SKPaintStyle style);

        [LibraryImport(LibraryName, EntryPoint = "sk_paint_set_color")]
        internal static partial void PaintSetColor(IntPtr paint, uint color);

        [LibraryImport(LibraryName, EntryPoint = "sk_paint_set_stroke_width")]
        internal static partial void PaintSetStrokeWidth(IntPtr paint, float width);

        [LibraryImport(LibraryName, EntryPoint = "sk_paint_set_stroke_cap")]
        internal static partial void PaintSetStrokeCap(IntPtr paint, SKStrokeCap cap);

        [LibraryImport(LibraryName, EntryPoint = "sk_paint_set_stroke_join")]
        internal static partial void PaintSetStrokeJoin(IntPtr paint, SKStrokeJoin join);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint PackColor(byte a, byte r, byte g, byte b)
            => (uint)(a << 24 | r << 16 | g << 8 | b);
    }
}

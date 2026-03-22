using System.Runtime.CompilerServices;
using SkiaSharp;

namespace DrawnUi.Draw;

/// <summary>
/// Guarded setters for SKPaint properties. Each method compares a managed shadow value
/// before calling the native P/Invoke setter, eliminating redundant interop when the
/// value has not changed between frames.
/// Methods are prefixed with "Guard" to avoid conflicts with SkiaSharp's own instance methods.
/// </summary>
public static class SKPaintExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardColor(this SKPaint paint, ref SKColor cache, SKColor value)
    {
        if (cache != value) { cache = value; paint.Color = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardStyle(this SKPaint paint, ref SKPaintStyle cache, SKPaintStyle value)
    {
        if (cache != value) { cache = value; paint.Style = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardIsDither(this SKPaint paint, ref bool cache, bool value)
    {
        if (cache != value) { cache = value; paint.IsDither = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardIsAntialias(this SKPaint paint, ref bool cache, bool value)
    {
        if (cache != value) { cache = value; paint.IsAntialias = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardTextSize(this SKPaint paint, ref float cache, float value)
    {
        if (cache != value) { cache = value; paint.TextSize = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardStrokeWidth(this SKPaint paint, ref float cache, float value)
    {
        if (cache != value) { cache = value; paint.StrokeWidth = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardBlendMode(this SKPaint paint, ref SKBlendMode cache, SKBlendMode value)
    {
        if (cache != value) { cache = value; paint.BlendMode = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardTypeface(this SKPaint paint, ref SKTypeface cache, SKTypeface value)
    {
        if (!ReferenceEquals(cache, value)) { cache = value; paint.Typeface = value; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GuardFilterQuality(this SKPaint paint, ref SKFilterQuality cache, SKFilterQuality value)
    {
        if (cache != value) { cache = value; paint.FilterQuality = value; }
    }
}

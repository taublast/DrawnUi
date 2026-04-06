using System.Runtime.CompilerServices;

namespace DrawnUi.Draw
{
    public partial class SkiaLabel
    {
        /// <summary>
        /// Span-based measurement methods to avoid string allocations
        /// </summary>
        public static class SpanMeasurement
        {
            /// <summary>
            /// Measures text width using ReadOnlySpan without converting to string
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float MeasureTextWidthWithAdvanceSpan(SKPaint paint, ReadOnlySpan<char> textSpan)
            {
                return paint.MeasureText(textSpan);
            }

            /// <summary>
            /// Checks if a span represents a single space character
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsSpaceSpan(ReadOnlySpan<char> span, char spaceChar)
            {
                return span.Length == 1 && span[0] == spaceChar;
            }

            /// <summary>
            /// Finds the last non-space character index in a span
            /// </summary>
            public static int LastNonSpaceIndexSpan(ReadOnlySpan<char> textSpan)
            {
                for (int i = textSpan.Length - 1; i >= 0; i--)
                {
                    if (!char.IsWhiteSpace(textSpan[i]))
                    {
                        return i;
                    }
                }
                return -1;
            }

            /// <summary>
            /// Checks if glyph is always available without string conversion
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsGlyphAlwaysAvailableSpan(ReadOnlySpan<char> glyphSpan)
            {
                return glyphSpan.Length == 1 && glyphSpan[0] == '\n';
            }

            /// <summary>
            /// Converts ReadOnlySpan to string only when absolutely necessary for cache keys
            /// This is the only place where we allow span-to-string conversion for caching
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static string SpanToStringForCache(ReadOnlySpan<char> span)
            {
                // This is the controlled conversion point - only used for cache keys
                return span.ToString();
            }

            /// <summary>
            /// Measures partial text width using span-based operations where possible
            /// Falls back to string conversion only for cache compatibility
            /// </summary>
            public static float MeasurePartialTextWidthSpan(SKPaint paint, ReadOnlySpan<char> textSpan,
                bool needsShaping, float scale, SKTypeface paintTypeface)
            {
                // For simple cases, measure directly with span
                if (!needsShaping && textSpan.Length <= 32) // Small text threshold
                {
                    return MeasureTextWidthWithAdvanceSpan(paint, textSpan);
                }

                // For complex cases or cache lookup, we need string conversion
                // This is the controlled fallback to maintain cache compatibility
                string text = SpanToStringForCache(textSpan);

                // Check cache first
                if (GlyphMeasurementCache.TryGetValue(paintTypeface, needsShaping, text, out var cachedResult))
                {
                    return cachedResult.Width;
                }

                // For cache miss, we need to fall back to string-based measurement
                // This maintains exact compatibility with existing cache behavior
                return MeasureTextWidthWithAdvanceSpan(paint, textSpan);
            }

            /// <summary>
            /// Appends a ReadOnlySpan to StringBuilder without intermediate string allocation
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void AppendSpan(StringBuilder sb, ReadOnlySpan<char> span)
            {
                // StringBuilder.Append(ReadOnlySpan<char>) is available in .NET Core 2.1+
                sb.Append(span);
            }

            /// <summary>
            /// Appends a single character to StringBuilder
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void AppendChar(StringBuilder sb, char c)
            {
                sb.Append(c);
            }
        }


    }
}

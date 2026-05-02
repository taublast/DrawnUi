using System.Runtime.CompilerServices;

namespace DrawnUi.Draw
{
    /// <summary>
    /// SkiaLabel with per-word glyph measurement memoization.
    ///
    /// During word-wrap, base SkiaLabel calls MeasureLineGlyphs with accumulating strings
    /// ("word1", "word1 word2", "word1 word2 word3") — each is unique so GlyphMeasurementCache
    /// never hits. This subclass splits on spaces and measures each word independently;
    /// words repeat across lines and reflows yielding a much higher hit rate.
    ///
    /// Only the simple path (no shaping, no mono-width digits, no fill/span/character-spacing)
    /// is intercepted. All other paths fall through to base.
    /// </summary>
    public partial class SkiaLabelFast : SkiaLabel
    {
        public SkiaLabelFast() { }
        public SkiaLabelFast(string text) : base(text) { }

        private readonly record struct WordKey(
            string Family, int Weight, int Width, SKFontStyleSlant Slant, float TextSize, string Word);

        private Dictionary<WordKey, float>? _wordCache;

        private bool IsComplexMeasuring =>
            Spans.Count > 0 ||
            CharacterSpacing != 1f ||
            HorizontalTextAlignment == DrawTextAlignment.FillWordsFull ||
            HorizontalTextAlignment == DrawTextAlignment.FillCharactersFull ||
            HorizontalTextAlignment == DrawTextAlignment.FillWords ||
            HorizontalTextAlignment == DrawTextAlignment.FillCharacters;

        protected override (float Width, LineGlyph[] Glyphs) MeasureLineGlyphs(
            SKPaint paint, string text, bool needsShaping, float scale)
        {
            if (needsShaping || charMonoWidthPixels > 0 || IsComplexMeasuring)
                return base.MeasureLineGlyphs(paint, text, needsShaping, scale);

            if (string.IsNullOrEmpty(text))
                return (0f, null);

            _wordCache ??= new Dictionary<WordKey, float>();

            var typeface = paint.Typeface ?? SkiaFontManager.DefaultTypeface;
            var style = typeface.FontStyle;
            var family = typeface.FamilyName;
            var textSize = paint.TextSize;

            float total = 0f;
            int start = 0;

            while (start < text.Length)
            {
                int spaceIdx = text.IndexOf(' ', start);
                int end = spaceIdx < 0 ? text.Length : spaceIdx;

                if (end > start)
                    total += GetCachedWidth(paint, family, style.Weight, style.Width, style.Slant,
                        textSize, text.Substring(start, end - start));

                if (spaceIdx < 0) break;

                total += GetCachedWidth(paint, family, style.Weight, style.Width, style.Slant, textSize, " ");
                start = spaceIdx + 1;
            }

            if (paint.TextSkewX != 0)
                total += Math.Abs(paint.TextSkewX) * textSize;

            return (total, null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetCachedWidth(SKPaint paint, string family, int weight, int width,
            SKFontStyleSlant slant, float textSize, string word)
        {
            var key = new WordKey(family, weight, width, slant, textSize, word);
            if (!_wordCache!.TryGetValue(key, out var w))
            {
                w = MeasureTextWidthWithAdvance(paint, word);
                _wordCache[key] = w;
            }
            return w;
        }
    }
}

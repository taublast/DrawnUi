using System.Runtime.CompilerServices;

namespace DrawnUi.Draw;

/// <summary>
/// SkiaRichLabel with per-word glyph measurement memoization.
///
/// SkiaRichLabel always takes the complex measurement path (Spans.Count > 0 → requiresComplexMeasuring).
/// During word-wrap the accumulated strings ("word1", "word1 word2", ...) are all unique → cache misses.
/// This override splits on spaces and measures each word independently; words repeat → high hit rate.
/// Only intercepts when CharacterSpacing==1 and no fill alignment. All other paths fall through to base.
/// </summary>
public partial class SkiaRichLabelFast : SkiaLabelFast
{
    public SkiaRichLabelFast() { }
    public SkiaRichLabelFast(string text) : base(text) { }

    private readonly record struct RichWordKey(
        string Family, int Weight, int Width, SKFontStyleSlant Slant, float TextSize, string Word);

    private Dictionary<RichWordKey, (float Width, LineGlyph[] Glyphs)>? _richWordCache;

    protected override (float Width, LineGlyph[] Glyphs) MeasureLineGlyphs(
        SKPaint paint, string text, bool needsShaping, float scale)
    {
        // Only intercept: spans present, CS=1, no fill, no shaping, no mono
        if (needsShaping || charMonoWidthPixels > 0 || Spans.Count == 0 ||
            CharacterSpacing != 1f ||
            HorizontalTextAlignment == DrawTextAlignment.FillWordsFull ||
            HorizontalTextAlignment == DrawTextAlignment.FillCharactersFull ||
            HorizontalTextAlignment == DrawTextAlignment.FillWords ||
            HorizontalTextAlignment == DrawTextAlignment.FillCharacters)
            return base.MeasureLineGlyphs(paint, text, needsShaping, scale);

        if (string.IsNullOrEmpty(text))
            return (0f, null);

        _richWordCache ??= new Dictionary<RichWordKey, (float, LineGlyph[])>();

        var typeface = paint.Typeface ?? SkiaFontManager.DefaultTypeface;
        var style = typeface.FontStyle;
        var family = typeface.FamilyName;
        var textSize = paint.TextSize;

        var assembled = new List<LineGlyph>();
        float total = 0f;
        int start = 0;

        while (start < text.Length)
        {
            int spaceIdx = text.IndexOf(' ', start);
            int end = spaceIdx < 0 ? text.Length : spaceIdx;

            if (end > start)
            {
                var word = text.Substring(start, end - start);
                var (w, glyphs) = GetCachedRichWord(paint, family, style, textSize, word, scale);
                AppendWithOffset(assembled, glyphs, total);
                total += w;
            }

            if (spaceIdx < 0) break;

            var (sw, sglyphs) = GetCachedRichWord(paint, family, style, textSize, " ", scale);
            AppendWithOffset(assembled, sglyphs, total);
            total += sw;
            start = spaceIdx + 1;
        }

        if (paint.TextSkewX != 0)
            total += Math.Abs(paint.TextSkewX) * textSize;

        return (total, assembled.Count > 0 ? assembled.ToArray() : null);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendWithOffset(List<LineGlyph> dest, LineGlyph[]? src, float offset)
    {
        if (src == null) return;
        foreach (var g in src)
            dest.Add(LineGlyph.Move(g, g.Position + offset));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (float Width, LineGlyph[] Glyphs) GetCachedRichWord(
        SKPaint paint, string family, SKFontStyle style, float textSize, string word, float scale)
    {
        var key = new RichWordKey(family, style.Weight, style.Width, style.Slant, textSize, word);
        if (!_richWordCache!.TryGetValue(key, out var cached))
        {
            // base call goes through complex path (Spans.Count > 0) for single word
            // returns glyphs with positions starting at 0
            cached = base.MeasureLineGlyphs(paint, word, false, scale);
            _richWordCache[key] = cached;
        }
        return cached;
    }

    protected virtual void AddTextSpan(string text, Action<TextSpan> modifySpan = null)
    {
        if (TypeFace == null)
            ReplaceFont();

        if (TypeFace == null)
        {
            Super.Log("TYPEFACE NULL");
            return;
        }

        var originalTypeFace = TypeFace;
        var spanData = SkiaRichLabel.BuildSpanData(text, originalTypeFace);

        foreach (var (Text, Typeface, Symbol, Shape) in spanData)
        {
            var span = new TextSpan
            {
                Text = Text,
                TypeFace = Typeface,
                FontDetectedWith = Symbol,
                NeedShape = Shape
            };

            var attrs = this.FontAttributes;
            if (attrs.HasFlag(FontAttributes.Bold)) span.IsBold = true;
            if (attrs.HasFlag(FontAttributes.Italic)) span.IsItalic = true;

            modifySpan?.Invoke(span);
            Spans.Add(span);
        }
    }
}

namespace DrawnUi.Draw
{
    /// <summary>
    /// Experimental fast-path label.
    /// Inherits SkiaLabel and overrides measurement hot paths to apply
    /// Pretext-inspired techniques:
    ///   #1 per-word width memoization + cached spaceWidth
    ///   #2 prefix-sum table for char-wrap and tail truncation
    ///   #3 TextSize-aware measurement cache key
    ///   #4 lock-free / per-typeface partitioned cache
    ///   #5 SKFont.MeasureText over SKPaint.MeasureText
    /// Until overrides land, behavior is identical to SkiaLabel.
    /// Used for side-by-side benchmarking against SkiaLabel.
    /// </summary>
    public partial class SkiaLabelFast : SkiaLabel
    {
        public SkiaLabelFast()
        {
        }

        public SkiaLabelFast(string text) : base(text)
        {
        }

        // TODO #1: override MeasureLineGlyphs to use per-word width memoization.
        // protected override (float Width, LineGlyph[] Glyphs) MeasureLineGlyphs(
        //     SKPaint paint, string text, bool needsShaping, float scale)
        // {
        //     return base.MeasureLineGlyphs(paint, text, needsShaping, scale);
        // }

        // TODO #1: override DecomposeText to consume per-word widths
        // and avoid concat-then-measure pattern.
        // protected override DecomposedText DecomposeText(
        //     string text, SKPaint paint,
        //     SKPoint firstLineOffset,
        //     float maxWidth, float maxHeight, int maxLines,
        //     bool needsShaping, TextSpan span, float scale)
        // {
        //     return base.DecomposeText(text, paint, firstLineOffset,
        //         maxWidth, maxHeight, maxLines, needsShaping, span, scale);
        // }

        // TODO #2: override CutLineToFit with prefix-sum binary search,
        // replace O(n^2) substring + measure loop.
        // public override (int Limit, float Width) CutLineToFit(
        //     SKPaint paint, string textIn, float maxWidth)
        // {
        //     return base.CutLineToFit(paint, textIn, maxWidth);
        // }
    }
}

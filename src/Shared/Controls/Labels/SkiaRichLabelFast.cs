namespace DrawnUi.Draw;

/// <summary>
/// Experimental fast-path rich label.
/// Inherits <see cref="SkiaLabelFast"/> for measurement, reuses
/// <see cref="SkiaRichLabel.BuildSpanData"/> for typeface-fallback span building.
/// Used for side-by-side benchmarking against SkiaRichLabel.
/// Markdown / link-tap / code-block colouring features from SkiaRichLabel are
/// intentionally NOT mirrored here: bench scope is measurement performance only.
/// Add them later if Fast path replaces the slow path.
/// </summary>
public partial class SkiaRichLabelFast : SkiaLabelFast
{
    public SkiaRichLabelFast()
    {
    }

    public SkiaRichLabelFast(string text) : base(text)
    {
    }

    /// <summary>
    /// Mirrors <see cref="SkiaRichLabel.AddTextSpan"/> using the shared
    /// <see cref="SkiaRichLabel.BuildSpanData"/> helper. Applies font attributes
    /// from <see cref="SkiaLabel.FontAttributes"/> only — no markdown styling.
    /// </summary>
    protected virtual void AddTextSpan(string text, Action<TextSpan> modifySpan = null)
    {
        if (TypeFace == null) //might happen early in cycle when set via Styles
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
            if (attrs.HasFlag(FontAttributes.Bold))
                span.IsBold = true;
            if (attrs.HasFlag(FontAttributes.Italic))
                span.IsItalic = true;

            modifySpan?.Invoke(span);
            Spans.Add(span);
        }
    }

    // TODO: when SkiaLabelFast measurement overrides land, this control automatically
    // benefits from them via inheritance. Bench harness should compare:
    //   SkiaRichLabel  vs SkiaRichLabelFast  (same text, same constraints)
}

namespace DrawnUi.Draw;

public class TextLine
{
    /// <summary>
    /// Set during rendering
    /// </summary>
    public SKRect Bounds { get; set; }

    public string Value { get; set; }

    public bool IsNewParagraph { get; set; }
    public bool IsLastInParagraph { get; set; }

    public float Width { get; set; }
    public float Height { get; set; }

    //public LineGlyph[] Glyphs { get; set; }

    //public List<ApplySpan> ApplySpans { get; set; }

    public List<LineSpan> Spans { get; set; }

    public TextLine()
    {
        Spans = new();
    }

}
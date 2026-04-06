namespace DrawnUi.Draw;

public struct LineGlyph
{
    public static LineGlyph FromGlyph(UsedGlyph glyph, float position, float width)
    {
        return new LineGlyph()
        {
            Id = glyph.Id,
            Symbol = glyph.Symbol,
            IsAvailable = glyph.IsAvailable,

            Source = glyph.Source,
            StartIndex = glyph.StartIndex,
            Length = glyph.Length,

            Position = position,
            Width = width,
        };
    }

    public string Source { get; set; }

    /// <summary>
    /// Position inside existing span
    /// </summary>
    public int StartIndex { get; set; }

    /// <summary>
    /// Length inside existing span
    /// </summary>
    public int Length { get; set; }

    public bool IsNumber()
    {
        // This will only work correctly if Text is a single character.
        return Length == 1 && char.IsDigit(Source[StartIndex]);
    }

    public ReadOnlySpan<char> GetGlyphText()
    {
        return Source.AsSpan().Slice(StartIndex, Length);
    }

    public ushort Id { get; set; }

    public int Symbol { get; set; }

    public float Position { get; set; }

    /// <summary>
    /// Measured text with advance
    /// </summary>
    public float Width { get; set; }

    public static LineGlyph Move(LineGlyph existing, float position)
    {
        return existing with { Position = position };
    }

    //public string Text { get; set; }

    public bool IsAvailable { get; set; }

}

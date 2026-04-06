namespace DrawnUi.Draw;

public struct UsedGlyph
{
    public ushort Id { get; set; }

    public string Source { get; set; }

    public int Symbol { get; set; }

    public bool IsAvailable { get; set; }

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
}

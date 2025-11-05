namespace DrawnUi.Models;

public class GridSpan
{
    public int Start { get; }
    public int Length { get; }
    public bool IsColumn { get; }
    public double Requested { get; }

    public SpanKey Key { get; }

    public GridSpan(int start, int length, bool isColumn, double requestedLength)
    {
        Start = start;
        Length = length;
        IsColumn = isColumn;
        Requested = requestedLength;

        Key = new SpanKey(Start, Length, IsColumn);
    }
}
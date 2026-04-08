using System.Diagnostics;

namespace DrawnUi.Draw;

[DebuggerDisplay("{DebugString}")]
public struct ApplySpan
{

    public string DebugString
    {
        get
        {
            return $"Apply span {Span.DebugString} to {Start}->{End}";
        }
    }

    public static ApplySpan Create(TextSpan span, int start, int end)
    {
        return new ApplySpan
        {
            Span = span,
            Start = start,
            End = end
        };
    }

    public TextSpan Span { get; set; }
    public int Start { get; set; }
    public int End { get; set; }

    public static ApplySpan Empty
    {
        get
        {
            return new ApplySpan();
        }
    }
}
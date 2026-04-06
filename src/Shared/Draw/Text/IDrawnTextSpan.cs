namespace DrawnUi.Draw;

public interface IDrawnTextSpan
{
    public void Render(DrawingContext ctx);

    public ScaledSize Measure(float maxWidth, float maxHeight, float scale);

    public DrawImageAlignment VerticalAlignement { get; }
}

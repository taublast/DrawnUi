namespace DrawnUi.Draw;

public interface IPostRendererEffect : ISkiaEffect
{
    void Render(DrawingContext ctx);
}

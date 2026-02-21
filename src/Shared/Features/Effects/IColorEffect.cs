namespace DrawnUi.Draw;

public interface IColorEffect : ISkiaEffect
{
    SKColorFilter Filter { get; }

    SKColorFilter CreateFilter(SKRect destination);
}
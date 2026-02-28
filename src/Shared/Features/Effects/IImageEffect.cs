namespace DrawnUi.Draw;

public interface IImageEffect : ISkiaEffect
{
    SKImageFilter Filter { get; }

    SKImageFilter CreateFilter(SKRect destination);
}
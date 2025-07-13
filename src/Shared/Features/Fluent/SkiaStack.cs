namespace DrawnUi.Draw;

/// <summary>
/// Vertical stack, like MAUI VerticalStackLayout
/// </summary>
public partial class SkiaStack : SkiaLayout
{
    public SkiaStack()
    {
        Type = LayoutType.Column;
        HorizontalOptions = LayoutOptions.Fill;
    }
}

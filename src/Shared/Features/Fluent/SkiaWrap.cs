namespace DrawnUi.Draw
{
    /// <summary>
    /// A powerful flexible control, a bit like WPF StackPanel, arranges children in a responsive way according available size. Can change the number of Columns to use by default.
    /// </summary>
    public class SkiaWrap : SkiaLayout
    {
        public SkiaWrap()
        {
            Type = LayoutType.Wrap;
            HorizontalOptions = LayoutOptions.Fill;
        }
    }
}

namespace Microsoft.Maui.Controls
{
    public class SolidColorBrush : Brush
    {
        public SolidColorBrush()
        {
        }

        public SolidColorBrush(DrawnUi.Color color)
        {
            Color = color;
        }

        public DrawnUi.Color Color { get; set; }
    }
}
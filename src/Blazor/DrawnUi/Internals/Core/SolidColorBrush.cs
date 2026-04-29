namespace Microsoft.Maui.Controls
{
    public class SolidColorBrush : Brush
    {
        public SolidColorBrush()
        {
        }

        public SolidColorBrush(Microsoft.Maui.Graphics.Color color)
        {
            Color = color;
        }

        public Microsoft.Maui.Graphics.Color Color { get; set; }
    }
}
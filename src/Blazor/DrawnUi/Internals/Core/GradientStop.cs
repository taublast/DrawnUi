namespace Microsoft.Maui.Controls
{
    public class GradientStop
    {
        public GradientStop()
        {
        }

        public GradientStop(DrawnUi.Color color, float offset)
        {
            Color = color;
            Offset = offset;
        }

        public DrawnUi.Color Color { get; set; }

        public float Offset { get; set; }
    }
}
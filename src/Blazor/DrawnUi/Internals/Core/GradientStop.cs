namespace Microsoft.Maui.Controls
{
    public class GradientStop
    {
        public GradientStop()
        {
        }

        public GradientStop(Microsoft.Maui.Graphics.Color color, float offset)
        {
            Color = color;
            Offset = offset;
        }

        public Microsoft.Maui.Graphics.Color Color { get; set; }

        public float Offset { get; set; }
    }
}
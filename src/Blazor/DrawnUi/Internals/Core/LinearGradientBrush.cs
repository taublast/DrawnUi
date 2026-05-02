using System.Drawing;

namespace DrawnUi.Draw
{
    public class LinearGradientBrush : GradientBrush
    {
        public PointF StartPoint { get; set; }

        public PointF EndPoint { get; set; }
    }
}

namespace DrawnUi.Controls
{
    public class WheelCellInfo
    {
        public SkiaControl View { get; set; }

        public int Index { get; set; }

        public bool WasMeasured { get; set; }

        public SKRect Destination { get; set; } = new SKRect();

        public float Offset { get; set; }

        public SKMatrix Transform { get; set; } = SKMatrix.Identity;

        public bool IsSelected { get; set; }

        public float Opacity { get; set; } = 1;
    }
}
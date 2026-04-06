namespace DrawnUi.Draw;

public class ZoomEventArgs : EventArgs
{
    public ZoomEventArgs()
    { }

    public ZoomEventArgs(PointF center, double value)
    {
        Center = center;
        Value = value;
    }

    public PointF Center { get; set; }
    public double Value { get; set; }
}
namespace Microsoft.Maui.Graphics
{
    public struct PointF
    {
        public PointF(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; set; }

        public float Y { get; set; }
    }
}
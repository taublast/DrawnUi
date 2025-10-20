namespace DrawnUi.Camera
{
    public struct DrawableFrame
    {
        public SKCanvas Canvas { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public TimeSpan Time { get; set; }
    }
}

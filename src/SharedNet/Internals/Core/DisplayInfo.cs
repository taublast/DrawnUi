namespace DrawnUi.Draw
{
    public readonly struct DisplayInfo
    {
        public DisplayInfo(double density, DisplayRotation rotation = DisplayRotation.Rotation0)
        {
            Density = density;
            Rotation = rotation;
        }

        public double Density { get; }

        public DisplayRotation Rotation { get; }
    }
}
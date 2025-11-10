namespace DrawnUi.Camera
{
    public struct CameraExposureBaseline
    {
        public float ISO { get; set; }
        public float ShutterSpeed { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public CameraExposureBaseline(float iso, float shutterSpeed, string name, string description)
        {
            ISO = iso;
            ShutterSpeed = shutterSpeed;
            Name = name;
            Description = description;
        }
    }
}
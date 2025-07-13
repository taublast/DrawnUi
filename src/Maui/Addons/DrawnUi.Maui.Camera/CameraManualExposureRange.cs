namespace DrawnUi.Camera
{
    public struct CameraManualExposureRange
    {
        public float MinISO { get; set; }
        public float MaxISO { get; set; }
        public float MinShutterSpeed { get; set; }
        public float MaxShutterSpeed { get; set; }
        public bool IsManualExposureSupported { get; set; }
        public CameraExposureBaseline[] RecommendedBaselines { get; set; }

        public CameraManualExposureRange(float minISO, float maxISO, float minShutter, float maxShutter, bool isSupported, CameraExposureBaseline[] baselines)
        {
            MinISO = minISO;
            MaxISO = maxISO;
            MinShutterSpeed = minShutter;
            MaxShutterSpeed = maxShutter;
            IsManualExposureSupported = isSupported;
            RecommendedBaselines = baselines ?? new CameraExposureBaseline[0];
        }
    }
}
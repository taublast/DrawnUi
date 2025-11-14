namespace DrawnUi.Camera
{
    public class ExposureResult
    {
        public bool Success { get; set; }
        public double ExposureValue { get; set; } // EV value
        public double Brightness { get; set; } // Current scene brightness
        public string ErrorMessage { get; set; }
        public double SuggestedShutterSpeed { get; set; }
        public double SuggestedIso { get; set; }
        public double SuggestedAperture { get; set; }
    }
}

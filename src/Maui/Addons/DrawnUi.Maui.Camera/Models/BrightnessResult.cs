namespace DrawnUi.Camera
{
    /// <summary>
    /// Result from camera brightness measurement
    /// </summary>
    public class BrightnessResult
    {
        public bool Success { get; set; }
        public double Brightness { get; set; } // In lux
        public string ErrorMessage { get; set; }
    }
}

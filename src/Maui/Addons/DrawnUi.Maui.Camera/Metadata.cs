using System.Text;

namespace DrawnUi.Camera;

public partial class Metadata
{
    // Main properties
    public int? Orientation { get; set; }
    public int? ISO { get; set; }
    public double? FocalLength { get; set; }
    public double? Aperture { get; set; }
    public double? Shutter { get; set; }
    public string Software { get; set; }
    public string Vendor { get; set; }
    public string Model { get; set; }

    // GPS Information
    public double? GpsLatitude { get; set; }
    public double? GpsLongitude { get; set; }
    public double? GpsAltitude { get; set; }
    public string GpsLatitudeRef { get; set; }
    public string GpsLongitudeRef { get; set; }
    public string GpsAltitudeRef { get; set; }
    public DateTime? GpsTimestamp { get; set; }

    // Camera Settings
    public string Flash { get; set; }
    public string WhiteBalance { get; set; }
    public string ExposureMode { get; set; }
    public string MeteringMode { get; set; }
    public string SceneCaptureType { get; set; }
    public double? ExposureBias { get; set; }
    public double? BrightnessValue { get; set; }
    public double? DigitalZoomRatio { get; set; }
    public double? FocalLengthIn35mm { get; set; }

    // Image Properties
    public int? PixelWidth { get; set; }
    public int? PixelHeight { get; set; }
    public double? XResolution { get; set; }
    public double? YResolution { get; set; }
    public string ResolutionUnit { get; set; }
    public string ColorSpace { get; set; }

    // Lens Information
    public string LensMake { get; set; }
    public string LensModel { get; set; }
    public string LensSpecification { get; set; }
    public double? LensMinFocalLength { get; set; }
    public double? LensMaxFocalLength { get; set; }

    // Date/Time Information
    public DateTime? DateTimeOriginal { get; set; }
    public DateTime? DateTimeDigitized { get; set; }
    public string SubsecTime { get; set; }
    public string SubsecTimeOriginal { get; set; }
    public string SubsecTimeDigitized { get; set; }

    // Subject and Focus
    public string SubjectArea { get; set; }
    public string SubjectDistanceRange { get; set; }
    public string SensingMethod { get; set; }

    // Advanced Settings
    public string ExposureProgram { get; set; }
    public string SceneType { get; set; }
    public string CustomRendered { get; set; }
    public string GainControl { get; set; }
    public string Contrast { get; set; }
    public string Saturation { get; set; }
    public string Sharpness { get; set; }

    // Device Information
    public string BodySerialNumber { get; set; }
    public string CameraOwnerName { get; set; }
    public string ImageUniqueId { get; set; }
    public string SpectralSensitivity { get; set; }


    /// <summary>
    /// Converts a double value to rational format for EXIF data
    /// </summary>
    /// <param name="value">The double value to convert</param>
    /// <returns>Rational representation as string</returns>
    public static string ConvertToRational(double value)
    {
        if (value == 0) return "0/1";

        // For exposure time (shutter speed), handle fractional seconds
        if (value < 1)
        {
            int denominator = (int)(1 / value);
            return $"1/{denominator}";
        }

        // For other values, multiply by 100 to preserve precision
        int numerator = (int)(value * 100);
        return $"{numerator}/100";
    }

    public static void ApplyGpsCoordinates(Metadata meta, double latitude, double longitude)
    {
        if (latitude != 0 && longitude != 0)
        {
            // Set Metadata GPS properties
            meta.GpsLatitude = latitude;
            meta.GpsLongitude = longitude;
            meta.GpsLatitudeRef = latitude > 0 ? "N" : "S";
            meta.GpsLongitudeRef = longitude > 0 ? "E" : "W";
        }
    }

    public static string ConvertCoords(double coord)
    {
        coord = Math.Abs(coord);
        int degree = (int)coord;
        coord *= 60;
        coord -= (degree * 60.0d);
        int minute = (int)coord;
        coord *= 60;
        coord -= (minute * 60.0d);
        int second = (int)(coord * 1000.0d);

        StringBuilder sb = new StringBuilder();
        sb.Append(degree);
        sb.Append("/1,");
        sb.Append(minute);
        sb.Append("/1,");
        sb.Append(second);
        sb.Append("/1000");
        return sb.ToString();
    }

    public static void ApplyRotation(Metadata meta, int rotation)
    {
        switch (rotation)
        {
            case 90:
                meta.Orientation = 6;
                break;
            case 270:
                meta.Orientation = 8;
                break;
            case 180:
                meta.Orientation = 3;
                break;
            default:
                meta.Orientation = 1;
                break;
        }
    }
}

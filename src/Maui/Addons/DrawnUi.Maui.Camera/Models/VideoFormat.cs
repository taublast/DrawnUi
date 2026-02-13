namespace DrawnUi.Camera;

/// <summary>
/// Represents an available video recording format/resolution
/// </summary>
public class VideoFormat
{
    public int Index { get; set; }

    /// <summary>
    /// Width in pixels
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Height in pixels
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Frame rate in frames per second
    /// </summary>
    public int FrameRate { get; set; }

    /// <summary>
    /// Video codec (e.g., "H.264", "H.265")
    /// </summary>
    public string Codec { get; set; } = "H.264";

    /// <summary>
    /// Target bitrate in bits per second
    /// </summary>
    public int BitRate { get; set; }

    /// <summary>
    /// Total pixel count
    /// </summary>
    public int TotalPixels => Width * Height;

    /// <summary>
    /// Aspect ratio (width/height)
    /// </summary>
    public double AspectRatio => (double)Width / Height;

    /// <summary>
    /// Aspect ratio in standard notation (e.g., "16:9", "4:3")
    /// </summary>
    public string AspectRatioString
    {
        get
        {
            // Calculate GCD to simplify the ratio
            int gcd = CalculateGCD(Width, Height);
            int simplifiedWidth = Width / gcd;
            int simplifiedHeight = Height / gcd;

            return $"{simplifiedWidth}:{simplifiedHeight}";
        }
    }

    /// <summary>
    /// Platform-specific format identifier (optional)
    /// </summary>
    public string FormatId { get; set; }

    /// <summary>
    /// Human-readable description
    /// </summary>
    public string Description => $"{Width}x{Height}@{FrameRate}fps";

    public string DescriptionFull => $"{Width}x{Height}@{FrameRate}fps ({Codec}, {BitRate / 1000000.0:F1}Mbps)";

    public override string ToString() => Description;

    public override bool Equals(object obj)
    {
        if (obj is VideoFormat other)
        {
            return Width == other.Width && Height == other.Height && FrameRate == other.FrameRate;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Width, Height, FrameRate);
    }

    /// <summary>
    /// Calculate Greatest Common Divisor using Euclidean algorithm
    /// </summary>
    private static int CalculateGCD(int a, int b)
    {
        while (b != 0)
        {
            int temp = b;
            b = a % b;
            a = temp;
        }
        return a;
    }
}

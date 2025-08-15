namespace DrawnUi.Camera;

/// <summary>
/// Represents an available capture format/resolution
/// </summary>
public class CaptureFormat
{
    /// <summary>
    /// Width in pixels
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Height in pixels
    /// </summary>
    public int Height { get; set; }

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
    public string Description => $"{Width}x{Height} ({TotalPixels:N0} pixels, {AspectRatioString})";

    public override string ToString() => Description;

    public override bool Equals(object obj)
    {
        if (obj is CaptureFormat other)
        {
            return Width == other.Width && Height == other.Height;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Width, Height);
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

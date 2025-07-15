using System.Numerics;

namespace DrawnUi.Draw;

public struct ScrollToIndexOrder
{
    public static ScrollToIndexOrder Default => new()
    {
        Index = -1
    };
    public bool IsSet
    {
        get
        {
            return Index >= 0;
        }
    }
    public bool Animated { get; set; }
    public float MaxTimeSecs { get; set; }
    public RelativePositionType RelativePosition { get; set; }
    public int Index { get; set; }
    public bool Clamp { get; set; }
}

public class VelocityAccumulator
{
    private List<(Vector2 velocity, DateTime time)> velocities = new List<(Vector2 velocity, DateTime time)>();
    private const double Threshold = 10.0; // Minimum significant movement
    private const int MaxSampleSize = 5; // Number of samples for weighted average
    private const int ConsiderationTimeframeMs = 150; // Timeframe in ms for velocity consideration

    public void Clear()
    {
        velocities.Clear();
    }

    public void CaptureVelocity(Vector2 velocity)
    {
        var now = DateTime.UtcNow;
        if (velocities.Count == MaxSampleSize) velocities.RemoveAt(0);
        velocities.Add((velocity, now));
    }

    public Vector2 CalculateFinalVelocity(float clampAbsolute = 0)
    {
        var now = DateTime.UtcNow;
        var relevantVelocities = velocities.Where(v => (now - v.time).TotalMilliseconds <= ConsiderationTimeframeMs).ToList();
        if (!relevantVelocities.Any()) return Vector2.Zero;

        // Calculate weighted average for both X and Y components
        float weightedSumX = relevantVelocities.Select((v, i) => v.velocity.X * (i + 1)).Sum();
        float weightedSumY = relevantVelocities.Select((v, i) => v.velocity.Y * (i + 1)).Sum();
        var weightSum = Enumerable.Range(1, relevantVelocities.Count).Sum();

        if (clampAbsolute != 0)
        {
            return new Vector2(Math.Clamp(weightedSumX / weightSum, -clampAbsolute, clampAbsolute),
                Math.Clamp(weightedSumY / weightSum, -clampAbsolute, clampAbsolute));
        }

        return new Vector2(weightedSumX / weightSum, weightedSumY / weightSum);
    }
}

public struct ScrollToPointOrder
{
    public bool IsValid
    {
        get
        {
            return !float.IsNaN(Location.X) && !float.IsNaN(Location.Y); ;
        }
    }

    public static ScrollToPointOrder NotValid => new()
    {
        Location = new SKPoint(float.NaN, float.NaN)
    };


    public static ScrollToPointOrder ToPoint(SKPoint point, bool animated)
    {
        return new()
        {
            Location = point,
            Animated = animated
        };
    }

    public static ScrollToPointOrder ToCoords(float x, float y, bool animated)
    {
        return new()
        {
            Location = new SKPoint(x, y),
            Animated = animated
        };
    }

    public static ScrollToPointOrder ToCoords(float x, float y, float maxTimeSecs)
    {
        return new()
        {
            Location = new SKPoint(x, y),
            MaxTimeSecs = maxTimeSecs
        };
    }

    public bool Animated { get; set; }
    public SKPoint Location { get; set; }
    public float MaxTimeSecs { get; set; }

}

using System.Numerics;

namespace DrawnUi.Infrastructure.Helpers;

public class VelocityTracker
{

    private List<(float VelocityX, float VelocityY)> velocityDataPoints;

    public VelocityTracker()
    {
        velocityDataPoints = new List<(float, float)>();
    }


    public void AddMovement(float velocityX, float velocityY)
    {
        velocityDataPoints.Add((velocityX, velocityY));

        // Limit the number of stored velocity data points
        if (velocityDataPoints.Count > MaxVelocityDataPoints)
        {
            velocityDataPoints.RemoveAt(0);
        }
    }

    public void Clear()
    {
        velocityDataPoints.Clear();
    }

    private const int MaxVelocityDataPoints = 10;


    //  Debug.WriteLine($"[V-OUT] {currentVelocityY:0.00}");

    public Vector2? ComputeCurrentVelocity(float VelocityWeighting = 0.1f)
    {
        if (velocityDataPoints.Count < 2)
        {
            return null; // Not enough data points to calculate velocity
        }

        // Get the last recorded data point
        var lastDataPoint = velocityDataPoints[velocityDataPoints.Count - 1];
        float lastVelocityX = lastDataPoint.VelocityX;
        float lastVelocityY = lastDataPoint.VelocityY;

        // Calculate the current velocity using the average velocity
        float averageVelocityX = 0f;
        float averageVelocityY = 0f;
        float totalWeight = 0f;

        for (int i = 0; i < velocityDataPoints.Count - 1; i++)
        {
            var dataPoint = velocityDataPoints[i];
            float velocityX = dataPoint.VelocityX;
            float velocityY = dataPoint.VelocityY;
            float weight = MathF.Pow(VelocityWeighting, velocityDataPoints.Count - 2 - i);
            averageVelocityX += velocityX * weight;
            averageVelocityY += velocityY * weight;
            totalWeight += weight;
        }

        averageVelocityX /= totalWeight;
        averageVelocityY /= totalWeight;

        // Compare the magnitudes of the last velocity and the calculated average velocity
        float magnitudeLastVelocity = MathF.Sqrt(lastVelocityX * lastVelocityX + lastVelocityY * lastVelocityY);
        float magnitudeAverageVelocity = MathF.Sqrt(averageVelocityX * averageVelocityX + averageVelocityY * averageVelocityY);

        // Choose the current velocity based on the magnitude comparison
        float currentVelocityX, currentVelocityY;
        if (magnitudeLastVelocity < 0.5f * magnitudeAverageVelocity) // Adjust the threshold as needed
        {
            currentVelocityX = lastVelocityX;
            currentVelocityY = lastVelocityY;
        }
        else
        {
            currentVelocityX = averageVelocityX;
            currentVelocityY = averageVelocityY;
        }

        //Debug.WriteLine($"[V-OUT] {currentVelocityY:0.00}");

        return new(currentVelocityX, currentVelocityY);
    }



}

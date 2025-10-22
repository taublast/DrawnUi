using System.Diagnostics;

namespace DrawnUi.Extensions;

public static class FloatingPointExtensions
{
    public static double Project(this double initialVelocity, double decelerationRate)
    {
        if (decelerationRate >= 1)
        {
            Debug.Assert(false);
            return initialVelocity;
        }

        return initialVelocity * decelerationRate / (1 - decelerationRate);
    }

    public static bool IsLess(this double number, double other, double eps)
    {
        return number < other - eps;
    }

    public static bool IsGreater(this double number, double other, double eps)
    {
        return number > other + eps;
    }

    public static bool IsEqual(this double number, double other, double eps)
    {
        return Math.Abs(number - other) < eps;
    }
}


public static class PointExtensions
{
    public static double Length(this Point p)
    {
        return Math.Sqrt(p.X * p.X + p.Y * p.Y);
    }

    public static Point Clamped(this Point p, Rect rect)
    {
        return new Point(p.X.Clamp(rect.Left, rect.Right), p.Y.Clamp(rect.Top, rect.Bottom));
    }



    /// <summary>
    /// Adds the coordinates of one Point to another.
    /// </summary>
    /// <param name="first"></param>
    /// <param name="second"></param>
    /// <returns></returns>
    public static Point Add(this Point first, Point second)
    {
        return new Point(first.X + second.X, first.Y + second.Y);
    }

    /// <summary>
    /// Gets the center of some touch points.
    /// </summary>
    /// <param name="touches"></param>
    /// <returns></returns>
    public static Point Center(this Point[] touches)
    {
        int num = touches != null ? touches.Length : 0;
        double x = 0;
        double y = 0;
        for (int i = 0; i < num; i++)
        {
            x += touches[i].X;
            y += touches[i].Y;
        }
        return new Point(x / num, y / num);
    }

    /// <summary>
    /// Subtracts the coordinates of one Point from another.
    /// </summary>
    /// <param name="first"></param>
    /// <param name="second"></param>
    /// <returns></returns>
    public static Point Subtract(this Point first, Point second)
    {
        return new Point(first.X - second.X, first.Y - second.Y);
    }
}
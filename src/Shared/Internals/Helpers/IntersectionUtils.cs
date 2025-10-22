using System.Numerics;

namespace DrawnUi.Infrastructure.Helpers;

public static class IntersectionUtils
{

    public static SKRect IntersectWith(this SKRect r1, SKRect r2)
    {
        var x = Math.Max(r1.Left, r2.Left);
        var y = Math.Max(r1.Top, r2.Top);
        var width = Math.Min(r1.Right, r2.Right) - x;
        var height = Math.Min(r1.Bottom, r2.Bottom) - y;

        if (width < 0 || height < 0)
        {
            return SKRect.Empty;
        }
        return new SKRect(x, y, width, height);
    }

    public static Vector2 Clamped(this Vector2 point, SKRect rect)
    {
        var x = Math.Max(rect.Left, Math.Min(rect.Right, point.X));
        var y = Math.Max(rect.Top, Math.Min(rect.Bottom, point.Y));
        return new(x, y);
    }

    public static Vector2? GetIntersection((Vector2, Vector2) segment1, (Vector2, Vector2) segment2)
    {
        Vector2 p1 = segment1.Item1;
        Vector2 p2 = segment1.Item2;
        Vector2 p3 = segment2.Item1;
        Vector2 p4 = segment2.Item2;
        float d = (p2.X - p1.X) * (p4.Y - p3.Y) - (p2.Y - p1.Y) * (p4.X - p3.X);

        if (d == 0)
        {
            return null; // parallel lines
        }

        float u = ((p3.X - p1.X) * (p4.Y - p3.Y) - (p3.Y - p1.Y) * (p4.X - p3.X)) / d;
        float v = ((p3.X - p1.X) * (p2.Y - p1.Y) - (p3.Y - p1.Y) * (p2.X - p1.X)) / d;

        if (u < 0.0f || u > 1.0f)
        {
            return null; // intersection point is not between p1 and p2
        }

        if (v < 0.0f || v > 1.0f)
        {
            return null; // intersection point is not between p3 and p4
        }

        return new Vector2(p1.X + u * (p2.X - p1.X), p1.Y + u * (p2.Y - p1.Y));
    }

    public static Vector2? GetIntersection(SKRect rect, (Vector2, Vector2) segment)
    {
        Vector2 rMinMin = new Vector2(rect.Left, rect.Top);
        Vector2 rMinMax = new Vector2(rect.Left, rect.Bottom);
        Vector2 rMaxMin = new Vector2(rect.Right, rect.Top);
        Vector2 rMaxMax = new Vector2(rect.Right, rect.Bottom);

        var point = GetIntersection((rMinMin, rMinMax), segment);
        if (point.HasValue)
        {
            return point.Value;
        }

        point = GetIntersection((rMinMin, rMaxMin), segment);
        if (point.HasValue)
        {
            return point.Value;
        }

        point = GetIntersection((rMinMax, rMaxMax), segment);
        if (point.HasValue)
        {
            return point.Value;
        }

        point = GetIntersection((rMaxMin, rMaxMax), segment);
        if (point.HasValue)
        {
            return point.Value;
        }

        return null;
    }
}
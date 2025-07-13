using Microsoft.Maui.Controls.Shapes;

namespace DrawnUi.Extensions;

public static partial class InternalExtensions
{
    #region MAUI CONTEXT

    public static IMauiContext? FindMauiContext(this Element element, bool fallbackToAppMauiContext = false)
    {
        if (element is IElement fe && fe.Handler?.MauiContext != null)
            return fe.Handler.MauiContext;

        foreach (var parent in element.GetParentsPath())
        {
            if (parent is IElement parentView && parentView.Handler?.MauiContext != null)
                return parentView.Handler.MauiContext;
        }

        return fallbackToAppMauiContext ? Application.Current?.FindMauiContext() : default;
    }

    public static IEnumerable<Element> GetParentsPath(this Element self)
    {
        Element current = self;

        while (!IsAppOrNull(current.RealParent))
        {
            current = current.RealParent;
            yield return current;
        }
    }

    internal static bool IsAppOrNull(object? element) =>
        element == null || element is IApplication;

    #endregion

    public static SkiaShadow FromPlatform(this object platform)
    {
        if (platform is Shadow shadow)
        {
            return new SkiaShadow
            {
                Blur = shadow.Radius,
                Opacity = shadow.Opacity,
                X = shadow.Offset.X,
                Y = shadow.Offset.Y,
                Color = ((SolidColorBrush)shadow.Brush).Color,
                BindingContext = shadow.BindingContext
            };
        }

        return null;
    }

    public static void FromPlatform(this Geometry geometry, SKPath path, SKRect destination, float scale)
    {
        FromPlatform(geometry, path, scale);
        path.Offset(destination.Location);
    }

    public static SKPath FromPlatform(this Geometry geometry, SKPath path, float scale)
    {
        if (geometry == null)
            throw new ArgumentNullException(nameof(geometry));

        path ??= new SKPath();

        if (geometry is EllipseGeometry ellipseGeometry)
        {
            return ConvertEllipseGeometry(ellipseGeometry, path, scale);
        }
        else if (geometry is LineGeometry lineGeometry)
        {
            return ConvertLineGeometry(lineGeometry, path, scale);
        }
        else if (geometry is RectangleGeometry rectangleGeometry)
        {
            return ConvertRectangleGeometry(rectangleGeometry, path, scale);
        }
        else if (geometry is PathGeometry pathGeometry)
        {
            return ConvertPathGeometry(pathGeometry, path, scale);
        }

        throw new NotSupportedException($"Unsupported geometry type: {geometry.GetType()}");
    }


    public static SKRect ToSKRect(this Rect rect, float scale) =>
        new SKRect(
            (float)rect.X * scale,
            (float)rect.Y * scale,
            (float)(rect.X + rect.Width) * scale,
            (float)(rect.Y + rect.Height) * scale);

    private static SKPath ConvertEllipseGeometry(EllipseGeometry ellipseGeometry, SKPath path, float scale)
    {
        var rect = new SKRect(0, 0, (float)(ellipseGeometry.RadiusX * 2 * scale),
            (float)(ellipseGeometry.RadiusY * 2 * scale));
        path.AddOval(rect);
        return path;
    }

    private static SKPoint ToSKPoint(this Point point, float scale)
    {
        return new SKPoint((float)(point.X * scale), (float)(point.Y * scale));
    }

    private static SKPath ConvertLineGeometry(LineGeometry lineGeometry, SKPath path, float scale)
    {
        path.MoveTo(lineGeometry.StartPoint.ToSKPoint(scale));
        path.LineTo(lineGeometry.EndPoint.ToSKPoint(scale));
        return path;
    }

    private static SKPath ConvertRectangleGeometry(RectangleGeometry rectangleGeometry, SKPath path, float scale)
    {
        var rect = rectangleGeometry.Rect.ToSKRect(scale);
        path.AddRect(rect);
        return path;
    }

    private static SKPath ConvertPathGeometry(PathGeometry pathGeometry, SKPath path, float scale)
    {
        foreach (var figure in pathGeometry.Figures)
        {
            if (figure.StartPoint != null)
                path.MoveTo(figure.StartPoint.ToSKPoint(scale));

            foreach (var segment in figure.Segments)
            {
                switch (segment)
                {
                    case LineSegment lineSegment:
                        path.LineTo(lineSegment.Point.ToSKPoint(scale));
                        break;

                    case BezierSegment bezierSegment:
                        path.CubicTo(
                            bezierSegment.Point1.ToSKPoint(scale),
                            bezierSegment.Point2.ToSKPoint(scale),
                            bezierSegment.Point3.ToSKPoint(scale));
                        break;

                    case PolyLineSegment polyLineSegment:
                        foreach (var point in polyLineSegment.Points)
                        {
                            path.LineTo(point.ToSKPoint(scale));
                        }

                        break;

                    case PolyBezierSegment polyBezierSegment:
                        for (int i = 0; i < polyBezierSegment.Points.Count; i += 3)
                        {
                            path.CubicTo(
                                polyBezierSegment.Points[i].ToSKPoint(scale),
                                polyBezierSegment.Points[i + 1].ToSKPoint(scale),
                                polyBezierSegment.Points[i + 2].ToSKPoint(scale));
                        }

                        break;

                    default:
                        throw new NotSupportedException($"Unsupported segment type: {segment.GetType()}");
                }
            }

            if (figure.IsClosed)
            {
                path.Close();
            }
        }

        return path;
    }
}

namespace DotNetCad.Core.Geometry;

public static class CadTransformMath
{
    public static Point2D RotatePoint(Point2D pt, Point2D origin, double angleDegrees)
    {
        var rad = angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);

        var dx = pt.X - origin.X;
        var dy = pt.Y - origin.Y;

        var rx = (dx * cos) - (dy * sin);
        var ry = (dx * sin) + (dy * cos);

        return new Point2D(origin.X + rx, origin.Y + ry);
    }

    public static Point2D MirrorPoint(Point2D pt, Point2D axisP1, Point2D axisP2)
    {
        var dx = axisP2.X - axisP1.X;
        var dy = axisP2.Y - axisP1.Y;
        var lenSq = (dx * dx) + (dy * dy);

        if (lenSq < 1e-9)
        {
            return new Point2D((2.0 * axisP1.X) - pt.X, (2.0 * axisP1.Y) - pt.Y);
        }

        var t = (((pt.X - axisP1.X) * dx) + ((pt.Y - axisP1.Y) * dy)) / lenSq;
        var projX = axisP1.X + (t * dx);
        var projY = axisP1.Y + (t * dy);

        var rx = (2.0 * projX) - pt.X;
        var ry = (2.0 * projY) - pt.Y;

        return new Point2D(rx, ry);
    }

    public static List<Point2D> RotatePolygon(IReadOnlyList<Point2D> vertices, Point2D origin, double angleDegrees)
    {
        return vertices.Select(v => RotatePoint(v, origin, angleDegrees)).ToList();
    }

    public static List<Point2D> MirrorPolygon(IReadOnlyList<Point2D> vertices, Point2D axisP1, Point2D axisP2)
    {
        var mirrored = vertices.Select(v => MirrorPoint(v, axisP1, axisP2)).ToList();
        mirrored.Reverse();
        return mirrored;
    }

    public static Point2D TranslatePoint(Point2D pt, double dx, double dy)
    {
        return new Point2D(pt.X + dx, pt.Y + dy);
    }

    public static List<Point2D> TranslatePolygon(IReadOnlyList<Point2D> vertices, double dx, double dy)
    {
        return vertices.Select(v => TranslatePoint(v, dx, dy)).ToList();
    }

    public static Point2D ScalePoint(Point2D pt, Point2D origin, double scaleX, double scaleY)
    {
        var dx = (pt.X - origin.X) * scaleX;
        var dy = (pt.Y - origin.Y) * scaleY;
        return new Point2D(origin.X + dx, origin.Y + dy);
    }
}

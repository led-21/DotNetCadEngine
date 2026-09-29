namespace DotNetCad.Core.Geometry;

public static class PolygonMath
{
    public static double CalculatePolygonArea(IReadOnlyList<Point2D> vertices)
    {
        if (vertices == null || vertices.Count < 3) return 0.0;

        double area = 0.0;
        int n = vertices.Count;
        for (int i = 0; i < n; i++)
        {
            var p1 = vertices[i];
            var p2 = vertices[(i + 1) % n];
            area += (p1.X * p2.Y) - (p2.X * p1.Y);
        }

        return Math.Abs(area) / 2.0;
    }

    public static double CalculatePerimeter(IReadOnlyList<Point2D> vertices)
    {
        if (vertices == null || vertices.Count < 2) return 0.0;
        double perimeter = 0.0;
        for (int i = 0; i < vertices.Count; i++)
        {
            var p1 = vertices[i];
            var p2 = vertices[(i + 1) % vertices.Count];
            perimeter += p1.DistanceTo(p2);
        }
        return perimeter;
    }

    public static Point2D CalculateCentroid(IReadOnlyList<Point2D> vertices)
    {
        if (vertices == null || vertices.Count == 0) return Point2D.Zero;
        if (vertices.Count == 1) return vertices[0];

        double cx = 0;
        double cy = 0;
        foreach (var v in vertices)
        {
            cx += v.X;
            cy += v.Y;
        }

        return new Point2D(cx / vertices.Count, cy / vertices.Count);
    }

    public static bool IsPointInsidePolygon(Point2D point, IReadOnlyList<Point2D> vertices)
    {
        if (vertices == null || vertices.Count < 3) return false;

        bool inside = false;
        int j = vertices.Count - 1;
        for (int i = 0; i < vertices.Count; j = i++)
        {
            if (((vertices[i].Y > point.Y) != (vertices[j].Y > point.Y)) &&
                (point.X < (vertices[j].X - vertices[i].X) * (point.Y - vertices[i].Y) / (vertices[j].Y - vertices[i].Y) + vertices[i].X))
            {
                inside = !inside;
            }
        }

        return inside;
    }

    public static List<Point2D> CreateRectangle(Point2D origin, double width, double height, double angleDegrees = 0.0)
    {
        var rad = angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);

        Point2D RotateAndOffset(double lx, double ly)
        {
            var rx = (lx * cos) - (ly * sin);
            var ry = (lx * sin) + (ly * cos);
            return new Point2D(origin.X + rx, origin.Y + ry);
        }

        return
        [
            RotateAndOffset(0, 0),
            RotateAndOffset(width, 0),
            RotateAndOffset(width, height),
            RotateAndOffset(0, height)
        ];
    }
}

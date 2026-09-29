using DotNetCad.Core.Measurements;

namespace DotNetCad.Core.Geometry;

public static class CadMeasurementMath
{
    public static CadDistanceMeasurement MeasureDistance(Point2D p1, Point2D p2)
    {
        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));

        var angleRad = Math.Atan2(dy, dx);
        var angleDeg = angleRad * 180.0 / Math.PI;
        if (angleDeg < 0) angleDeg += 360.0;

        var azRad = Math.Atan2(dx, dy);
        var azDeg = azRad * 180.0 / Math.PI;
        if (azDeg < 0) azDeg += 360.0;

        return new CadDistanceMeasurement(length, dx, dy, azDeg, angleDeg);
    }

    public static double MeasureAngle(Point2D vertex, Point2D p1, Point2D p2)
    {
        var v1x = p1.X - vertex.X;
        var v1y = p1.Y - vertex.Y;
        var v2x = p2.X - vertex.X;
        var v2y = p2.Y - vertex.Y;

        var len1 = Math.Sqrt((v1x * v1x) + (v1y * v1y));
        var len2 = Math.Sqrt((v2x * v2x) + (v2y * v2y));

        if (len1 < 1e-6 || len2 < 1e-6) return 0.0;

        var dot = (v1x * v2x) + (v1y * v2y);
        var cosTheta = Math.Clamp(dot / (len1 * len2), -1.0, 1.0);
        return Math.Acos(cosTheta) * 180.0 / Math.PI;
    }

    public static CadPolygonMeasurement MeasurePolygon(IReadOnlyList<Point2D> vertices)
    {
        if (vertices == null || vertices.Count < 3)
        {
            return new CadPolygonMeasurement(0.0, 0.0, Point2D.Zero);
        }

        var area = PolygonMath.CalculatePolygonArea(vertices);
        var perimeter = 0.0;
        int n = vertices.Count;

        for (int i = 0; i < n; i++)
        {
            var p1 = vertices[i];
            var p2 = vertices[(i + 1) % n];
            perimeter += p1.DistanceTo(p2);
        }

        var centroid = PolygonMath.CalculateCentroid(vertices);
        return new CadPolygonMeasurement(area, perimeter, centroid);
    }
}

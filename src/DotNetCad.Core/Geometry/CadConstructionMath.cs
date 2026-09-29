namespace DotNetCad.Core.Geometry;

public static class CadConstructionMath
{
    public static (Point2D Start, Point2D End) OffsetSegment(Point2D p1, Point2D p2, double distance, bool toLeft = true)
    {
        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        var len = Math.Sqrt((dx * dx) + (dy * dy));

        if (len < 1e-6) return (p1, p2);

        double nx, ny;
        if (toLeft)
        {
            nx = -dy / len;
            ny = dx / len;
        }
        else
        {
            nx = dy / len;
            ny = -dx / len;
        }

        var op1 = new Point2D(p1.X + (nx * distance), p1.Y + (ny * distance));
        var op2 = new Point2D(p2.X + (nx * distance), p2.Y + (ny * distance));

        return (op1, op2);
    }

    public static List<Point2D> OffsetPolygon(IReadOnlyList<Point2D> vertices, double distance)
    {
        if (vertices == null || vertices.Count < 3) return vertices?.ToList() ?? [];

        int n = vertices.Count;
        double signedArea = 0.0;
        for (int i = 0; i < n; i++)
        {
            var p1 = vertices[i];
            var p2 = vertices[(i + 1) % n];
            signedArea += (p1.X * p2.Y) - (p2.X * p1.Y);
        }

        bool toLeft = signedArea < 0;
        var distAbs = Math.Abs(distance);
        if (distance < 0)
        {
            toLeft = !toLeft;
        }

        var offsetLines = new List<(Point2D P1, Point2D P2)>();

        for (int i = 0; i < n; i++)
        {
            var p1 = vertices[i];
            var p2 = vertices[(i + 1) % n];
            offsetLines.Add(OffsetSegment(p1, p2, distAbs, toLeft: toLeft));
        }

        var result = new List<Point2D>();
        for (int i = 0; i < n; i++)
        {
            var line1 = offsetLines[(i - 1 + n) % n];
            var line2 = offsetLines[i];

            var inter = FindLineIntersection(line1.P1, line1.P2, line2.P1, line2.P2);
            result.Add(inter ?? line2.P1);
        }

        return result;
    }

    public static (Point2D Start, Point2D End)? ExtendSegment(Point2D p1, Point2D p2, Point2D boundA, Point2D boundB)
    {
        var inter = FindLineIntersection(p1, p2, boundA, boundB);
        if (inter == null) return null;

        if (!IsPointOnSegment(inter.Value, boundA, boundB, tolerance: 0.05)) return null;

        var vDirX = p2.X - p1.X;
        var vDirY = p2.Y - p1.Y;
        var vExtX = inter.Value.X - p2.X;
        var vExtY = inter.Value.Y - p2.Y;

        var dot = (vDirX * vExtX) + (vDirY * vExtY);
        if (dot < 0) return null;

        return (p1, inter.Value);
    }

    public static (Point2D Start, Point2D End)? TrimSegment(Point2D p1, Point2D p2, Point2D cutA, Point2D cutB, Point2D clickPoint)
    {
        var inter = FindSegmentIntersection(p1, p2, cutA, cutB);
        if (inter == null) return null;

        var distToP1 = clickPoint.DistanceTo(p1);
        var distToP2 = clickPoint.DistanceTo(p2);

        return distToP1 < distToP2 ? (inter.Value, p2) : (p1, inter.Value);
    }

    public static (Point2D Center, Point2D TangentA, Point2D TangentB)? CreateFillet(Point2D a1, Point2D a2, Point2D b1, Point2D b2, double radius)
    {
        var inter = FindLineIntersection(a1, a2, b1, b2);
        if (inter == null || radius <= 0.001) return null;

        var v1 = new Point2D(a1.X - inter.Value.X, a1.Y - inter.Value.Y);
        var v2 = new Point2D(b2.X - inter.Value.X, b2.Y - inter.Value.Y);

        var len1 = Math.Sqrt((v1.X * v1.X) + (v1.Y * v1.Y));
        var len2 = Math.Sqrt((v2.X * v2.X) + (v2.Y * v2.Y));
        if (len1 < 1e-6 || len2 < 1e-6) return null;

        var u1 = new Point2D(v1.X / len1, v1.Y / len1);
        var u2 = new Point2D(v2.X / len2, v2.Y / len2);

        var dot = Math.Clamp((u1.X * u2.X) + (u1.Y * u2.Y), -1.0, 1.0);
        var angle = Math.Acos(dot);
        if (angle < 0.01 || Math.Abs(angle - Math.PI) < 0.01) return null;

        var tangentDist = radius / Math.Tan(angle / 2.0);
        var tanA = new Point2D(inter.Value.X + (u1.X * tangentDist), inter.Value.Y + (u1.Y * tangentDist));
        var tanB = new Point2D(inter.Value.X + (u2.X * tangentDist), inter.Value.Y + (u2.Y * tangentDist));

        var bisect = new Point2D(u1.X + u2.X, u1.Y + u2.Y);
        var blen = Math.Sqrt((bisect.X * bisect.X) + (bisect.Y * bisect.Y));
        if (blen < 1e-6) return null;

        var uBisect = new Point2D(bisect.X / blen, bisect.Y / blen);
        var centerDist = radius / Math.Sin(angle / 2.0);
        var center = new Point2D(inter.Value.X + (uBisect.X * centerDist), inter.Value.Y + (uBisect.Y * centerDist));

        return (center, tanA, tanB);
    }

    #region Intersection Helpers

    public static Point2D? FindLineIntersection(Point2D p1, Point2D p2, Point2D p3, Point2D p4)
    {
        var d = ((p1.X - p2.X) * (p3.Y - p4.Y)) - ((p1.Y - p2.Y) * (p3.X - p4.X));
        if (Math.Abs(d) < 1e-9) return null;

        var t = (((p1.X - p3.X) * (p3.Y - p4.Y)) - ((p1.Y - p3.Y) * (p3.X - p4.X))) / d;
        return new Point2D(p1.X + (t * (p2.X - p1.X)), p1.Y + (t * (p2.Y - p1.Y)));
    }

    public static Point2D? FindSegmentIntersection(Point2D a1, Point2D a2, Point2D b1, Point2D b2)
    {
        var d = ((a2.X - a1.X) * (b2.Y - b1.Y)) - ((a2.Y - a1.Y) * (b2.X - b1.X));
        if (Math.Abs(d) < 1e-9) return null;

        var u = (((b1.X - a1.X) * (b2.Y - b1.Y)) - ((b1.Y - a1.Y) * (b2.X - b1.X))) / d;
        var v = (((b1.X - a1.X) * (a2.Y - a1.Y)) - ((b1.Y - a1.Y) * (a2.X - a1.X))) / d;

        if (u >= 0.0 && u <= 1.0 && v >= 0.0 && v <= 1.0)
        {
            return new Point2D(a1.X + (u * (a2.X - a1.X)), a1.Y + (u * (a2.Y - a1.Y)));
        }

        return null;
    }

    public static bool IsPointOnSegment(Point2D pt, Point2D p1, Point2D p2, double tolerance = 0.05)
    {
        var distTotal = p1.DistanceTo(p2);
        var dist1 = p1.DistanceTo(pt);
        var dist2 = pt.DistanceTo(p2);
        return Math.Abs((dist1 + dist2) - distTotal) <= tolerance;
    }

    #endregion
}

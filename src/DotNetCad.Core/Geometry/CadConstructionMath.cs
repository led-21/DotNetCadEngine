namespace DotNetCad.Core.Geometry;

public static class CadConstructionMath
{
    public static (Point2D Start, Point2D End) OffsetSegment(Point2D p1, Point2D p2, double distance, bool toLeft = true)
    {
        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        var len = Math.Sqrt((dx * dx) + (dy * dy));

        if (!double.IsFinite(distance) || !double.IsFinite(len) || len < 1e-6)
            throw new ArgumentException("Offset requer segmento finito e não degenerado.");

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
        if (vertices == null || vertices.Count < 3 || !double.IsFinite(distance))
            throw new ArgumentException("Offset requer polígono convexo com pelo menos três vértices finitos.");
        ValidateConvex(vertices);

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
            if (inter == null) throw new ArgumentException("Canto colinear não suportado no offset.");
            result.Add(inter.Value);
        }

        ValidateConvex(result);
        // Every resulting edge must retain its direction. This rejects inward collapse,
        // including the case where both dimensions cross through zero.
        for (int i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            if ((result[j].X-result[i].X)*(vertices[j].X-vertices[i].X) +
                (result[j].Y-result[i].Y)*(vertices[j].Y-vertices[i].Y) <= 1e-9)
                throw new ArgumentException("Offset colapsa ou inverte arestas.");
        }
        return result;
    }

    private static void ValidateConvex(IReadOnlyList<Point2D> points)
    {
        double sign = 0;
        for (int i = 0; i < points.Count; i++)
        {
            var a = points[i]; var b = points[(i+1)%points.Count]; var c = points[(i+2)%points.Count];
            if (!double.IsFinite(a.X) || !double.IsFinite(a.Y) || a.DistanceTo(b) < 1e-6)
                throw new ArgumentException("Vértice inválido ou segmento degenerado.");
            var cross = (b.X-a.X)*(c.Y-b.Y)-(b.Y-a.Y)*(c.X-b.X);
            if (!double.IsFinite(cross) || Math.Abs(cross) < 1e-9 || sign != 0 && Math.Sign(cross) != sign)
                throw new ArgumentException("Offset suporta apenas polígonos estritamente convexos, sem cantos colineares.");
            sign = Math.Sign(cross);
            for (int j = i+2; j < points.Count; j++)
            {
                if ((j+1)%points.Count == i) continue;
                if (FindSegmentIntersection(a,b,points[j],points[(j+1)%points.Count]) != null)
                    throw new ArgumentException("Polígono com auto-interseção não suportado.");
            }
        }
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

        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared < 1e-12) return null;
        var pickPosition = ((clickPoint.X - p1.X) * dx + (clickPoint.Y - p1.Y) * dy) / lengthSquared;
        var intersectionPosition = ((inter.Value.X - p1.X) * dx + (inter.Value.Y - p1.Y) * dy) / lengthSquared;
        if (Math.Abs(pickPosition - intersectionPosition) < 1e-8) return null;

        return pickPosition < intersectionPosition ? (inter.Value, p2) : (p1, inter.Value);
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

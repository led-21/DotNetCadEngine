using DotNetCad.Core.Entities;

namespace DotNetCad.Core.Geometry;

public static class CadConstructionMath
{
    public static (Point2D Start, Point2D End) OffsetSegment(Point2D p1, Point2D p2, double distance, bool toLeft = true, GeometricTolerancePolicy? tol = null)
    {
        tol ??= GeometricTolerancePolicy.Default;
        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        var len = Math.Sqrt((dx * dx) + (dy * dy));

        if (!double.IsFinite(distance) || !double.IsFinite(len) || len < tol.PointCoincidenceTolerance)
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

    /// <summary>
    /// Offsets a polyline (open or closed) by a signed or absolute distance.
    /// Supports open polylines and concave simple polygons (e.g. L or U shapes).
    /// </summary>
    public static List<Point2D> OffsetPolyline(IReadOnlyList<Point2D> vertices, bool isClosed, double distance, GeometricTolerancePolicy? tol = null)
    {
        tol ??= GeometricTolerancePolicy.Default;

        if (vertices == null)
            throw new ArgumentException("Offset requer vértices válidos.");

        // Clean consecutive duplicates
        var cleaned = new List<Point2D>(vertices.Count);
        for (int i = 0; i < vertices.Count; i++)
        {
            var pt = vertices[i];
            if (!double.IsFinite(pt.X) || !double.IsFinite(pt.Y))
                throw new ArgumentException("Vértice inválido com coordenadas não finitas.");

            if (cleaned.Count == 0 || cleaned[^1].DistanceTo(pt) > tol.PointCoincidenceTolerance)
            {
                cleaned.Add(pt);
            }
        }

        if (isClosed && cleaned.Count > 1 && cleaned[0].DistanceTo(cleaned[^1]) <= tol.PointCoincidenceTolerance)
        {
            cleaned.RemoveAt(cleaned.Count - 1);
        }

        if (isClosed && cleaned.Count < 3)
            throw new ArgumentException("Offset de polígono requer pelo menos 3 vértices distintos.");

        if (!isClosed && cleaned.Count < 2)
            throw new ArgumentException("Offset de polilinha aberta requer pelo menos 2 vértices distintos.");

        if (!double.IsFinite(distance) || Math.Abs(distance) < tol.MathEpsilon)
            throw new ArgumentException("Distância de offset deve ser finita e não nula.");

        int n = cleaned.Count;

        if (isClosed)
        {
            // Signed area (shoelace)
            double signedArea = 0.0;
            for (int i = 0; i < n; i++)
            {
                var p1 = cleaned[i];
                var p2 = cleaned[(i + 1) % n];
                signedArea += (p1.X * p2.Y) - (p2.X * p1.Y);
            }

            if (Math.Abs(signedArea) < tol.MinPolygonArea)
                throw new ArgumentException("Área do polígono muito pequena para offset.");

            // Standard convention: signedArea < 0 is CW, toLeft is outward/inward
            bool toLeft = signedArea < 0;
            var distAbs = Math.Abs(distance);
            if (distance < 0)
            {
                toLeft = !toLeft;
            }

            var offsetLines = new List<(Point2D P1, Point2D P2)>(n);
            for (int i = 0; i < n; i++)
            {
                var p1 = cleaned[i];
                var p2 = cleaned[(i + 1) % n];
                offsetLines.Add(OffsetSegment(p1, p2, distAbs, toLeft: toLeft));
            }

            var result = new List<Point2D>(n);
            for (int i = 0; i < n; i++)
            {
                var line1 = offsetLines[(i - 1 + n) % n];
                var line2 = offsetLines[i];

                var inter = CadIntersectionEngine.IntersectLineLine(line1.P1, line1.P2, line2.P1, line2.P2, tol);
                if (inter == null)
                {
                    // Collinear adjacent edges: average the contact points
                    result.Add(new Point2D((line1.P2.X + line2.P1.X) * 0.5, (line1.P2.Y + line2.P1.Y) * 0.5));
                }
                else
                {
                    result.Add(inter.Value);
                }
            }

            // Every resulting edge must retain its direction (rejects edge collapse/crossing zero)
            for (int i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                var dot = ((result[j].X - result[i].X) * (cleaned[j].X - cleaned[i].X)) +
                          ((result[j].Y - result[i].Y) * (cleaned[j].Y - cleaned[i].Y));
                if (dot <= 1e-9)
                    throw new ArgumentException("Offset colapsa ou inverte arestas.");
            }

            // Reject self-intersecting polygon boundaries
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 2; j < n; j++)
                {
                    if ((j + 1) % n == i) continue;
                    if (CadIntersectionEngine.IntersectSegmentSegment(result[i], result[(i + 1) % n], result[j], result[(j + 1) % n], tol) != null)
                        throw new ArgumentException("Polígono com auto-interseção não suportado.");
                }
            }

            return result;
        }
        else
        {
            // Open Polyline Offset
            bool toLeft = distance >= 0;
            var distAbs = Math.Abs(distance);

            var offsetLines = new List<(Point2D P1, Point2D P2)>(n - 1);
            for (int i = 0; i < n - 1; i++)
            {
                var p1 = cleaned[i];
                var p2 = cleaned[i + 1];
                offsetLines.Add(OffsetSegment(p1, p2, distAbs, toLeft: toLeft));
            }

            var result = new List<Point2D>(n);

            // First endpoint: projected perpendicularly from start of first segment
            result.Add(offsetLines[0].P1);

            // Intermediate vertices: mitered intersection between adjacent offset segments
            for (int i = 1; i < n - 1; i++)
            {
                var line1 = offsetLines[i - 1];
                var line2 = offsetLines[i];

                var inter = CadIntersectionEngine.IntersectLineLine(line1.P1, line1.P2, line2.P1, line2.P2, tol);
                if (inter == null)
                {
                    result.Add(new Point2D((line1.P2.X + line2.P1.X) * 0.5, (line1.P2.Y + line2.P1.Y) * 0.5));
                }
                else
                {
                    // Miter limit cap
                    var miterDist = inter.Value.DistanceTo(cleaned[i]);
                    if (miterDist > distAbs * 6.0)
                    {
                        result.Add(line1.P2);
                    }
                    else
                    {
                        result.Add(inter.Value);
                    }
                }
            }

            // Last endpoint: projected perpendicularly from end of last segment
            result.Add(offsetLines[^1].P2);

            return result;
        }
    }

    /// <summary>
    /// Offsets a closed polygon by distance. Forwards to OffsetPolyline with isClosed=true.
    /// </summary>
    public static List<Point2D> OffsetPolygon(IReadOnlyList<Point2D> vertices, double distance)
    {
        return OffsetPolyline(vertices, isClosed: true, distance);
    }

    public static (Point2D Start, Point2D End)? ExtendSegment(Point2D p1, Point2D p2, Point2D boundA, Point2D boundB, GeometricTolerancePolicy? tol = null)
    {
        tol ??= GeometricTolerancePolicy.Default;
        var inter = CadIntersectionEngine.IntersectLineLine(p1, p2, boundA, boundB, tol);
        if (inter == null) return null;

        if (!IsPointOnSegment(inter.Value, boundA, boundB, tolerance: tol.OnCurveTolerance)) return null;

        var vDirX = p2.X - p1.X;
        var vDirY = p2.Y - p1.Y;
        var vExtX = inter.Value.X - p2.X;
        var vExtY = inter.Value.Y - p2.Y;

        var dot = (vDirX * vExtX) + (vDirY * vExtY);
        if (dot < 0) return null;

        return (p1, inter.Value);
    }

    /// <summary>
    /// Extends a segment [p1, p2] towards any boundary entity (CadLine, CadCircle, CadArc, CadPolyline, CadRectangle).
    /// Finds the nearest valid intersection ahead of p2 in the direction of extension.
    /// </summary>
    public static (Point2D Start, Point2D End)? ExtendSegmentToEntity(Point2D p1, Point2D p2, ICadEntity boundaryEntity, GeometricTolerancePolicy? tol = null)
    {
        tol ??= GeometricTolerancePolicy.Default;
        var intersections = new List<Point2D>();
        var segments = CadIntersectionEngine.GetEntitySegments(boundaryEntity);

        if (segments.Count > 0)
        {
            foreach (var seg in segments)
            {
                var pt = CadIntersectionEngine.IntersectLineLine(p1, p2, seg.Start, seg.End, tol);
                if (pt != null && IsPointOnSegment(pt.Value, seg.Start, seg.End, tol.OnCurveTolerance))
                {
                    intersections.Add(pt.Value);
                }
            }
        }
        else if (boundaryEntity is CadCircle circle)
        {
            var pts = CadIntersectionEngine.IntersectLineCircle(p1, p2, circle.Center, circle.Radius, segmentOnly: false, tol);
            intersections.AddRange(pts);
        }
        else if (boundaryEntity is CadArc arc)
        {
            var pts = CadIntersectionEngine.IntersectLineArc(p1, p2, arc, segmentOnly: false, tol);
            intersections.AddRange(pts);
        }

        if (intersections.Count == 0) return null;

        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        var len = Math.Sqrt((dx * dx) + (dy * dy));
        if (len < tol.PointCoincidenceTolerance) return null;

        Point2D? bestIntersection = null;
        double bestDist = double.MaxValue;

        foreach (var pt in intersections)
        {
            var vx = pt.X - p2.X;
            var vy = pt.Y - p2.Y;
            var dot = (dx * vx) + (dy * vy);

            // Forward in direction of extension
            if (dot >= -tol.PointCoincidenceTolerance)
            {
                var dist = p2.DistanceTo(pt);
                if (dist > tol.PointCoincidenceTolerance && dist < bestDist)
                {
                    bestDist = dist;
                    bestIntersection = pt;
                }
            }
        }

        if (bestIntersection == null) return null;
        return (p1, bestIntersection.Value);
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

    /// <summary>
    /// Trims a segment [p1, p2] against any boundary entity (CadLine, CadCircle, CadArc, CadPolyline, CadRectangle).
    /// Selects the intersection point closest to the user's click/pick position.
    /// </summary>
    public static (Point2D Start, Point2D End)? TrimSegmentWithEntity(Point2D p1, Point2D p2, ICadEntity boundaryEntity, Point2D clickPoint, GeometricTolerancePolicy? tol = null)
    {
        tol ??= GeometricTolerancePolicy.Default;
        var dummyLine = new CadLine(p1, p2, "temp", "#ffffff");
        var intersections = CadIntersectionEngine.IntersectEntityEntity(dummyLine, boundaryEntity, tol);
        if (intersections.Count == 0) return null;

        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        var lenSq = (dx * dx) + (dy * dy);
        if (lenSq < 1e-12) return null;

        var pickPos = (((clickPoint.X - p1.X) * dx) + ((clickPoint.Y - p1.Y) * dy)) / lenSq;

        Point2D? bestInter = null;
        double bestParamDiff = double.MaxValue;
        double bestT = 0.0;

        foreach (var inter in intersections)
        {
            var t = (((inter.X - p1.X) * dx) + ((inter.Y - p1.Y) * dy)) / lenSq;
            var diff = Math.Abs(pickPos - t);
            if (diff < bestParamDiff)
            {
                bestParamDiff = diff;
                bestInter = inter;
                bestT = t;
            }
        }

        if (bestInter == null || Math.Abs(pickPos - bestT) < 1e-6) return null;

        return pickPos < bestT ? (bestInter.Value, p2) : (p1, bestInter.Value);
    }

    public static ((Point2D Start, Point2D End) Seg1, (Point2D Start, Point2D End) Seg2)? BreakSegment(Point2D p1, Point2D p2, Point2D breakPoint, GeometricTolerancePolicy? tol = null)
    {
        tol ??= GeometricTolerancePolicy.Default;
        var seg = new LineSegment2D(p1, p2);
        var proj = seg.ClosestPointTo(breakPoint);
        var d1 = p1.DistanceTo(proj);
        var d2 = p2.DistanceTo(proj);
        if (d1 < tol.OnCurveTolerance || d2 < tol.OnCurveTolerance) return null;
        return ((p1, proj), (proj, p2));
    }

    public static (Point2D Center, Point2D TangentA, Point2D TangentB)? CreateFillet(Point2D a1, Point2D a2, Point2D b1, Point2D b2, double radius, GeometricTolerancePolicy? tol = null)
    {
        tol ??= GeometricTolerancePolicy.Default;
        var inter = CadIntersectionEngine.IntersectLineLine(a1, a2, b1, b2, tol);
        if (inter == null || radius < tol.MinEntityDimension) return null;

        var v1 = new Point2D(a1.X - inter.Value.X, a1.Y - inter.Value.Y);
        var v2 = new Point2D(b2.X - inter.Value.X, b2.Y - inter.Value.Y);

        var len1 = Math.Sqrt((v1.X * v1.X) + (v1.Y * v1.Y));
        var len2 = Math.Sqrt((v2.X * v2.X) + (v2.Y * v2.Y));
        if (len1 < tol.PointCoincidenceTolerance || len2 < tol.PointCoincidenceTolerance) return null;

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
        return CadIntersectionEngine.IntersectLineLine(p1, p2, p3, p4);
    }

    public static Point2D? FindSegmentIntersection(Point2D a1, Point2D a2, Point2D b1, Point2D b2)
    {
        return CadIntersectionEngine.IntersectSegmentSegment(a1, a2, b1, b2);
    }

    /// <summary>
    /// Tests whether a point lies on a segment [p1, p2] using parametric projection and orthogonal distance.
    /// Eliminates triangular inequality round-off errors at segment endpoints.
    /// </summary>
    public static bool IsPointOnSegment(Point2D pt, Point2D p1, Point2D p2, double tolerance = 0.05)
    {
        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        var lenSq = (dx * dx) + (dy * dy);
        if (lenSq < 1e-12)
            return pt.DistanceTo(p1) <= tolerance;

        var len = Math.Sqrt(lenSq);
        var t = (((pt.X - p1.X) * dx) + ((pt.Y - p1.Y) * dy)) / lenSq;
        var epsT = tolerance / len;

        if (t < -epsT || t > 1.0 + epsT) return false;

        var projX = p1.X + (t * dx);
        var projY = p1.Y + (t * dy);
        var distSq = ((pt.X - projX) * (pt.X - projX)) + ((pt.Y - projY) * (pt.Y - projY));
        return distSq <= tolerance * tolerance;
    }

    #endregion
}

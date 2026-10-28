using DotNetCad.Core.Entities;

namespace DotNetCad.Core.Geometry;

/// <summary>
/// High-precision 2D CAD geometric intersection engine supporting micro to geodetic scales.
/// Provides stabilized intersections for Line-Line, Line-Circle, Line-Arc, Circle-Circle,
/// and polymorphic Entity-Entity combinations.
/// </summary>
public static class CadIntersectionEngine
{
    /// <summary>
    /// Intersects two infinite lines defined by (p1, p2) and (p3, p4).
    /// Returns null if lines are parallel or coincident within tolerance.
    /// </summary>
    public static Point2D? IntersectLineLine(Point2D p1, Point2D p2, Point2D p3, Point2D p4, GeometricTolerancePolicy? tol = null)
    {
        tol ??= GeometricTolerancePolicy.Default;

        var d1x = p2.X - p1.X;
        var d1y = p2.Y - p1.Y;
        var d2x = p4.X - p3.X;
        var d2y = p4.Y - p3.Y;

        var len1 = Math.Sqrt((d1x * d1x) + (d1y * d1y));
        var len2 = Math.Sqrt((d2x * d2x) + (d2y * d2y));
        if (len1 < tol.PointCoincidenceTolerance || len2 < tol.PointCoincidenceTolerance)
            return null;

        var det = (d1x * d2y) - (d1y * d2x);
        var normDet = det / (len1 * len2);

        if (Math.Abs(normDet) < tol.MathEpsilon)
            return null; // Parallel or anti-parallel

        var t = (((p3.X - p1.X) * d2y) - ((p3.Y - p1.Y) * d2x)) / det;
        return new Point2D(p1.X + (t * d1x), p1.Y + (t * d1y));
    }

    /// <summary>
    /// Intersects two finite line segments [a1, a2] and [b1, b2].
    /// Returns the single intersection point if segments cross, or null if parallel / non-intersecting.
    /// </summary>
    public static Point2D? IntersectSegmentSegment(Point2D a1, Point2D a2, Point2D b1, Point2D b2, GeometricTolerancePolicy? tol = null)
    {
        tol ??= GeometricTolerancePolicy.Default;

        var dax = a2.X - a1.X;
        var day = a2.Y - a1.Y;
        var dbx = b2.X - b1.X;
        var dby = b2.Y - b1.Y;

        var lena = Math.Sqrt((dax * dax) + (day * day));
        var lenb = Math.Sqrt((dbx * dbx) + (dby * dby));
        if (lena < tol.PointCoincidenceTolerance || lenb < tol.PointCoincidenceTolerance)
            return null;

        var det = (dax * dby) - (day * dbx);
        var normDet = det / (lena * lenb);

        if (Math.Abs(normDet) < tol.MathEpsilon)
        {
            // Segments are parallel or collinear; check for endpoint contact
            if (a1.DistanceTo(b1) <= tol.PointCoincidenceTolerance || a1.DistanceTo(b2) <= tol.PointCoincidenceTolerance)
                return a1;
            if (a2.DistanceTo(b1) <= tol.PointCoincidenceTolerance || a2.DistanceTo(b2) <= tol.PointCoincidenceTolerance)
                return a2;
            return null;
        }

        var dx = b1.X - a1.X;
        var dy = b1.Y - a1.Y;

        var u = ((dx * dby) - (dy * dbx)) / det;
        var v = ((dx * day) - (dy * dax)) / det;

        var epsU = tol.OnCurveTolerance / lena;
        var epsV = tol.OnCurveTolerance / lenb;

        if (u >= -epsU && u <= 1.0 + epsU && v >= -epsV && v <= 1.0 + epsV)
        {
            var clampedU = Math.Clamp(u, 0.0, 1.0);
            return new Point2D(a1.X + (clampedU * dax), a1.Y + (clampedU * day));
        }

        return null;
    }

    /// <summary>
    /// Intersects a line (or finite segment) with a circle.
    /// Returns 0, 1 (tangent), or 2 intersection points.
    /// </summary>
    public static IReadOnlyList<Point2D> IntersectLineCircle(
        Point2D p1, Point2D p2, Point2D center, double radius,
        bool segmentOnly = true, GeometricTolerancePolicy? tol = null)
    {
        tol ??= GeometricTolerancePolicy.Default;

        if (radius < tol.MinEntityDimension) return [];

        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        var len = Math.Sqrt((dx * dx) + (dy * dy));
        if (len < tol.PointCoincidenceTolerance) return [];

        var fx = p1.X - center.X;
        var fy = p1.Y - center.Y;

        var a = (dx * dx) + (dy * dy);
        var b = 2.0 * ((fx * dx) + (fy * dy));
        var c = (fx * fx) + (fy * fy) - (radius * radius);

        var discriminant = (b * b) - (4.0 * a * c);
        var epsT = tol.OnCurveTolerance / len;

        // Tangency tolerance scaled by a
        if (Math.Abs(discriminant) <= tol.MathEpsilon * a)
        {
            var t = -b / (2.0 * a);
            if (!segmentOnly || (t >= -epsT && t <= 1.0 + epsT))
            {
                var clampedT = segmentOnly ? Math.Clamp(t, 0.0, 1.0) : t;
                return [new Point2D(p1.X + (clampedT * dx), p1.Y + (clampedT * dy))];
            }
            return [];
        }

        if (discriminant < 0) return [];

        var sqrtDisc = Math.Sqrt(discriminant);
        var t1 = (-b - sqrtDisc) / (2.0 * a);
        var t2 = (-b + sqrtDisc) / (2.0 * a);

        var results = new List<Point2D>(2);

        if (!segmentOnly || (t1 >= -epsT && t1 <= 1.0 + epsT))
        {
            var clamped = segmentOnly ? Math.Clamp(t1, 0.0, 1.0) : t1;
            results.Add(new Point2D(p1.X + (clamped * dx), p1.Y + (clamped * dy)));
        }

        if (!segmentOnly || (t2 >= -epsT && t2 <= 1.0 + epsT))
        {
            var clamped = segmentOnly ? Math.Clamp(t2, 0.0, 1.0) : t2;
            var pt = new Point2D(p1.X + (clamped * dx), p1.Y + (clamped * dy));
            if (results.Count == 0 || results[0].DistanceTo(pt) > tol.PointCoincidenceTolerance)
            {
                results.Add(pt);
            }
        }

        return results;
    }

    /// <summary>
    /// Intersects a line (or finite segment) with a circular arc.
    /// Filters circle intersections by the CCW angular range [StartAngle, EndAngle].
    /// </summary>
    public static IReadOnlyList<Point2D> IntersectLineArc(
        Point2D p1, Point2D p2, CadArc arc,
        bool segmentOnly = true, GeometricTolerancePolicy? tol = null)
    {
        tol ??= GeometricTolerancePolicy.Default;

        var circleIntersections = IntersectLineCircle(p1, p2, arc.Center, arc.Radius, segmentOnly, tol);
        if (circleIntersections.Count == 0) return [];

        var filtered = new List<Point2D>(circleIntersections.Count);
        foreach (var pt in circleIntersections)
        {
            if (IsAngleOnArc(pt, arc, tol))
            {
                filtered.Add(pt);
            }
        }

        return filtered;
    }

    /// <summary>
    /// Intersects two circles. Returns 0, 1 (tangent), or 2 intersection points.
    /// </summary>
    public static IReadOnlyList<Point2D> IntersectCircleCircle(
        Point2D c1, double r1, Point2D c2, double r2,
        GeometricTolerancePolicy? tol = null)
    {
        tol ??= GeometricTolerancePolicy.Default;

        var d = c1.DistanceTo(c2);
        if (d < tol.PointCoincidenceTolerance) return []; // Concentric
        if (d > r1 + r2 + tol.PointCoincidenceTolerance) return []; // Separate
        if (d < Math.Abs(r1 - r2) - tol.PointCoincidenceTolerance) return []; // Contained

        // Tangent externally or internally
        if (Math.Abs(d - (r1 + r2)) <= tol.PointCoincidenceTolerance ||
            Math.Abs(d - Math.Abs(r1 - r2)) <= tol.PointCoincidenceTolerance)
        {
            var dirX = (c2.X - c1.X) / d;
            var dirY = (c2.Y - c1.Y) / d;
            return [new Point2D(c1.X + (dirX * r1), c1.Y + (dirY * r1))];
        }

        var a = ((r1 * r1) - (r2 * r2) + (d * d)) / (2.0 * d);
        var hSquared = (r1 * r1) - (a * a);
        var h = hSquared > 0 ? Math.Sqrt(hSquared) : 0.0;

        var p2x = c1.X + ((a * (c2.X - c1.X)) / d);
        var p2y = c1.Y + ((a * (c2.Y - c1.Y)) / d);

        var rx = -((c2.Y - c1.Y) * (h / d));
        var ry = (c2.X - c1.X) * (h / d);

        var pt1 = new Point2D(p2x + rx, p2y + ry);
        var pt2 = new Point2D(p2x - rx, p2y - ry);

        if (pt1.DistanceTo(pt2) <= tol.PointCoincidenceTolerance)
            return [pt1];

        return [pt1, pt2];
    }

    /// <summary>
    /// Tests if a point on the circle falls within the circular arc's CCW angular sector.
    /// </summary>
    public static bool IsAngleOnArc(Point2D pt, CadArc arc, GeometricTolerancePolicy? tol = null)
    {
        tol ??= GeometricTolerancePolicy.Default;

        var angleRad = Math.Atan2(pt.Y - arc.Center.Y, pt.X - arc.Center.X);
        var angleDeg = angleRad * (180.0 / Math.PI);
        if (angleDeg < 0) angleDeg += 360.0;

        var start = NormalizeAngle(arc.StartAngleDegrees);
        var end = NormalizeAngle(arc.EndAngleDegrees);
        var epsDeg = (tol.OnCurveTolerance / Math.Max(arc.Radius, 1e-4)) * (180.0 / Math.PI);

        if (Math.Abs(end - start) < tol.MathEpsilon)
            return true; // Full circle represented as 0..360 or equal start/end

        if (end > start)
        {
            return angleDeg >= start - epsDeg && angleDeg <= end + epsDeg;
        }
        else
        {
            // Arc crosses the 0-degree discontinuity (e.g. 350 to 20)
            return angleDeg >= start - epsDeg || angleDeg <= end + epsDeg;
        }
    }

    private static double NormalizeAngle(double deg)
    {
        var mod = deg % 360.0;
        return mod < 0 ? mod + 360.0 : mod;
    }

    /// <summary>
    /// Computes all geometric intersections between any two CAD entities.
    /// Supports polymorphic combinations of CadLine, CadPolyline, CadRectangle, CadCircle, and CadArc.
    /// </summary>
    public static IReadOnlyList<Point2D> IntersectEntityEntity(ICadEntity a, ICadEntity b, GeometricTolerancePolicy? tol = null)
    {
        tol ??= GeometricTolerancePolicy.Default;
        var results = new List<Point2D>();

        var segmentsA = GetEntitySegments(a);
        var segmentsB = GetEntitySegments(b);

        // Segment-to-segment intersections (covers Line, Polyline, Rectangle combinations)
        if (segmentsA.Count > 0 && segmentsB.Count > 0)
        {
            foreach (var sa in segmentsA)
            {
                foreach (var sb in segmentsB)
                {
                    var inter = IntersectSegmentSegment(sa.Start, sa.End, sb.Start, sb.End, tol);
                    if (inter != null) AddUnique(results, inter.Value, tol);
                }
            }
            return results;
        }

        // Segments vs Circle/Arc
        if (segmentsA.Count > 0)
        {
            foreach (var seg in segmentsA)
            {
                if (b is CadCircle c)
                {
                    foreach (var p in IntersectLineCircle(seg.Start, seg.End, c.Center, c.Radius, true, tol))
                        AddUnique(results, p, tol);
                }
                else if (b is CadArc arc)
                {
                    foreach (var p in IntersectLineArc(seg.Start, seg.End, arc, true, tol))
                        AddUnique(results, p, tol);
                }
            }
            return results;
        }

        if (segmentsB.Count > 0)
        {
            foreach (var seg in segmentsB)
            {
                if (a is CadCircle c)
                {
                    foreach (var p in IntersectLineCircle(seg.Start, seg.End, c.Center, c.Radius, true, tol))
                        AddUnique(results, p, tol);
                }
                else if (a is CadArc arc)
                {
                    foreach (var p in IntersectLineArc(seg.Start, seg.End, arc, true, tol))
                        AddUnique(results, p, tol);
                }
            }
            return results;
        }

        // Circle / Arc combinations
        if (a is CadCircle c1 && b is CadCircle c2)
        {
            foreach (var p in IntersectCircleCircle(c1.Center, c1.Radius, c2.Center, c2.Radius, tol))
                AddUnique(results, p, tol);
        }
        else if (a is CadCircle circ && b is CadArc arcB)
        {
            foreach (var p in IntersectCircleCircle(circ.Center, circ.Radius, arcB.Center, arcB.Radius, tol))
            {
                if (IsAngleOnArc(p, arcB, tol)) AddUnique(results, p, tol);
            }
        }
        else if (a is CadArc arcA && b is CadCircle circB)
        {
            foreach (var p in IntersectCircleCircle(arcA.Center, arcA.Radius, circB.Center, circB.Radius, tol))
            {
                if (IsAngleOnArc(p, arcA, tol)) AddUnique(results, p, tol);
            }
        }
        else if (a is CadArc arc1 && b is CadArc arc2)
        {
            foreach (var p in IntersectCircleCircle(arc1.Center, arc1.Radius, arc2.Center, arc2.Radius, tol))
            {
                if (IsAngleOnArc(p, arc1, tol) && IsAngleOnArc(p, arc2, tol)) AddUnique(results, p, tol);
            }
        }

        return results;
    }

    /// <summary>
    /// Decomposes linear or polygonal entities into discrete segments for intersection tests.
    /// </summary>
    public static IReadOnlyList<(Point2D Start, Point2D End)> GetEntitySegments(ICadEntity entity)
    {
        if (entity is CadLine l)
        {
            return [(l.StartPoint, l.EndPoint)];
        }
        if (entity is CadRectangle r)
        {
            var v = r.GetVertices();
            if (v.Count >= 4)
            {
                return [(v[0], v[1]), (v[1], v[2]), (v[2], v[3]), (v[3], v[0])];
            }
        }
        if (entity is CadPolyline poly && poly.Vertices.Count >= 2)
        {
            var segments = new List<(Point2D Start, Point2D End)>(poly.Vertices.Count);
            for (int i = 0; i < poly.Vertices.Count - 1; i++)
            {
                segments.Add((poly.Vertices[i], poly.Vertices[i + 1]));
            }
            if (poly.IsClosed && poly.Vertices.Count > 2)
            {
                segments.Add((poly.Vertices[^1], poly.Vertices[0]));
            }
            return segments;
        }

        return [];
    }

    private static void AddUnique(List<Point2D> list, Point2D pt, GeometricTolerancePolicy tol)
    {
        foreach (var existing in list)
        {
            if (existing.DistanceTo(pt) <= tol.PointCoincidenceTolerance) return;
        }
        list.Add(pt);
    }
}

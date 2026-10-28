using DotNetCad.Core.Entities;

namespace DotNetCad.Core.Geometry;

public sealed record TrimResult(
    ICadEntity OriginalEntity,
    IReadOnlyList<ICadEntity> RemainingEntities,
    ICadEntity DiscardedSegment,
    Point2D IntersectionPoint);

public sealed record ExtendResult(
    ICadEntity OriginalEntity,
    ICadEntity ExtendedEntity,
    Point2D ExtensionPoint,
    bool ExtendedStart);

/// <summary>
/// High-precision 2D CAD computational geometry engine for TRIM (Aparar) and EXTEND (Estender).
/// Supports polymorphic combinations of CadLine, CadCircle, CadArc, CadPolyline, and CadRectangle.
/// </summary>
public static class CadTrimExtendEngine
{
    #region Intersections Collection

    /// <summary>
    /// Computes all geometric intersections between a target entity and a collection of boundary entities.
    /// </summary>
    public static IReadOnlyList<Point2D> FindIntersections(
        ICadEntity target,
        IEnumerable<ICadEntity> boundaries,
        GeometricTolerancePolicy? tol = null)
    {
        tol ??= GeometricTolerancePolicy.Default;
        var results = new List<Point2D>();

        foreach (var boundary in boundaries)
        {
            if (boundary.Id == target.Id) continue;

            var pts = CadIntersectionEngine.IntersectEntityEntity(target, boundary, tol);
            foreach (var p in pts)
            {
                if (!results.Any(existing => existing.DistanceTo(p) <= tol.PointCoincidenceTolerance))
                {
                    results.Add(p);
                }
            }
        }

        return results;
    }

    #endregion

    #region TRIM

    /// <summary>
    /// Computes the TRIM operation on an entity given boundary entities and the click pick point.
    /// Returns null if trimming cannot be performed (e.g. no valid intersections).
    /// </summary>
    public static TrimResult? Trim(
        ICadEntity target,
        IEnumerable<ICadEntity> boundaries,
        Point2D pickPoint,
        GeometricTolerancePolicy? tol = null)
    {
        tol ??= GeometricTolerancePolicy.Default;

        return target switch
        {
            CadLine line => TrimLine(line, boundaries, pickPoint, tol),
            CadCircle circle => TrimCircle(circle, boundaries, pickPoint, tol),
            CadArc arc => TrimArc(arc, boundaries, pickPoint, tol),
            CadPolyline polyline => TrimPolyline(polyline, boundaries, pickPoint, tol),
            CadRectangle rect => TrimRectangle(rect, boundaries, pickPoint, tol),
            _ => null
        };
    }

    private static TrimResult? TrimLine(
        CadLine line,
        IEnumerable<ICadEntity> boundaries,
        Point2D pickPoint,
        GeometricTolerancePolicy tol)
    {
        var dx = line.EndPoint.X - line.StartPoint.X;
        var dy = line.EndPoint.Y - line.StartPoint.Y;
        var len = Math.Sqrt((dx * dx) + (dy * dy));
        if (len < tol.MinEntityDimension) return null;

        var ux = dx / len;
        var uy = dy / len;

        // Collect intersections strictly inside line interior
        var intersections = FindIntersections(line, boundaries, tol);
        var tValues = new List<double>();

        var epsT = tol.PointCoincidenceTolerance / len;

        foreach (var pt in intersections)
        {
            var t = (((pt.X - line.StartPoint.X) * ux) + ((pt.Y - line.StartPoint.Y) * uy)) / len;
            if (t > epsT && t < 1.0 - epsT)
            {
                if (!tValues.Any(existing => Math.Abs(existing - t) <= epsT))
                {
                    tValues.Add(t);
                }
            }
        }

        if (tValues.Count == 0) return null;

        tValues.Sort();

        // Intervals from 0 to 1
        var allT = new List<double> { 0.0 };
        allT.AddRange(tValues);
        allT.Add(1.0);

        var tPick = (((pickPoint.X - line.StartPoint.X) * ux) + ((pickPoint.Y - line.StartPoint.Y) * uy)) / len;
        tPick = Math.Clamp(tPick, 0.0, 1.0);

        // Find which interval contains tPick
        int cutIdx = -1;
        for (int i = 0; i < allT.Count - 1; i++)
        {
            if (tPick >= allT[i] - 1e-9 && tPick <= allT[i + 1] + 1e-9)
            {
                cutIdx = i;
                break;
            }
        }

        if (cutIdx == -1) cutIdx = 0;

        var tStart = allT[cutIdx];
        var tEnd = allT[cutIdx + 1];

        var discardP1 = new Point2D(line.StartPoint.X + (tStart * dx), line.StartPoint.Y + (tStart * dy));
        var discardP2 = new Point2D(line.StartPoint.X + (tEnd * dx), line.StartPoint.Y + (tEnd * dy));
        var discarded = new CadLine(discardP1, discardP2, line.Layer, line.ColorHex)
        {
            LineThickness = line.LineThickness
        };

        var remaining = new List<ICadEntity>();

        // Segment before cut
        if (cutIdx > 0)
        {
            var p1 = line.StartPoint;
            var p2 = new Point2D(line.StartPoint.X + (tStart * dx), line.StartPoint.Y + (tStart * dy));
            if (p1.DistanceTo(p2) >= tol.MinEntityDimension)
            {
                remaining.Add(new CadLine(p1, p2, line.Layer, line.ColorHex)
                {
                    Id = line.Id, // Preserves original ID
                    LineThickness = line.LineThickness
                });
            }
        }

        // Segment after cut
        if (cutIdx < allT.Count - 2)
        {
            var p1 = new Point2D(line.StartPoint.X + (tEnd * dx), line.StartPoint.Y + (tEnd * dy));
            var p2 = line.EndPoint;
            if (p1.DistanceTo(p2) >= tol.MinEntityDimension)
            {
                var newId = remaining.Count == 0 ? line.Id : Guid.NewGuid();
                remaining.Add(new CadLine(p1, p2, line.Layer, line.ColorHex)
                {
                    Id = newId,
                    LineThickness = line.LineThickness
                });
            }
        }

        if (remaining.Count == 0) return null;

        var interPt = cutIdx > 0 ? discardP1 : discardP2;
        return new TrimResult(line, remaining, discarded, interPt);
    }

    private static TrimResult? TrimCircle(
        CadCircle circle,
        IEnumerable<ICadEntity> boundaries,
        Point2D pickPoint,
        GeometricTolerancePolicy tol)
    {
        var intersections = FindIntersections(circle, boundaries, tol);
        if (intersections.Count < 2) return null; // Circle requires at least 2 cut points

        // Calculate polar angles in degrees [0, 360)
        var angles = new List<double>();
        foreach (var pt in intersections)
        {
            var rad = Math.Atan2(pt.Y - circle.Center.Y, pt.X - circle.Center.X);
            var deg = (rad * 180.0 / Math.PI + 360.0) % 360.0;
            if (!angles.Any(a => Math.Abs(NormalizeDeltaAngle(a - deg)) < 0.05))
            {
                angles.Add(deg);
            }
        }

        if (angles.Count < 2) return null;

        angles.Sort();

        // Calculate angle of pick point
        var pickRad = Math.Atan2(pickPoint.Y - circle.Center.Y, pickPoint.X - circle.Center.X);
        var pickDeg = (pickRad * 180.0 / Math.PI + 360.0) % 360.0;

        // Find sector containing pickDeg
        int cutSector = -1;
        int count = angles.Count;
        for (int i = 0; i < count; i++)
        {
            var aStart = angles[i];
            var aEnd = angles[(i + 1) % count];
            if (IsAngleInSector(pickDeg, aStart, aEnd))
            {
                cutSector = i;
                break;
            }
        }

        if (cutSector == -1) cutSector = 0;

        var discardStartAngle = angles[cutSector];
        var discardEndAngle = angles[(cutSector + 1) % count];

        var discarded = new CadArc(circle.Center, circle.Radius, discardStartAngle, discardEndAngle, circle.Layer, circle.ColorHex)
        {
            LineThickness = circle.LineThickness
        };

        var remaining = new List<ICadEntity>();
        bool first = true;

        for (int i = 0; i < count; i++)
        {
            if (i == cutSector) continue;

            var aStart = angles[i];
            var aEnd = angles[(i + 1) % count];
            var arc = new CadArc(circle.Center, circle.Radius, aStart, aEnd, circle.Layer, circle.ColorHex)
            {
                Id = first ? circle.Id : Guid.NewGuid(),
                LineThickness = circle.LineThickness
            };
            remaining.Add(arc);
            first = false;
        }

        if (remaining.Count == 0) return null;

        var radInter = discardStartAngle * Math.PI / 180.0;
        var interPt = new Point2D(circle.Center.X + (circle.Radius * Math.Cos(radInter)), circle.Center.Y + (circle.Radius * Math.Sin(radInter)));

        return new TrimResult(circle, remaining, discarded, interPt);
    }

    private static TrimResult? TrimArc(
        CadArc arc,
        IEnumerable<ICadEntity> boundaries,
        Point2D pickPoint,
        GeometricTolerancePolicy tol)
    {
        var intersections = FindIntersections(arc, boundaries, tol);
        var offsets = new List<double>();
        var sweep = arc.SweepAngleDegrees;

        var epsDeg = (tol.PointCoincidenceTolerance / Math.Max(arc.Radius, 1e-4)) * (180.0 / Math.PI);

        foreach (var pt in intersections)
        {
            var rad = Math.Atan2(pt.Y - arc.Center.Y, pt.X - arc.Center.X);
            var deg = (rad * 180.0 / Math.PI + 360.0) % 360.0;
            var delta = (deg - arc.StartAngleDegrees + 360.0) % 360.0;

            if (delta > epsDeg && delta < sweep - epsDeg)
            {
                if (!offsets.Any(existing => Math.Abs(existing - delta) <= epsDeg))
                {
                    offsets.Add(delta);
                }
            }
        }

        if (offsets.Count == 0) return null;

        offsets.Sort();

        var allOffsets = new List<double> { 0.0 };
        allOffsets.AddRange(offsets);
        allOffsets.Add(sweep);

        var pickRad = Math.Atan2(pickPoint.Y - arc.Center.Y, pickPoint.X - arc.Center.X);
        var pickDeg = (pickRad * 180.0 / Math.PI + 360.0) % 360.0;
        var pickOffset = Math.Clamp((pickDeg - arc.StartAngleDegrees + 360.0) % 360.0, 0.0, sweep);

        int cutIdx = -1;
        for (int i = 0; i < allOffsets.Count - 1; i++)
        {
            if (pickOffset >= allOffsets[i] - 1e-9 && pickOffset <= allOffsets[i + 1] + 1e-9)
            {
                cutIdx = i;
                break;
            }
        }

        if (cutIdx == -1) cutIdx = 0;

        var dStart = (arc.StartAngleDegrees + allOffsets[cutIdx]) % 360.0;
        var dEnd = (arc.StartAngleDegrees + allOffsets[cutIdx + 1]) % 360.0;

        var discarded = new CadArc(arc.Center, arc.Radius, dStart, dEnd, arc.Layer, arc.ColorHex)
        {
            LineThickness = arc.LineThickness
        };

        var remaining = new List<ICadEntity>();

        // Arc before cut
        if (cutIdx > 0)
        {
            var aStart = arc.StartAngleDegrees;
            var aEnd = (arc.StartAngleDegrees + allOffsets[cutIdx]) % 360.0;
            remaining.Add(new CadArc(arc.Center, arc.Radius, aStart, aEnd, arc.Layer, arc.ColorHex)
            {
                Id = arc.Id,
                LineThickness = arc.LineThickness
            });
        }

        // Arc after cut
        if (cutIdx < allOffsets.Count - 2)
        {
            var aStart = (arc.StartAngleDegrees + allOffsets[cutIdx + 1]) % 360.0;
            var aEnd = arc.EndAngleDegrees;
            remaining.Add(new CadArc(arc.Center, arc.Radius, aStart, aEnd, arc.Layer, arc.ColorHex)
            {
                Id = remaining.Count == 0 ? arc.Id : Guid.NewGuid(),
                LineThickness = arc.LineThickness
            });
        }

        if (remaining.Count == 0) return null;

        var radInter = dStart * Math.PI / 180.0;
        var interPt = new Point2D(arc.Center.X + (arc.Radius * Math.Cos(radInter)), arc.Center.Y + (arc.Radius * Math.Sin(radInter)));

        return new TrimResult(arc, remaining, discarded, interPt);
    }

    private static TrimResult? TrimPolyline(
        CadPolyline polyline,
        IEnumerable<ICadEntity> boundaries,
        Point2D pickPoint,
        GeometricTolerancePolicy tol)
    {
        if (polyline.Vertices.Count < 2) return null;

        // Decompose into line segments, find segment closest to pickPoint
        var segments = new List<CadLine>();
        int segCount = polyline.Vertices.Count - 1;
        int closestSegIdx = -1;
        double minDistance = double.MaxValue;

        for (int i = 0; i < segCount; i++)
        {
            var line = new CadLine(polyline.Vertices[i], polyline.Vertices[i + 1], polyline.Layer, polyline.ColorHex)
            {
                LineThickness = polyline.LineThickness
            };
            segments.Add(line);

            var seg = new LineSegment2D(line.StartPoint, line.EndPoint);
            var dist = seg.DistanceTo(pickPoint);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestSegIdx = i;
            }
        }

        if (polyline.IsClosed && polyline.Vertices.Count >= 3)
        {
            var line = new CadLine(polyline.Vertices[^1], polyline.Vertices[0], polyline.Layer, polyline.ColorHex)
            {
                LineThickness = polyline.LineThickness
            };
            segments.Add(line);
            var seg = new LineSegment2D(line.StartPoint, line.EndPoint);
            var dist = seg.DistanceTo(pickPoint);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestSegIdx = segments.Count - 1;
            }
        }

        if (closestSegIdx == -1) return null;

        // Trim the target segment
        var targetSeg = segments[closestSegIdx];
        var segTrim = TrimLine(targetSeg, boundaries, pickPoint, tol);
        if (segTrim == null) return null;

        // Reconstruct polyline(s)
        var remaining = new List<ICadEntity>();
        if (polyline.IsClosed)
        {
            // If closed and trimmed, it becomes an open polyline starting after the cut
            var openVertices = new List<Point2D>();
            for (int i = (closestSegIdx + 1) % segments.Count; i != closestSegIdx; i = (i + 1) % segments.Count)
            {
                if (openVertices.Count == 0) openVertices.Add(segments[i].StartPoint);
                openVertices.Add(segments[i].EndPoint);
            }
            if (segTrim.RemainingEntities.Count > 0)
            {
                foreach (var rem in segTrim.RemainingEntities.OfType<CadLine>())
                {
                    openVertices.Add(rem.EndPoint);
                }
            }
            if (openVertices.Count >= 2)
            {
                remaining.Add(new CadPolyline(openVertices, false, polyline.Layer, polyline.ColorHex)
                {
                    Id = polyline.Id,
                    LineThickness = polyline.LineThickness
                });
            }
        }
        else
        {
            // Open polyline: trimmed segment can shorten either end or split polyline
            if (closestSegIdx == 0 && segTrim.RemainingEntities.Count == 1)
            {
                // Shortened at start
                var newVerts = new List<Point2D> { ((CadLine)segTrim.RemainingEntities[0]).StartPoint };
                for (int i = 1; i < polyline.Vertices.Count; i++) newVerts.Add(polyline.Vertices[i]);
                remaining.Add(new CadPolyline(newVerts, false, polyline.Layer, polyline.ColorHex)
                {
                    Id = polyline.Id,
                    LineThickness = polyline.LineThickness
                });
            }
            else if (closestSegIdx == segCount - 1 && segTrim.RemainingEntities.Count == 1)
            {
                // Shortened at end
                var newVerts = new List<Point2D>();
                for (int i = 0; i < polyline.Vertices.Count - 1; i++) newVerts.Add(polyline.Vertices[i]);
                newVerts.Add(((CadLine)segTrim.RemainingEntities[0]).EndPoint);
                remaining.Add(new CadPolyline(newVerts, false, polyline.Layer, polyline.ColorHex)
                {
                    Id = polyline.Id,
                    LineThickness = polyline.LineThickness
                });
            }
            else
            {
                // Internal split: convert remaining contiguous segments to separate polylines
                var part1Verts = new List<Point2D>();
                for (int i = 0; i <= closestSegIdx; i++) part1Verts.Add(polyline.Vertices[i]);
                if (segTrim.RemainingEntities.Count > 0 && ((CadLine)segTrim.RemainingEntities[0]).StartPoint.DistanceTo(part1Verts[^1]) < tol.PointCoincidenceTolerance)
                {
                    part1Verts[^1] = ((CadLine)segTrim.RemainingEntities[0]).EndPoint;
                }

                var part2Verts = new List<Point2D>();
                if (segTrim.RemainingEntities.Count > 1)
                {
                    part2Verts.Add(((CadLine)segTrim.RemainingEntities[1]).StartPoint);
                }
                for (int i = closestSegIdx + 1; i < polyline.Vertices.Count; i++) part2Verts.Add(polyline.Vertices[i]);

                if (part1Verts.Count >= 2)
                {
                    remaining.Add(new CadPolyline(part1Verts, false, polyline.Layer, polyline.ColorHex)
                    {
                        Id = polyline.Id,
                        LineThickness = polyline.LineThickness
                    });
                }
                if (part2Verts.Count >= 2)
                {
                    remaining.Add(new CadPolyline(part2Verts, false, polyline.Layer, polyline.ColorHex)
                    {
                        Id = Guid.NewGuid(),
                        LineThickness = polyline.LineThickness
                    });
                }
            }
        }

        if (remaining.Count == 0) return null;

        return new TrimResult(polyline, remaining, segTrim.DiscardedSegment, segTrim.IntersectionPoint);
    }

    private static TrimResult? TrimRectangle(
        CadRectangle rect,
        IEnumerable<ICadEntity> boundaries,
        Point2D pickPoint,
        GeometricTolerancePolicy tol)
    {
        // Convert rectangle to closed polyline and trim
        var vertices = rect.GetVertices();
        var poly = new CadPolyline(vertices, true, rect.Layer, rect.ColorHex)
        {
            Id = rect.Id,
            LineThickness = rect.LineThickness
        };

        return TrimPolyline(poly, boundaries, pickPoint, tol);
    }

    #endregion

    #region EXTEND

    /// <summary>
    /// Computes the EXTEND operation on an entity given boundary entities and the click pick point.
    /// The endpoint closest to the pickPoint is chosen for extension.
    /// </summary>
    public static ExtendResult? Extend(
        ICadEntity target,
        IEnumerable<ICadEntity> boundaries,
        Point2D pickPoint,
        GeometricTolerancePolicy? tol = null)
    {
        tol ??= GeometricTolerancePolicy.Default;

        return target switch
        {
            CadLine line => ExtendLine(line, boundaries, pickPoint, tol),
            CadArc arc => ExtendArc(arc, boundaries, pickPoint, tol),
            _ => null
        };
    }

    /// <summary>
    /// Computes the EXTEND operation on a line targeting an explicit endpoint (START or END).
    /// Used by console and MCP commands.
    /// </summary>
    public static ExtendResult? ExtendEndpoint(
        ICadEntity target,
        IEnumerable<ICadEntity> boundaries,
        bool extendStart,
        GeometricTolerancePolicy? tol = null)
    {
        tol ??= GeometricTolerancePolicy.Default;

        if (target is CadLine line)
        {
            return ExtendLineExplicit(line, boundaries, extendStart, tol);
        }
        else if (target is CadArc arc)
        {
            return ExtendArcExplicit(arc, boundaries, extendStart, tol);
        }

        return null;
    }

    private static ExtendResult? ExtendLine(
        CadLine line,
        IEnumerable<ICadEntity> boundaries,
        Point2D pickPoint,
        GeometricTolerancePolicy tol)
    {
        var distStart = pickPoint.DistanceTo(line.StartPoint);
        var distEnd = pickPoint.DistanceTo(line.EndPoint);
        bool extendStart = distStart < distEnd;

        return ExtendLineExplicit(line, boundaries, extendStart, tol);
    }

    private static ExtendResult? ExtendLineExplicit(
        CadLine line,
        IEnumerable<ICadEntity> boundaries,
        bool extendStart,
        GeometricTolerancePolicy tol)
    {
        var extPoint = extendStart ? line.StartPoint : line.EndPoint;
        var basePoint = extendStart ? line.EndPoint : line.StartPoint;

        var dx = extPoint.X - basePoint.X;
        var dy = extPoint.Y - basePoint.Y;
        var len = Math.Sqrt((dx * dx) + (dy * dy));
        if (len < tol.MinEntityDimension) return null;

        var ux = dx / len;
        var uy = dy / len;
        var rayDir = new Point2D(ux, uy);

        // Find closest forward intersection with boundaries
        double minDistance = double.MaxValue;
        Point2D? closestIntersection = null;

        foreach (var boundary in boundaries)
        {
            if (boundary.Id == line.Id) continue;

            var candidates = IntersectRayWithEntity(extPoint, rayDir, boundary, tol);
            foreach (var (candPt, dist) in candidates)
            {
                if (dist > tol.PointCoincidenceTolerance && dist < minDistance)
                {
                    minDistance = dist;
                    closestIntersection = candPt;
                }
            }
        }

        if (closestIntersection == null) return null;

        // Disallow zero-length extensions or already touching
        if (minDistance < 0.01) return null;

        var newStart = extendStart ? closestIntersection.Value : line.StartPoint;
        var newEnd = extendStart ? line.EndPoint : closestIntersection.Value;

        var extendedLine = new CadLine(newStart, newEnd, line.Layer, line.ColorHex)
        {
            Id = line.Id,
            LineThickness = line.LineThickness
        };

        return new ExtendResult(line, extendedLine, closestIntersection.Value, extendStart);
    }

    private static ExtendResult? ExtendArc(
        CadArc arc,
        IEnumerable<ICadEntity> boundaries,
        Point2D pickPoint,
        GeometricTolerancePolicy tol)
    {
        var distStart = pickPoint.DistanceTo(arc.StartPoint);
        var distEnd = pickPoint.DistanceTo(arc.EndPoint);
        bool extendStart = distStart < distEnd;

        return ExtendArcExplicit(arc, boundaries, extendStart, tol);
    }

    private static ExtendResult? ExtendArcExplicit(
        CadArc arc,
        IEnumerable<ICadEntity> boundaries,
        bool extendStart,
        GeometricTolerancePolicy tol)
    {
        // Full circle circle-entity intersections
        var circle = new CadCircle(arc.Center, arc.Radius, arc.Layer, arc.ColorHex);
        var intersections = FindIntersections(circle, boundaries, tol);
        if (intersections.Count == 0) return null;

        double bestAngleDiff = double.MaxValue;
        Point2D? bestIntersection = null;
        double bestNewAngle = 0;

        foreach (var pt in intersections)
        {
            var rad = Math.Atan2(pt.Y - arc.Center.Y, pt.X - arc.Center.X);
            var deg = (rad * 180.0 / Math.PI + 360.0) % 360.0;

            if (extendStart)
            {
                // Extending start goes clockwise (decreasing angle)
                var diff = (arc.StartAngleDegrees - deg + 360.0) % 360.0;
                if (diff > 0.05 && diff < 359.0 && diff < bestAngleDiff)
                {
                    bestAngleDiff = diff;
                    bestIntersection = pt;
                    bestNewAngle = deg;
                }
            }
            else
            {
                // Extending end goes counterclockwise (increasing angle)
                var diff = (deg - arc.EndAngleDegrees + 360.0) % 360.0;
                if (diff > 0.05 && diff < 359.0 && diff < bestAngleDiff)
                {
                    bestAngleDiff = diff;
                    bestIntersection = pt;
                    bestNewAngle = deg;
                }
            }
        }

        if (bestIntersection == null) return null;

        var newStartAngle = extendStart ? bestNewAngle : arc.StartAngleDegrees;
        var newEndAngle = extendStart ? arc.EndAngleDegrees : bestNewAngle;

        var extendedArc = new CadArc(arc.Center, arc.Radius, newStartAngle, newEndAngle, arc.Layer, arc.ColorHex)
        {
            Id = arc.Id,
            LineThickness = arc.LineThickness
        };

        return new ExtendResult(arc, extendedArc, bestIntersection.Value, extendStart);
    }

    #endregion

    #region Ray-Entity Intersections

    public static List<(Point2D Point, double Distance)> IntersectRayWithEntity(
        Point2D rayOrigin,
        Point2D rayDir,
        ICadEntity boundary,
        GeometricTolerancePolicy tol)
    {
        var results = new List<(Point2D Point, double Distance)>();

        var segments = CadIntersectionEngine.GetEntitySegments(boundary);
        if (segments.Count > 0)
        {
            foreach (var seg in segments)
            {
                if (IntersectRaySegment(rayOrigin, rayDir, seg.Start, seg.End, tol, out var pt, out var dist))
                {
                    results.Add((pt, dist));
                }
            }
            return results;
        }

        if (boundary is CadCircle circle)
        {
            foreach (var (pt, dist) in IntersectRayCircle(rayOrigin, rayDir, circle.Center, circle.Radius, tol))
            {
                results.Add((pt, dist));
            }
            return results;
        }

        if (boundary is CadArc arc)
        {
            foreach (var (pt, dist) in IntersectRayCircle(rayOrigin, rayDir, arc.Center, arc.Radius, tol))
            {
                if (CadIntersectionEngine.IsAngleOnArc(pt, arc, tol))
                {
                    results.Add((pt, dist));
                }
            }
            return results;
        }

        return results;
    }

    private static bool IntersectRaySegment(
        Point2D rayOrigin,
        Point2D rayDir,
        Point2D segA,
        Point2D segB,
        GeometricTolerancePolicy tol,
        out Point2D intersection,
        out double distance)
    {
        intersection = default;
        distance = 0;

        var vSegX = segB.X - segA.X;
        var vSegY = segB.Y - segA.Y;
        var lenSeg = Math.Sqrt((vSegX * vSegX) + (vSegY * vSegY));
        if (lenSeg < tol.PointCoincidenceTolerance) return false;

        var det = (rayDir.X * vSegY) - (rayDir.Y * vSegX);
        if (Math.Abs(det) < tol.MathEpsilon) return false; // Parallel

        var dx = segA.X - rayOrigin.X;
        var dy = segA.Y - rayOrigin.Y;

        var t = ((dx * vSegY) - (dy * vSegX)) / det;
        var s = ((dx * rayDir.Y) - (dy * rayDir.X)) / det;

        var epsS = tol.OnCurveTolerance / lenSeg;
        if (s >= -epsS && s <= 1.0 + epsS && t > tol.PointCoincidenceTolerance)
        {
            distance = t;
            intersection = new Point2D(rayOrigin.X + (t * rayDir.X), rayOrigin.Y + (t * rayDir.Y));
            return true;
        }

        return false;
    }

    private static List<(Point2D Point, double Distance)> IntersectRayCircle(
        Point2D rayOrigin,
        Point2D rayDir,
        Point2D center,
        double radius,
        GeometricTolerancePolicy tol)
    {
        var results = new List<(Point2D Point, double Distance)>();
        if (radius < tol.MinEntityDimension) return results;

        var fx = rayOrigin.X - center.X;
        var fy = rayOrigin.Y - center.Y;

        // rayDir is unit length
        var b = 2.0 * ((fx * rayDir.X) + (fy * rayDir.Y));
        var c = (fx * fx) + (fy * fy) - (radius * radius);

        var discriminant = (b * b) - (4.0 * c);
        if (discriminant < -tol.MathEpsilon) return results;

        if (Math.Abs(discriminant) <= tol.MathEpsilon)
        {
            var t = -b / 2.0;
            if (t > tol.PointCoincidenceTolerance)
            {
                var pt = new Point2D(rayOrigin.X + (t * rayDir.X), rayOrigin.Y + (t * rayDir.Y));
                results.Add((pt, t));
            }
            return results;
        }

        var sqrtDisc = Math.Sqrt(Math.Max(0.0, discriminant));
        var t1 = (-b - sqrtDisc) / 2.0;
        var t2 = (-b + sqrtDisc) / 2.0;

        if (t1 > tol.PointCoincidenceTolerance)
        {
            results.Add((new Point2D(rayOrigin.X + (t1 * rayDir.X), rayOrigin.Y + (t1 * rayDir.Y)), t1));
        }
        if (t2 > tol.PointCoincidenceTolerance && Math.Abs(t2 - t1) > tol.PointCoincidenceTolerance)
        {
            results.Add((new Point2D(rayOrigin.X + (t2 * rayDir.X), rayOrigin.Y + (t2 * rayDir.Y)), t2));
        }

        results.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        return results;
    }

    #endregion

    #region Sector Math Helpers

    private static double NormalizeDeltaAngle(double deg)
    {
        var m = deg % 360.0;
        if (m > 180.0) m -= 360.0;
        if (m < -180.0) m += 360.0;
        return m;
    }

    private static bool IsAngleInSector(double angle, double start, double end)
    {
        angle = (angle % 360.0 + 360.0) % 360.0;
        start = (start % 360.0 + 360.0) % 360.0;
        end = (end % 360.0 + 360.0) % 360.0;

        if (start <= end)
            return angle >= start - 1e-9 && angle <= end + 1e-9;
        return angle >= start - 1e-9 || angle <= end + 1e-9;
    }

    #endregion
}

using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;

namespace DotNetCad.Core.Selection;

public enum SelectionMode
{
    Window,
    Crossing,
    Point
}

public static class CadSelectionEngine
{
    public static ICadEntity? HitTestEntity(ICadEntity entity, Point2D pt, double toleranceMeters = 0.30)
    {
        if (entity is CadLine line)
        {
            var seg = new LineSegment2D(line.StartPoint, line.EndPoint);
            if (seg.DistanceTo(pt) <= toleranceMeters) return entity;
        }
        else if (entity is CadPolyline poly)
        {
            for (int i = 0; i < poly.Vertices.Count - 1; i++)
            {
                var seg = new LineSegment2D(poly.Vertices[i], poly.Vertices[i + 1]);
                if (seg.DistanceTo(pt) <= toleranceMeters) return entity;
            }
            if (poly.IsClosed && poly.Vertices.Count >= 3)
            {
                var segLast = new LineSegment2D(poly.Vertices[^1], poly.Vertices[0]);
                if (segLast.DistanceTo(pt) <= toleranceMeters) return entity;
                // Hollow polyline: do NOT select solely because point is in interior
            }
        }
        else if (entity is CadRectangle rect)
        {
            var vertices = rect.GetVertices();
            for (int i = 0; i < vertices.Count; i++)
            {
                var seg = new LineSegment2D(vertices[i], vertices[(i + 1) % vertices.Count]);
                if (seg.DistanceTo(pt) <= toleranceMeters) return entity;
            }
            // Hollow rectangle: do NOT select solely because point is in interior
        }
        else if (entity is CadCircle circle)
        {
            var distToCenter = circle.Center.DistanceTo(pt);
            // Hollow circle: ONLY match the visible perimeter contour within tolerance
            if (Math.Abs(distToCenter - circle.Radius) <= toleranceMeters)
                return entity;
        }
        else if (entity is CadArc arc)
        {
            var distToCenter = arc.Center.DistanceTo(pt);
            if (Math.Abs(distToCenter - arc.Radius) <= toleranceMeters)
            {
                var angleRad = Math.Atan2(pt.Y - arc.Center.Y, pt.X - arc.Center.X);
                var angleDeg = (angleRad * 180.0 / Math.PI + 360.0) % 360.0;
                if (IsAngleBetween(angleDeg, arc.StartAngleDegrees, arc.EndAngleDegrees))
                    return entity;
            }
        }
        else if (entity is CadDimension dim)
        {
            var (d1, d2) = dim.GetDimensionLinePoints();
            var dimSeg = new LineSegment2D(d1, d2);
            var ext1 = new LineSegment2D(dim.StartPoint, d1);
            var ext2 = new LineSegment2D(dim.EndPoint, d2);
            var baseline = new LineSegment2D(dim.StartPoint, dim.EndPoint);
            if (dimSeg.DistanceTo(pt) <= toleranceMeters ||
                ext1.DistanceTo(pt) <= toleranceMeters ||
                ext2.DistanceTo(pt) <= toleranceMeters ||
                baseline.DistanceTo(pt) <= toleranceMeters ||
                dim.OffsetPoint.DistanceTo(pt) <= toleranceMeters)
                return entity;
        }
        else if (entity is CadText label)
        {
            var box = label.BoundingBox;
            var expanded = new BoundingBox2D(box.MinX - toleranceMeters, box.MinY - toleranceMeters,
                box.MaxX + toleranceMeters, box.MaxY + toleranceMeters);
            if (expanded.Contains(pt)) return entity;
        }

        return null;
    }

    public static List<ICadEntity> HitTestAllEntities(IEnumerable<ICadEntity> entities, Point2D pt, double toleranceMeters = 0.30)
    {
        var hits = new List<ICadEntity>();
        foreach (var entity in entities.Reverse())
        {
            var hit = HitTestEntity(entity, pt, toleranceMeters);
            if (hit != null) hits.Add(hit);
        }
        return hits;
    }

    public static ICadEntity? SelectByPoint(IEnumerable<ICadEntity> entities, Point2D clickPt, double toleranceMeters = 0.30, int cycleIndex = 0)
    {
        var hits = HitTestAllEntities(entities, clickPt, toleranceMeters);
        if (hits.Count == 0) return null;
        var idx = Math.Abs(cycleIndex) % hits.Count;
        return hits[idx];
    }

    public static List<ICadEntity> SelectByBox(
        IEnumerable<ICadEntity> entities,
        Point2D p1,
        Point2D p2,
        SelectionMode? forcedMode = null)
    {
        var box = BoundingBox2D.FromCorners(p1, p2);
        bool isWindow = forcedMode.HasValue
            ? forcedMode.Value == SelectionMode.Window
            : p2.X >= p1.X; // Left-to-right is Window; right-to-left is Crossing

        var selected = new List<ICadEntity>();

        foreach (var entity in entities)
        {
            var entityBbox = entity.BoundingBox;

            if (isWindow)
            {
                // Window: entirely contained
                if (box.Contains(entityBbox))
                {
                    selected.Add(entity);
                }
            }
            else
            {
                // Crossing: intersects or contains
                if (box.Contains(entityBbox) || BoxIntersectsEntity(box, entity))
                {
                    selected.Add(entity);
                }
            }
        }

        return selected;
    }

    public static bool BoxIntersectsEntity(BoundingBox2D box, ICadEntity entity)
    {
        var eBox = entity.BoundingBox;
        if (!box.Intersects(eBox)) return false;
        if (box.Contains(eBox)) return true;

        var corners = new[]
        {
            new Point2D(box.MinX, box.MinY),
            new Point2D(box.MaxX, box.MinY),
            new Point2D(box.MaxX, box.MaxY),
            new Point2D(box.MinX, box.MaxY)
        };
        var boxSegments = new[]
        {
            new LineSegment2D(corners[0], corners[1]),
            new LineSegment2D(corners[1], corners[2]),
            new LineSegment2D(corners[2], corners[3]),
            new LineSegment2D(corners[3], corners[0])
        };

        if (entity is CadLine line)
        {
            if (box.Contains(line.StartPoint) || box.Contains(line.EndPoint)) return true;
            var lineSeg = new LineSegment2D(line.StartPoint, line.EndPoint);
            return boxSegments.Any(bs => bs.Intersects(lineSeg));
        }

        if (entity is CadRectangle rect)
        {
            var v = rect.GetVertices();
            if (v.Any(box.Contains)) return true;
            for (int i = 0; i < v.Count; i++)
            {
                var rSeg = new LineSegment2D(v[i], v[(i + 1) % v.Count]);
                if (boxSegments.Any(bs => bs.Intersects(rSeg))) return true;
            }
            return false;
        }

        if (entity is CadPolyline poly)
        {
            if (poly.Vertices.Any(box.Contains)) return true;
            for (int i = 0; i < poly.Vertices.Count - 1; i++)
            {
                var pSeg = new LineSegment2D(poly.Vertices[i], poly.Vertices[i + 1]);
                if (boxSegments.Any(bs => bs.Intersects(pSeg))) return true;
            }
            if (poly.IsClosed && poly.Vertices.Count >= 3)
            {
                var pSeg = new LineSegment2D(poly.Vertices[^1], poly.Vertices[0]);
                if (boxSegments.Any(bs => bs.Intersects(pSeg))) return true;
            }
            return false;
        }

        if (entity is CadCircle circle)
        {
            if (box.Contains(circle.Center)) return true;
            return boxSegments.Any(bs => bs.DistanceTo(circle.Center) <= circle.Radius);
        }

        return box.Intersects(eBox);
    }

    private static bool IsAngleBetween(double angle, double start, double end)
    {
        angle = (angle % 360 + 360) % 360;
        start = (start % 360 + 360) % 360;
        end = (end % 360 + 360) % 360;

        if (start <= end)
            return angle >= start && angle <= end;
        return angle >= start || angle <= end;
    }
}

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
                if (PolygonMath.IsPointInsidePolygon(pt, poly.Vertices)) return entity;
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
            if (PolygonMath.IsPointInsidePolygon(pt, vertices)) return entity;
        }
        else if (entity is CadCircle circle)
        {
            var distToCenter = circle.Center.DistanceTo(pt);
            if (Math.Abs(distToCenter - circle.Radius) <= toleranceMeters || distToCenter <= circle.Radius)
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

        return null;
    }

    public static ICadEntity? SelectByPoint(IEnumerable<ICadEntity> entities, Point2D clickPt, double toleranceMeters = 0.30)
    {
        foreach (var entity in entities.Reverse())
        {
            var hit = HitTestEntity(entity, clickPt, toleranceMeters);
            if (hit != null) return hit;
        }
        return null;
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
                if (box.Intersects(entityBbox))
                {
                    selected.Add(entity);
                }
            }
        }

        return selected;
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

using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;

namespace DotNetCad.Core.Snapping;

public static class CadSnapEngine
{
    public static double ConvertScreenPixelsToMeters(double screenPixels, double zoom)
    {
        var safeZoom = Math.Max(0.001, zoom);
        return screenPixels / safeZoom;
    }

    public static Point2D SnapToGrid(Point2D point, double gridIntervalMeters = 0.50)
    {
        if (gridIntervalMeters <= 0) return point;
        var x = Math.Round(point.X / gridIntervalMeters) * gridIntervalMeters;
        var y = Math.Round(point.Y / gridIntervalMeters) * gridIntervalMeters;
        return new Point2D(x, y);
    }

    public static Point2D SnapToPoints(Point2D point, IEnumerable<Point2D> snapTargets, double toleranceMeters = 0.30)
    {
        var bestPoint = point;
        var minDistance = toleranceMeters;

        foreach (var target in snapTargets)
        {
            var d = point.DistanceTo(target);
            if (d < minDistance)
            {
                minDistance = d;
                bestPoint = target;
            }
        }

        return bestPoint;
    }

    public static PrecisionSnapResult ResolvePrecisionPoint(
        Point2D mousePoint,
        IEnumerable<ICadEntity>? entities,
        CadPreferences prefs,
        double objectSnapToleranceMeters,
        Point2D? trackingOrigin = null)
    {
        if (prefs.IsOsnapEnabled && entities != null)
        {
            var objectSnap = FindOsnap(mousePoint, entities, prefs, objectSnapToleranceMeters);
            if (objectSnap != null)
            {
                return new PrecisionSnapResult(objectSnap.Point, objectSnap, null, false);
            }
        }

        var gridPoint = (prefs.IsGridSnapEnabled && prefs.GridIntervalMeters > 0)
            ? SnapToGrid(mousePoint, prefs.GridIntervalMeters)
            : mousePoint;
        var isGridSnapped = prefs.IsGridSnapEnabled && prefs.GridIntervalMeters > 0 && gridPoint.DistanceTo(mousePoint) > 1e-9;

        if (trackingOrigin is not Point2D origin)
        {
            return new PrecisionSnapResult(gridPoint, null, null, isGridSnapped);
        }

        var tracking = SnapOrthoAndPolar(origin, gridPoint, prefs.IsOrthoModeEnabled, prefs.IsPolarTrackingEnabled, prefs.PolarAngleIncrementDegrees);
        return new PrecisionSnapResult(tracking.Point, null, tracking, isGridSnapped);
    }

    public static OsnapResult? FindOsnap(
        Point2D mousePt,
        IEnumerable<ICadEntity> entities,
        CadPreferences prefs,
        double toleranceMeters = 0.60)
    {
        if (!prefs.IsOsnapEnabled) return null;

        OsnapResult? bestResult = null;
        double minDistance = toleranceMeters;

        void TestCandidate(Point2D pt, OsnapType type, string description)
        {
            var d = mousePt.DistanceTo(pt);
            if (d < minDistance)
            {
                minDistance = d;
                bestResult = new OsnapResult { Point = pt, Type = type, Description = description };
            }
        }

        var entityList = entities as IList<ICadEntity> ?? entities.ToList();

        foreach (var entity in entityList)
        {
            if (entity is CadLine line)
            {
                if (prefs.IsOsnapEndpointEnabled)
                {
                    TestCandidate(line.StartPoint, OsnapType.Endpoint, "Endpoint");
                    TestCandidate(line.EndPoint, OsnapType.Endpoint, "Endpoint");
                }
                if (prefs.IsOsnapMidpointEnabled)
                {
                    TestCandidate(line.Midpoint, OsnapType.Midpoint, "Midpoint");
                }
            }
            else if (entity is CadPolyline poly)
            {
                for (int i = 0; i < poly.Vertices.Count; i++)
                {
                    var v = poly.Vertices[i];
                    if (prefs.IsOsnapEndpointEnabled)
                    {
                        TestCandidate(v, OsnapType.Endpoint, "Vertex");
                    }

                    if (prefs.IsOsnapMidpointEnabled && (i < poly.Vertices.Count - 1 || poly.IsClosed))
                    {
                        var nextV = poly.Vertices[(i + 1) % poly.Vertices.Count];
                        var mid = new Point2D((v.X + nextV.X) / 2.0, (v.Y + nextV.Y) / 2.0);
                        TestCandidate(mid, OsnapType.Midpoint, "Midpoint");
                    }
                }
            }
            else if (entity is CadRectangle rect)
            {
                var corners = rect.GetVertices();
                if (prefs.IsOsnapEndpointEnabled)
                {
                    foreach (var c in corners) TestCandidate(c, OsnapType.Endpoint, "Corner");
                }
                if (prefs.IsOsnapMidpointEnabled)
                {
                    for (int i = 0; i < corners.Count; i++)
                    {
                        var p1 = corners[i];
                        var p2 = corners[(i + 1) % corners.Count];
                        TestCandidate(new Point2D((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0), OsnapType.Midpoint, "Midpoint");
                    }
                }
                if (prefs.IsOsnapCenterEnabled)
                {
                    var center = PolygonMath.CalculateCentroid(corners);
                    TestCandidate(center, OsnapType.Center, "Center");
                }
            }
            else if (entity is CadCircle circle)
            {
                if (prefs.IsOsnapCenterEnabled)
                {
                    TestCandidate(circle.Center, OsnapType.Center, "Center");
                }
                if (prefs.IsOsnapEndpointEnabled)
                {
                    // Quadrant points
                    TestCandidate(new Point2D(circle.Center.X + circle.Radius, circle.Center.Y), OsnapType.Endpoint, "Quadrant East");
                    TestCandidate(new Point2D(circle.Center.X, circle.Center.Y + circle.Radius), OsnapType.Endpoint, "Quadrant North");
                    TestCandidate(new Point2D(circle.Center.X - circle.Radius, circle.Center.Y), OsnapType.Endpoint, "Quadrant West");
                    TestCandidate(new Point2D(circle.Center.X, circle.Center.Y - circle.Radius), OsnapType.Endpoint, "Quadrant South");
                }
            }
            else if (entity is CadArc arc)
            {
                if (prefs.IsOsnapCenterEnabled)
                {
                    TestCandidate(arc.Center, OsnapType.Center, "Center");
                }
                if (prefs.IsOsnapEndpointEnabled)
                {
                    TestCandidate(arc.StartPoint, OsnapType.Endpoint, "Arc Start");
                    TestCandidate(arc.EndPoint, OsnapType.Endpoint, "Arc End");
                }
                if (prefs.IsOsnapMidpointEnabled)
                {
                    TestCandidate(arc.Midpoint, OsnapType.Midpoint, "Arc Midpoint");
                }
            }
            else if (entity is CadDimension dim)
            {
                if (prefs.IsOsnapEndpointEnabled)
                {
                    TestCandidate(dim.StartPoint, OsnapType.Endpoint, "Dimension Origin");
                    TestCandidate(dim.EndPoint, OsnapType.Endpoint, "Dimension Target");
                }
            }
        }

        return bestResult;
    }

    public static Point2D SnapOrtho(Point2D origin, Point2D current)
    {
        var dx = Math.Abs(current.X - origin.X);
        var dy = Math.Abs(current.Y - origin.Y);

        return dx >= dy ? new Point2D(current.X, origin.Y) : new Point2D(origin.X, current.Y);
    }

    public static PolarSnapResult SnapOrthoAndPolar(
        Point2D origin,
        Point2D current,
        bool isOrtho,
        bool isPolar,
        double polarIncrementDegrees = 45.0,
        double toleranceDegrees = 4.0)
    {
        var dx = current.X - origin.X;
        var dy = current.Y - origin.Y;
        var dist = Math.Sqrt((dx * dx) + (dy * dy));

        if (dist < 1e-4)
        {
            return new PolarSnapResult(current, 0.0, false, false);
        }

        // 1. Ortho Mode (F8)
        if (isOrtho)
        {
            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                var snappedX = new Point2D(current.X, origin.Y);
                var angle = dx >= 0 ? 0.0 : 180.0;
                return new PolarSnapResult(snappedX, angle, true, false);
            }
            else
            {
                var snappedY = new Point2D(origin.X, current.Y);
                var angle = dy >= 0 ? 90.0 : 270.0;
                return new PolarSnapResult(snappedY, angle, true, false);
            }
        }

        // 2. Polar Tracking Mode (F10)
        if (isPolar && polarIncrementDegrees > 0)
        {
            var rawAngleRad = Math.Atan2(dy, dx);
            var rawAngleDeg = rawAngleRad * 180.0 / Math.PI;
            if (rawAngleDeg < 0) rawAngleDeg += 360.0;

            var snappedAngleDeg = Math.Round(rawAngleDeg / polarIncrementDegrees) * polarIncrementDegrees;
            if (snappedAngleDeg >= 360.0) snappedAngleDeg -= 360.0;

            var angleDiff = Math.Abs(rawAngleDeg - snappedAngleDeg);
            if (angleDiff > 180.0) angleDiff = 360.0 - angleDiff;

            if (angleDiff <= toleranceDegrees)
            {
                var rad = snappedAngleDeg * Math.PI / 180.0;
                var snappedPoint = new Point2D(
                    origin.X + (dist * Math.Cos(rad)),
                    origin.Y + (dist * Math.Sin(rad)));

                return new PolarSnapResult(snappedPoint, snappedAngleDeg, false, true);
            }
        }

        var generalAngle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        if (generalAngle < 0) generalAngle += 360.0;
        return new PolarSnapResult(current, generalAngle, false, false);
    }
}

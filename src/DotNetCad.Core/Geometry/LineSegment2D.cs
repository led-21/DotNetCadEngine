namespace DotNetCad.Core.Geometry;

public readonly record struct LineSegment2D(Point2D Start, Point2D End)
{
    public double Length => Start.DistanceTo(End);
    public double LengthSquared => Start.DistanceToSquared(End);
    public Point2D Midpoint => new((Start.X + End.X) / 2.0, (Start.Y + End.Y) / 2.0);
    public Vector2D Direction => Start.VectorTo(End).Normalize();
    public BoundingBox2D BoundingBox => BoundingBox2D.FromCorners(Start, End);

    public Point2D ClosestPointTo(Point2D pt)
    {
        var dx = End.X - Start.X;
        var dy = End.Y - Start.Y;
        var lenSq = (dx * dx) + (dy * dy);
        if (lenSq < 1e-12) return Start;

        var t = Math.Clamp((((pt.X - Start.X) * dx) + ((pt.Y - Start.Y) * dy)) / lenSq, 0.0, 1.0);
        return new Point2D(Start.X + (t * dx), Start.Y + (t * dy));
    }

    public double DistanceTo(Point2D pt)
    {
        return pt.DistanceTo(ClosestPointTo(pt));
    }

    public bool Intersects(LineSegment2D other)
    {
        return SegmentsIntersect(Start, End, other.Start, other.End);
    }

    private static bool SegmentsIntersect(Point2D a, Point2D b, Point2D c, Point2D d)
    {
        static double Ccw(Point2D p1, Point2D p2, Point2D p3) =>
            ((p2.X - p1.X) * (p3.Y - p1.Y)) - ((p2.Y - p1.Y) * (p3.X - p1.X));

        var ccw1 = Ccw(a, b, c);
        var ccw2 = Ccw(a, b, d);
        var ccw3 = Ccw(c, d, a);
        var ccw4 = Ccw(c, d, b);

        if (((ccw1 > 0 && ccw2 < 0) || (ccw1 < 0 && ccw2 > 0)) &&
            ((ccw3 > 0 && ccw4 < 0) || (ccw3 < 0 && ccw4 > 0)))
            return true;

        static bool OnSegment(Point2D p, Point2D q, Point2D r) =>
            q.X <= Math.Max(p.X, r.X) + 1e-9 && q.X >= Math.Min(p.X, r.X) - 1e-9 &&
            q.Y <= Math.Max(p.Y, r.Y) + 1e-9 && q.Y >= Math.Min(p.Y, r.Y) - 1e-9;

        if (Math.Abs(ccw1) < 1e-9 && OnSegment(a, c, b)) return true;
        if (Math.Abs(ccw2) < 1e-9 && OnSegment(a, d, b)) return true;
        if (Math.Abs(ccw3) < 1e-9 && OnSegment(c, a, d)) return true;
        if (Math.Abs(ccw4) < 1e-9 && OnSegment(c, b, d)) return true;

        return false;
    }
}

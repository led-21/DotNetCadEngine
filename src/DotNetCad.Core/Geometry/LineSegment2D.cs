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
}

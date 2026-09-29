namespace DotNetCad.Core.Geometry;

public readonly record struct BoundingBox2D(double MinX, double MinY, double MaxX, double MaxY)
{
    public static BoundingBox2D Empty => new(double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity);

    public bool IsEmpty => MinX > MaxX || MinY > MaxY;
    public double Width => IsEmpty ? 0.0 : Math.Max(0.0, MaxX - MinX);
    public double Height => IsEmpty ? 0.0 : Math.Max(0.0, MaxY - MinY);
    public Point2D Center => IsEmpty ? Point2D.Zero : new((MinX + MaxX) / 2.0, (MinY + MaxY) / 2.0);

    public bool Contains(Point2D pt)
    {
        return pt.X >= MinX && pt.X <= MaxX && pt.Y >= MinY && pt.Y <= MaxY;
    }

    public bool Contains(BoundingBox2D other)
    {
        if (other.IsEmpty) return false;
        return other.MinX >= MinX && other.MaxX <= MaxX &&
               other.MinY >= MinY && other.MaxY <= MaxY;
    }

    public bool Intersects(BoundingBox2D other)
    {
        if (IsEmpty || other.IsEmpty) return false;
        return !(other.MinX > MaxX || other.MaxX < MinX ||
                 other.MinY > MaxY || other.MaxY < MinY);
    }

    public BoundingBox2D Expand(Point2D pt)
    {
        return new BoundingBox2D(
            Math.Min(MinX, pt.X),
            Math.Min(MinY, pt.Y),
            Math.Max(MaxX, pt.X),
            Math.Max(MaxY, pt.Y));
    }

    public BoundingBox2D Expand(BoundingBox2D other)
    {
        if (other.IsEmpty) return this;
        if (IsEmpty) return other;

        return new BoundingBox2D(
            Math.Min(MinX, other.MinX),
            Math.Min(MinY, other.MinY),
            Math.Max(MaxX, other.MaxX),
            Math.Max(MaxY, other.MaxY));
    }

    public static BoundingBox2D FromPoints(IEnumerable<Point2D> points)
    {
        double minX = double.PositiveInfinity;
        double minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity;
        double maxY = double.NegativeInfinity;
        bool any = false;

        foreach (var p in points)
        {
            any = true;
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }

        return any ? new BoundingBox2D(minX, minY, maxX, maxY) : Empty;
    }

    public static BoundingBox2D FromCorners(Point2D p1, Point2D p2)
    {
        return new BoundingBox2D(
            Math.Min(p1.X, p2.X),
            Math.Min(p1.Y, p2.Y),
            Math.Max(p1.X, p2.X),
            Math.Max(p1.Y, p2.Y));
    }
}

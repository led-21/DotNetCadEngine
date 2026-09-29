namespace DotNetCad.Core.Geometry;

public readonly record struct Point2D(double X, double Y)
{
    public static Point2D Zero => new(0, 0);

    public double DistanceTo(Point2D other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    public double DistanceToSquared(Point2D other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return (dx * dx) + (dy * dy);
    }

    public static Point2D operator +(Point2D a, Point2D b) => new(a.X + b.X, a.Y + b.Y);
    public static Point2D operator -(Point2D a, Point2D b) => new(a.X - b.X, a.Y - b.Y);
    public Vector2D VectorTo(Point2D other) => new(other.X - X, other.Y - Y);
    public static Point2D operator -(Point2D a, Vector2D v) => new(a.X - v.X, a.Y - v.Y);
    public static Point2D operator *(Point2D a, double scalar) => new(a.X * scalar, a.Y * scalar);
    public static Point2D operator /(Point2D a, double scalar) => new(a.X / scalar, a.Y / scalar);

    public override string ToString() => $"({X:F3}, {Y:F3})";
}

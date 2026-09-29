namespace DotNetCad.Core.Geometry;

public readonly record struct Vector2D(double X, double Y)
{
    public static Vector2D Zero => new(0, 0);
    public static Vector2D UnitX => new(1, 0);
    public static Vector2D UnitY => new(0, 1);

    public double Length => Math.Sqrt((X * X) + (Y * Y));
    public double LengthSquared => (X * X) + (Y * Y);

    public double AngleRadians => Math.Atan2(Y, X);
    public double AngleDegrees
    {
        get
        {
            var deg = AngleRadians * 180.0 / Math.PI;
            return deg < 0 ? deg + 360.0 : deg;
        }
    }

    public Vector2D Normalize()
    {
        var len = Length;
        return len < 1e-12 ? Zero : new Vector2D(X / len, Y / len);
    }

    public double Dot(Vector2D other) => (X * other.X) + (Y * other.Y);
    public double Cross(Vector2D other) => (X * other.Y) - (Y * other.X);

    public static Vector2D operator +(Vector2D a, Vector2D b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2D operator -(Vector2D a, Vector2D b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2D operator -(Vector2D v) => new(-v.X, -v.Y);
    public static Vector2D operator *(Vector2D v, double scalar) => new(v.X * scalar, v.Y * scalar);
    public static Vector2D operator *(double scalar, Vector2D v) => new(v.X * scalar, v.Y * scalar);
    public static Vector2D operator /(Vector2D v, double scalar) => new(v.X / scalar, v.Y / scalar);

    public static implicit operator Vector2D(Point2D p) => new(p.X, p.Y);
    public static implicit operator Point2D(Vector2D v) => new(v.X, v.Y);

    public override string ToString() => $"Vector2D({X:F3}, {Y:F3})";
}

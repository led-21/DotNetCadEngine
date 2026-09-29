using DotNetCad.Core.Geometry;

namespace DotNetCad.Core.Snapping;

public enum OsnapType
{
    Endpoint,
    Midpoint,
    Center,
    Intersection,
    Perpendicular
}

public sealed class OsnapResult
{
    public Point2D Point { get; set; }
    public OsnapType Type { get; set; }
    public string Description { get; set; } = string.Empty;
}

public readonly record struct PolarSnapResult(
    Point2D Point,
    double AngleDegrees,
    bool IsOrthoSnapped,
    bool IsPolarSnapped);

public readonly record struct PrecisionSnapResult(
    Point2D Point,
    OsnapResult? ObjectSnap,
    PolarSnapResult? TrackingSnap,
    bool IsGridSnapped)
{
    public bool IsSnapped => ObjectSnap != null || IsGridSnapped ||
                             TrackingSnap?.IsOrthoSnapped == true || TrackingSnap?.IsPolarSnapped == true;
}

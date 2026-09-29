using DotNetCad.Core.Geometry;

namespace DotNetCad.Core.Measurements;

public readonly record struct CadDistanceMeasurement(
    double LengthMeters,
    double DeltaX,
    double DeltaY,
    double AzimuthDegrees,
    double AngleDegrees);

public readonly record struct CadPolygonMeasurement(
    double AreaSquareMeters,
    double PerimeterMeters,
    Point2D Centroid);

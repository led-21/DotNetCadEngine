using DotNetCad.Core.Measurements;

namespace DotNetCad.Core.Geometry;

/// <summary>
/// Defines numerical tolerances and geometric thresholds for 2D CAD operations.
/// Supports scale-adaptive tolerances across millimeter, centimeter, meter, and large civil scales.
/// </summary>
public sealed record GeometricTolerancePolicy
{
    /// <summary>Numerical epsilon for collinearity, parallelism, and determinants (dimensionless/relative).</summary>
    public double MathEpsilon { get; init; } = 1e-9;

    /// <summary>Minimum distance between distinct points in drawing units.</summary>
    public double PointCoincidenceTolerance { get; init; } = 1e-5;

    /// <summary>Tolerance for point-on-curve / point-on-segment test (orthogonal projection distance).</summary>
    public double OnCurveTolerance { get; init; } = 1e-4;

    /// <summary>Minimum segment length or feature dimension in drawing units.</summary>
    public double MinEntityDimension { get; init; } = 1e-3;

    /// <summary>Minimum area for closed polygons in square drawing units.</summary>
    public double MinPolygonArea { get; init; } = 1e-4;

    /// <summary>Minimum sweep angle for circular arcs in degrees.</summary>
    public double MinArcSweepDegrees { get; init; } = 0.05;

    /// <summary>Default tolerance policy (standard meter scale).</summary>
    public static GeometricTolerancePolicy Default { get; } = new();

    /// <summary>
    /// Creates a tolerance policy adapted to the specified drawing measurement unit.
    /// </summary>
    public static GeometricTolerancePolicy ForUnit(CadLinearUnit unit) => unit switch
    {
        CadLinearUnit.Millimeter => new()
        {
            MathEpsilon = 1e-9,
            PointCoincidenceTolerance = 1e-3, // 0.001 mm
            OnCurveTolerance = 1e-2,          // 0.01 mm
            MinEntityDimension = 0.1,         // 0.1 mm
            MinPolygonArea = 0.01,
            MinArcSweepDegrees = 0.05
        },
        CadLinearUnit.Centimeter => new()
        {
            MathEpsilon = 1e-9,
            PointCoincidenceTolerance = 1e-4, // 0.0001 cm
            OnCurveTolerance = 1e-3,          // 0.001 cm
            MinEntityDimension = 0.01,        // 0.01 cm
            MinPolygonArea = 1e-4,
            MinArcSweepDegrees = 0.05
        },
        CadLinearUnit.Meter or _ => Default
    };
}

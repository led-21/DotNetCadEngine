using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Core.Measurements;
using Xunit;

namespace DotNetCad.Core.Tests;

public sealed class GeometryScaleAndPrecisionTests
{
    [Fact]
    public void IsPointOnSegment_ProjectionEliminatesTriangleInequalityErrorsNearEndpoints()
    {
        // Segment from (0, 0) to (10, 0)
        var p1 = new Point2D(0, 0);
        var p2 = new Point2D(10, 0);

        // Point extremely close to endpoint (1e-5 away from start along line)
        var nearStart = new Point2D(1e-5, 0);
        Assert.True(CadConstructionMath.IsPointOnSegment(nearStart, p1, p2, tolerance: 1e-4));

        // Point slightly off-axis perpendicular within tolerance
        var slightlyOff = new Point2D(5.0, 5e-5);
        Assert.True(CadConstructionMath.IsPointOnSegment(slightlyOff, p1, p2, tolerance: 1e-4));

        // Point off-axis beyond tolerance
        var beyondTol = new Point2D(5.0, 2e-4);
        Assert.False(CadConstructionMath.IsPointOnSegment(beyondTol, p1, p2, tolerance: 1e-4));

        // Point outside the segment bounds along the line
        var outsidePastEnd = new Point2D(10.05, 0);
        Assert.False(CadConstructionMath.IsPointOnSegment(outsidePastEnd, p1, p2, tolerance: 1e-3));
    }

    [Fact]
    public void MultiScale_LineLineIntersection_MicroScale()
    {
        // Millimeter/micro scale: 0.001 units
        var tol = GeometricTolerancePolicy.ForUnit(CadLinearUnit.Millimeter);
        var a1 = new Point2D(0.001, 0.000);
        var a2 = new Point2D(0.005, 0.004);
        var b1 = new Point2D(0.001, 0.004);
        var b2 = new Point2D(0.005, 0.000);

        var inter = CadIntersectionEngine.IntersectSegmentSegment(a1, a2, b1, b2, tol);
        Assert.NotNull(inter);
        Assert.Equal(0.003, inter!.Value.X, 6);
        Assert.Equal(0.002, inter!.Value.Y, 6);
    }

    [Fact]
    public void MultiScale_LineLineIntersection_GeodeticScale()
    {
        // Geodetic / civil scale: coordinates in 100,000s
        var a1 = new Point2D(100_000.0, 200_000.0);
        var a2 = new Point2D(110_000.0, 210_000.0);
        var b1 = new Point2D(100_000.0, 210_000.0);
        var b2 = new Point2D(110_000.0, 200_000.0);

        var inter = CadIntersectionEngine.IntersectSegmentSegment(a1, a2, b1, b2);
        Assert.NotNull(inter);
        Assert.Equal(105_000.0, inter!.Value.X, 4);
        Assert.Equal(205_000.0, inter!.Value.Y, 4);
    }

    [Fact]
    public void MultiScale_LineCircleIntersection_ExactTangency()
    {
        // Circle centered at (0, 5) with radius 5
        // Horizontal line at y = 10 from (-10, 10) to (10, 10) -> exactly tangent at (0, 10)
        var p1 = new Point2D(-10, 10);
        var p2 = new Point2D(10, 10);
        var center = new Point2D(0, 5);
        double radius = 5.0;

        var pts = CadIntersectionEngine.IntersectLineCircle(p1, p2, center, radius, segmentOnly: true);
        Assert.Single(pts);
        Assert.Equal(0.0, pts[0].X, 5);
        Assert.Equal(10.0, pts[0].Y, 5);
    }

    [Fact]
    public void LineArcIntersection_SectorFilteringAndDiscontinuity()
    {
        // Arc centered at (0, 0), radius 10, sweep from 315° to 45° (crosses 0°)
        var arc = new CadArc(new(0, 0), 10.0, 315.0, 45.0);

        // Vertical line at x = 7.0710678 (cos(45°)*10)
        var p1 = new Point2D(7.0710678, -15);
        var p2 = new Point2D(7.0710678, 15);

        var pts = CadIntersectionEngine.IntersectLineArc(p1, p2, arc, segmentOnly: true);
        // Intersects at +45° and -45° (315°), both within the arc sweep
        Assert.Equal(2, pts.Count);

        // Line on the opposite side at x = -7.0710678 (135° and 225°, outside arc)
        var q1 = new Point2D(-7.0710678, -15);
        var q2 = new Point2D(-7.0710678, 15);
        var outsidePts = CadIntersectionEngine.IntersectLineArc(q1, q2, arc, segmentOnly: true);
        Assert.Empty(outsidePts);
    }

    [Fact]
    public void CircleCircleIntersection_TwoPointsAndTangency()
    {
        // Two intersecting circles: (0, 0) r=5 and (6, 0) r=5
        var c1 = new Point2D(0, 0);
        var c2 = new Point2D(6, 0);
        var pts = CadIntersectionEngine.IntersectCircleCircle(c1, 5.0, c2, 5.0);
        Assert.Equal(2, pts.Count);
        Assert.Equal(3.0, pts[0].X, 4);
        Assert.Equal(3.0, pts[1].X, 4);
        Assert.Equal(4.0, Math.Abs(pts[0].Y), 4);
        Assert.Equal(4.0, Math.Abs(pts[1].Y), 4);

        // Tangent circles: (0, 0) r=5 and (10, 0) r=5
        var cTangent = new Point2D(10, 0);
        var tangentPts = CadIntersectionEngine.IntersectCircleCircle(c1, 5.0, cTangent, 5.0);
        Assert.Single(tangentPts);
        Assert.Equal(5.0, tangentPts[0].X, 4);
        Assert.Equal(0.0, tangentPts[0].Y, 4);
    }

    [Fact]
    public void OffsetPolyline_OpenPolylineMiter()
    {
        // Open polyline: (0, 0) -> (10, 0) -> (10, 10)
        var vertices = new List<Point2D> { new(0, 0), new(10, 0), new(10, 10) };
        var offset = CadConstructionMath.OffsetPolyline(vertices, isClosed: false, distance: 2.0);

        Assert.Equal(3, offset.Count);
        // Start vertex shifted left (upwards by 2): (0, 2)
        Assert.Equal(0.0, offset[0].X, 4);
        Assert.Equal(2.0, offset[0].Y, 4);

        // Mitered corner intersection of y=2 and x=8: (8, 2)
        Assert.Equal(8.0, offset[1].X, 4);
        Assert.Equal(2.0, offset[1].Y, 4);

        // End vertex: (8, 10)
        Assert.Equal(8.0, offset[2].X, 4);
        Assert.Equal(10.0, offset[2].Y, 4);
    }

    [Fact]
    public void OffsetPolyline_ConcaveLShapedPolygon()
    {
        // L-shaped polygon (concave inner corner at 5, 5)
        var vertices = new List<Point2D>
        {
            new(0, 0),
            new(10, 0),
            new(10, 5),
            new(5, 5),
            new(5, 10),
            new(0, 10)
        };

        // Outward offset by 1.0
        var outward = CadConstructionMath.OffsetPolyline(vertices, isClosed: true, distance: 1.0);
        Assert.Equal(6, outward.Count);

        // Outward min X should be -1 and max Y should be 11
        Assert.Equal(-1.0, outward.Min(p => p.X), 4);
        Assert.Equal(11.0, outward.Max(p => p.Y), 4);

        // Inward offset by -0.5
        var inward = CadConstructionMath.OffsetPolyline(vertices, isClosed: true, distance: -0.5);
        Assert.Equal(6, inward.Count);
        Assert.Equal(0.5, inward.Min(p => p.X), 4);
        Assert.Equal(9.5, inward.Max(p => p.Y), 4);
    }

    [Fact]
    public void PolymorphicTrim_SegmentAgainstCircleBoundary()
    {
        // Line passing through circle: from (-10, 0) to (10, 0)
        // Circle at (0, 0) with radius 5
        var p1 = new Point2D(-10, 0);
        var p2 = new Point2D(10, 0);
        var circle = new CadCircle(new(0, 0), 5.0);

        // Click near (-8, 0) to trim the left extension outside circle
        var click = new Point2D(-8, 0);
        var trimmed = CadConstructionMath.TrimSegmentWithEntity(p1, p2, circle, click);

        Assert.NotNull(trimmed);
        // The nearest intersection to click is (-5, 0).
        // Since pick (-8) < intersection (-5), the left side is cut: segment becomes (-5, 0) to (10, 0)
        Assert.Equal(-5.0, trimmed!.Value.Start.X, 4);
        Assert.Equal(0.0, trimmed.Value.Start.Y, 4);
        Assert.Equal(10.0, trimmed.Value.End.X, 4);
        Assert.Equal(0.0, trimmed.Value.End.Y, 4);
    }

    [Fact]
    public void PolymorphicExtend_SegmentTowardsArcBoundary()
    {
        // Line from (0, 0) pointing towards (0, 4)
        var p1 = new Point2D(0, 0);
        var p2 = new Point2D(0, 4);

        // Arc centered at (0, 0), radius 10, sweep from 60° to 120° (includes 90° on +Y axis)
        var arc = new CadArc(new(0, 0), 10.0, 60.0, 120.0);

        var extended = CadConstructionMath.ExtendSegmentToEntity(p1, p2, arc);
        Assert.NotNull(extended);
        Assert.Equal(0.0, extended!.Value.Start.X, 4);
        Assert.Equal(0.0, extended.Value.Start.Y, 4);
        Assert.Equal(0.0, extended.Value.End.X, 4);
        Assert.Equal(10.0, extended.Value.End.Y, 4);
    }

    [Fact]
    public void PolymorphicExtend_SegmentTowardsRectangleBoundary()
    {
        // Line from (0, 5) pointing towards (3, 5)
        var p1 = new Point2D(0, 5);
        var p2 = new Point2D(3, 5);

        // Rectangle with Origin (10, 0), Width 10, Height 10 (Left edge is x=10)
        var rect = new CadRectangle(new(10, 0), 10, 10);

        var extended = CadConstructionMath.ExtendSegmentToEntity(p1, p2, rect);
        Assert.NotNull(extended);
        Assert.Equal(0.0, extended!.Value.Start.X, 4);
        Assert.Equal(5.0, extended.Value.Start.Y, 4);
        Assert.Equal(10.0, extended.Value.End.X, 4);
        Assert.Equal(5.0, extended.Value.End.Y, 4);
    }

    [Fact]
    public void GeometryValidator_RespectsTolerancePolicyAcrossUnits_AndValidatesArcs()
    {
        var mmPolicy = GeometricTolerancePolicy.ForUnit(CadLinearUnit.Millimeter);
        var cmPolicy = GeometricTolerancePolicy.ForUnit(CadLinearUnit.Centimeter);

        // Segment of 0.02 units: valid in centimeters (MinEntityDimension = 0.01), invalid in millimeters (MinEntityDimension = 0.1)
        var p1 = new Point2D(0, 0);
        var p2 = new Point2D(0.02, 0);
        Assert.True(GeometryValidator.ValidateSegment(p1, p2, cmPolicy).IsValid);
        Assert.False(GeometryValidator.ValidateSegment(p1, p2, mmPolicy).IsValid);

        // Arc validation: valid sweep vs degenerate 0° and 360° sweeps
        Assert.True(GeometryValidator.ValidateArc(new(0, 0), 2.0, 15.0, 135.0, cmPolicy).IsValid);
        Assert.False(GeometryValidator.ValidateArc(new(0, 0), 2.0, 45.0, 45.01, cmPolicy).IsValid);
        Assert.False(GeometryValidator.ValidateArc(new(0, 0), 2.0, 0.0, 360.0, cmPolicy).IsValid);
    }
}

using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using Xunit;

namespace DotNetCad.Core.Tests;

public class CadTrimExtendEngineTests
{
    private static readonly GeometricTolerancePolicy Tol = GeometricTolerancePolicy.Default;

    #region TRIM Tests

    [Fact]
    public void Trim_TwoPerpendicularLines_TrimsCorrectEnd()
    {
        // Target: (0, 5) to (10, 5)
        // Boundary: (5, 0) to (5, 10)
        // Pick near start (1, 5) -> should trim off [0..5], leaving [5..10]
        var target = new CadLine(new(0, 5), new(10, 5), "TestLayer", "#FF0000") { LineThickness = 2.0 };
        var boundary = new CadLine(new(5, 0), new(5, 10));

        var result = CadTrimExtendEngine.Trim(target, [boundary], new(1, 5), Tol);

        Assert.NotNull(result);
        var remaining = Assert.Single(result.RemainingEntities);
        var remLine = Assert.IsType<CadLine>(remaining);
        Assert.Equal(5.0, remLine.StartPoint.X, 3);
        Assert.Equal(10.0, remLine.EndPoint.X, 3);
        Assert.Equal(target.Id, remLine.Id);
        Assert.Equal("TestLayer", remLine.Layer);
        Assert.Equal("#FF0000", remLine.ColorHex);
        Assert.Equal(2.0, remLine.LineThickness);

        var discarded = Assert.IsType<CadLine>(result.DiscardedSegment);
        Assert.Equal(0.0, discarded.StartPoint.X, 3);
        Assert.Equal(5.0, discarded.EndPoint.X, 3);
    }

    [Fact]
    public void Trim_TwoPerpendicularLines_TrimsNearEnd()
    {
        // Pick near end (9, 5) -> should trim off [5..10], leaving [0..5]
        var target = new CadLine(new(0, 5), new(10, 5));
        var boundary = new CadLine(new(5, 0), new(5, 10));

        var result = CadTrimExtendEngine.Trim(target, [boundary], new(9, 5), Tol);

        Assert.NotNull(result);
        var remaining = Assert.Single(result.RemainingEntities);
        var remLine = Assert.IsType<CadLine>(remaining);
        Assert.Equal(0.0, remLine.StartPoint.X, 3);
        Assert.Equal(5.0, remLine.EndPoint.X, 3);
    }

    [Fact]
    public void Trim_DiagonalLines_CalculatesCorrectIntersectionAndTrims()
    {
        // Target: (0, 0) to (10, 10)
        // Boundary: (0, 10) to (10, 0) -> intersection at (5, 5)
        var target = new CadLine(new(0, 0), new(10, 10));
        var boundary = new CadLine(new(0, 10), new(10, 0));

        var result = CadTrimExtendEngine.Trim(target, [boundary], new(2, 2), Tol);

        Assert.NotNull(result);
        var remaining = Assert.Single(result.RemainingEntities);
        var remLine = Assert.IsType<CadLine>(remaining);
        Assert.Equal(5.0, remLine.StartPoint.X, 3);
        Assert.Equal(5.0, remLine.StartPoint.Y, 3);
        Assert.Equal(10.0, remLine.EndPoint.X, 3);
        Assert.Equal(10.0, remLine.EndPoint.Y, 3);
    }

    [Fact]
    public void Trim_MultipleIntersections_MiddleSegmentRemovalSplitsLine()
    {
        // Target: (0, 0) to (12, 0)
        // Boundaries: at X=4 and X=8
        // Pick at X=6 (middle) -> leaves [0..4] and [8..12]
        var target = new CadLine(new(0, 0), new(12, 0));
        var b1 = new CadLine(new(4, -2), new(4, 2));
        var b2 = new CadLine(new(8, -2), new(8, 2));

        var result = CadTrimExtendEngine.Trim(target, [b1, b2], new(6, 0), Tol);

        Assert.NotNull(result);
        Assert.Equal(2, result.RemainingEntities.Count);

        var first = Assert.IsType<CadLine>(result.RemainingEntities[0]);
        var second = Assert.IsType<CadLine>(result.RemainingEntities[1]);

        Assert.Equal(0.0, first.StartPoint.X, 3);
        Assert.Equal(4.0, first.EndPoint.X, 3);
        Assert.Equal(target.Id, first.Id); // ID policy: first segment preserves original ID

        Assert.Equal(8.0, second.StartPoint.X, 3);
        Assert.Equal(12.0, second.EndPoint.X, 3);
        Assert.NotEqual(target.Id, second.Id); // ID policy: second segment gets new GUID
    }

    [Fact]
    public void Trim_ParallelLines_ReturnsNull()
    {
        var target = new CadLine(new(0, 0), new(10, 0));
        var boundary = new CadLine(new(0, 2), new(10, 2));

        var result = CadTrimExtendEngine.Trim(target, [boundary], new(5, 0), Tol);
        Assert.Null(result);
    }

    [Fact]
    public void Trim_CoincidentLines_ReturnsNull()
    {
        var target = new CadLine(new(0, 0), new(10, 0));
        var boundary = new CadLine(new(2, 0), new(8, 0));

        var result = CadTrimExtendEngine.Trim(target, [boundary], new(5, 0), Tol);
        Assert.Null(result);
    }

    [Fact]
    public void Trim_IntersectionOutsideSegments_ReturnsNull()
    {
        // Lines would cross if infinite, but segments do not touch
        var target = new CadLine(new(0, 0), new(5, 0));
        var boundary = new CadLine(new(10, -5), new(10, 5));

        var result = CadTrimExtendEngine.Trim(target, [boundary], new(2, 0), Tol);
        Assert.Null(result);
    }

    [Fact]
    public void Trim_NegativeCoordinates_TrimsProperly()
    {
        // Target: (-10, -5) to (-2, -5)
        // Boundary: (-6, -10) to (-6, 0) -> intersection at (-6, -5)
        // Pick near start (-8, -5) -> leaves [-6..-2]
        var target = new CadLine(new(-10, -5), new(-2, -5));
        var boundary = new CadLine(new(-6, -10), new(-6, 0));

        var result = CadTrimExtendEngine.Trim(target, [boundary], new(-8, -5), Tol);

        Assert.NotNull(result);
        var remaining = Assert.Single(result.RemainingEntities);
        var remLine = Assert.IsType<CadLine>(remaining);
        Assert.Equal(-6.0, remLine.StartPoint.X, 3);
        Assert.Equal(-2.0, remLine.EndPoint.X, 3);
    }

    [Fact]
    public void Trim_VerySmallRemainingSegment_AvoidsDegenerateSegment()
    {
        // Intersection is very close to end (e.g. 0.0001 from end)
        var target = new CadLine(new(0, 0), new(1, 0));
        var boundary = new CadLine(new(0.99995, -1), new(0.99995, 1));

        // Picking the main body leaves an endpoint piece smaller than MinEntityDimension (1e-3)
        // Should avoid creating degenerate segment
        var result = CadTrimExtendEngine.Trim(target, [boundary], new(0.5, 0), Tol);
        Assert.Null(result);
    }

    [Fact]
    public void Trim_Circle_TransformsIntoArc()
    {
        // Circle centered at (0, 0) with radius 5
        // Cutting line along Y axis: from (0, -10) to (0, 10)
        // Intersections at (0, -5) [270 deg] and (0, 5) [90 deg]
        // Pick on right side (5, 0) [0 deg] -> cuts sector [270..90], leaves left arc [90..270]
        var circle = new CadCircle(new(0, 0), 5, "Geometry", "#38BDF8") { LineThickness = 1.8 };
        var cutter = new CadLine(new(0, -10), new(0, 10));

        var result = CadTrimExtendEngine.Trim(circle, [cutter], new(5, 0), Tol);

        Assert.NotNull(result);
        var remaining = Assert.Single(result.RemainingEntities);
        var arc = Assert.IsType<CadArc>(remaining);

        Assert.Equal(0.0, arc.Center.X, 3);
        Assert.Equal(0.0, arc.Center.Y, 3);
        Assert.Equal(5.0, arc.Radius, 3);
        Assert.Equal(90.0, arc.StartAngleDegrees, 1);
        Assert.Equal(270.0, arc.EndAngleDegrees, 1);
        Assert.Equal(circle.Id, arc.Id);
        Assert.Equal(circle.Layer, arc.Layer);
        Assert.Equal(circle.ColorHex, arc.ColorHex);
        Assert.Equal(1.8, arc.LineThickness);
    }

    [Fact]
    public void Trim_Arc_TrimsIntoSubArc()
    {
        // Arc from 0 to 180 degrees, radius 5
        // Cutting line at X=0 (Y from -1 to 10) -> intersection at (0, 5) [90 deg]
        // Pick at (4, 3) [~36 deg] -> cuts [0..90], leaves [90..180]
        var arc = new CadArc(new(0, 0), 5, 0, 180, "Arcs", "#00FF00");
        var cutter = new CadLine(new(0, 0), new(0, 10));

        var result = CadTrimExtendEngine.Trim(arc, [cutter], new(4, 3), Tol);

        Assert.NotNull(result);
        var remaining = Assert.Single(result.RemainingEntities);
        var remArc = Assert.IsType<CadArc>(remaining);
        Assert.Equal(90.0, remArc.StartAngleDegrees, 1);
        Assert.Equal(180.0, remArc.EndAngleDegrees, 1);
    }

    [Fact]
    public void Trim_Polyline_ShortensSegment()
    {
        // Polyline: (0, 0) -> (5, 0) -> (5, 5)
        // Cutting line across first segment at X=2: (2, -2) to (2, 2)
        // Pick near start (1, 0) -> shortens polyline to (2, 0) -> (5, 0) -> (5, 5)
        var poly = new CadPolyline([new(0, 0), new(5, 0), new(5, 5)]);
        var cutter = new CadLine(new(2, -2), new(2, 2));

        var result = CadTrimExtendEngine.Trim(poly, [cutter], new(1, 0), Tol);

        Assert.NotNull(result);
        var remaining = Assert.Single(result.RemainingEntities);
        var remPoly = Assert.IsType<CadPolyline>(remaining);
        Assert.Equal(3, remPoly.Vertices.Count);
        Assert.Equal(2.0, remPoly.Vertices[0].X, 3);
        Assert.Equal(0.0, remPoly.Vertices[0].Y, 3);
        Assert.Equal(5.0, remPoly.Vertices[1].X, 3);
        Assert.Equal(5.0, remPoly.Vertices[2].Y, 3);
    }

    #endregion

    #region EXTEND Tests

    [Fact]
    public void Extend_ByStartEndpoint_ExtendsToBoundary()
    {
        // Target: (5, 0) to (10, 0)
        // Boundary at X=0: (0, -5) to (0, 5)
        // Pick near start (5.5, 0) -> StartPoint extends from (5, 0) to (0, 0)
        var target = new CadLine(new(5, 0), new(10, 0), "Lines", "#123456") { LineThickness = 2.5 };
        var boundary = new CadLine(new(0, -5), new(0, 5));

        var result = CadTrimExtendEngine.Extend(target, [boundary], new(5.5, 0), Tol);

        Assert.NotNull(result);
        Assert.True(result.ExtendedStart);
        var extLine = Assert.IsType<CadLine>(result.ExtendedEntity);
        Assert.Equal(0.0, extLine.StartPoint.X, 3);
        Assert.Equal(0.0, extLine.StartPoint.Y, 3);
        Assert.Equal(10.0, extLine.EndPoint.X, 3);
        Assert.Equal(target.Id, extLine.Id);
        Assert.Equal("Lines", extLine.Layer);
        Assert.Equal("#123456", extLine.ColorHex);
        Assert.Equal(2.5, extLine.LineThickness);
    }

    [Fact]
    public void Extend_ByEndEndpoint_ExtendsToBoundary()
    {
        // Target: (0, 0) to (5, 0)
        // Boundary at X=8: (8, -5) to (8, 5)
        // Pick near end (4.5, 0) -> EndPoint extends from (5, 0) to (8, 0)
        var target = new CadLine(new(0, 0), new(5, 0));
        var boundary = new CadLine(new(8, -5), new(8, 5));

        var result = CadTrimExtendEngine.Extend(target, [boundary], new(4.5, 0), Tol);

        Assert.NotNull(result);
        Assert.False(result.ExtendedStart);
        var extLine = Assert.IsType<CadLine>(result.ExtendedEntity);
        Assert.Equal(0.0, extLine.StartPoint.X, 3);
        Assert.Equal(8.0, extLine.EndPoint.X, 3);
    }

    [Fact]
    public void Extend_ToSlantedBoundary_CalculatesExactIntersection()
    {
        // Target: horizontal line (0, 2) to (3, 2)
        // Slanted boundary: (0, 6) to (6, 0) -> equation Y = 6 - X. At Y=2, X=4.
        var target = new CadLine(new(0, 2), new(3, 2));
        var boundary = new CadLine(new(0, 6), new(6, 0));

        var result = CadTrimExtendEngine.Extend(target, [boundary], new(2.5, 2), Tol);

        Assert.NotNull(result);
        var extLine = Assert.IsType<CadLine>(result.ExtendedEntity);
        Assert.Equal(0.0, extLine.StartPoint.X, 3);
        Assert.Equal(4.0, extLine.EndPoint.X, 3);
        Assert.Equal(2.0, extLine.EndPoint.Y, 3);
    }

    [Fact]
    public void Extend_MultipleBoundaries_ChoosesNearestForwardBoundary()
    {
        // Target: (0, 0) to (2, 0)
        // Boundary 1: at X=5
        // Boundary 2: at X=10
        // Target should extend to X=5, not X=10
        var target = new CadLine(new(0, 0), new(2, 0));
        var b1 = new CadLine(new(5, -2), new(5, 2));
        var b2 = new CadLine(new(10, -2), new(10, 2));

        var result = CadTrimExtendEngine.Extend(target, [b2, b1], new(1.8, 0), Tol);

        Assert.NotNull(result);
        var extLine = Assert.IsType<CadLine>(result.ExtendedEntity);
        Assert.Equal(5.0, extLine.EndPoint.X, 3);
    }

    [Fact]
    public void Extend_NoIntersectionAhead_ReturnsNull()
    {
        // Target is pointing in +X direction, but boundary is behind at X=-5
        var target = new CadLine(new(0, 0), new(5, 0));
        var boundary = new CadLine(new(-5, -5), new(-5, 5));

        // Click near end (extending +X direction): no boundary ahead
        var result = CadTrimExtendEngine.Extend(target, [boundary], new(4.5, 0), Tol);
        Assert.Null(result);
    }

    [Fact]
    public void Extend_ParallelBoundary_ReturnsNull()
    {
        var target = new CadLine(new(0, 0), new(5, 0));
        var boundary = new CadLine(new(0, 2), new(10, 2));

        var result = CadTrimExtendEngine.Extend(target, [boundary], new(4, 0), Tol);
        Assert.Null(result);
    }

    [Fact]
    public void Extend_AlreadyTouchingBoundary_ReturnsNull()
    {
        var target = new CadLine(new(0, 0), new(5, 0));
        var boundary = new CadLine(new(5, -5), new(5, 5));

        var result = CadTrimExtendEngine.Extend(target, [boundary], new(4.5, 0), Tol);
        Assert.Null(result);
    }

    [Fact]
    public void Extend_Arc_ExtendsToBoundary()
    {
        // Arc from 0 to 90 degrees, radius 5 (ends at (0, 5))
        // Boundary at Y=7: horizontal line (-5, 7) to (5, 7)
        // Extending end CCW reaches intersection with Y=7
        // Circle x^2 + y^2 = 25 at y=7 has no real intersection.
        // Let's use boundary at X=-3 (vertical line from (-3, 0) to (-3, 10))
        // Circle x^2 + y^2 = 25 at x=-3 has y=4 (angle ~126.87 deg).
        var arc = new CadArc(new(0, 0), 5, 0, 90);
        var boundary = new CadLine(new(-3, 0), new(-3, 10));

        var result = CadTrimExtendEngine.Extend(arc, [boundary], new(0.5, 4.8), Tol);

        Assert.NotNull(result);
        var extArc = Assert.IsType<CadArc>(result.ExtendedEntity);
        Assert.Equal(0.0, extArc.StartAngleDegrees, 1);
        Assert.True(extArc.EndAngleDegrees > 90.0);
        Assert.Equal(126.87, extArc.EndAngleDegrees, 1);
    }

    #endregion
}

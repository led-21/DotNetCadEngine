using DotNetCad.Core.Geometry;
using Xunit;

namespace DotNetCad.Core.Tests;

public class CadConstructionMathTests
{
    [Fact]
    public void OffsetSegment_CalculatesParallelLineAtExactDistance()
    {
        var p1 = new Point2D(0, 0);
        var p2 = new Point2D(10, 0);
        double dist = 2.0;

        var (op1, op2) = CadConstructionMath.OffsetSegment(p1, p2, dist, toLeft: true);
        Assert.Equal(0.0, op1.X, precision: 4);
        Assert.Equal(2.0, op1.Y, precision: 4);
        Assert.Equal(10.0, op2.X, precision: 4);
        Assert.Equal(2.0, op2.Y, precision: 4);

        var (rp1, rp2) = CadConstructionMath.OffsetSegment(p1, p2, dist, toLeft: false);
        Assert.Equal(-2.0, rp1.Y, precision: 4);
        Assert.Equal(-2.0, rp2.Y, precision: 4);
    }

    [Fact]
    public void OffsetPolygon_ExpandsRectangleEquidistantly()
    {
        var rect = PolygonMath.CreateRectangle(new Point2D(0, 0), 10.0, 5.0);
        var offsetRect = CadConstructionMath.OffsetPolygon(rect, 1.0);

        Assert.Equal(4, offsetRect.Count);
        var originalArea = PolygonMath.CalculatePolygonArea(rect);
        var offsetArea = PolygonMath.CalculatePolygonArea(offsetRect);

        Assert.True(offsetArea > originalArea);
    }

    [Fact]
    public void FindLineIntersection_CalculatesIntersectionPoint()
    {
        var a1 = new Point2D(0, 5);
        var a2 = new Point2D(10, 5);
        var b1 = new Point2D(5, 0);
        var b2 = new Point2D(5, 10);

        var inter = CadConstructionMath.FindLineIntersection(a1, a2, b1, b2);
        Assert.NotNull(inter);
        Assert.Equal(5.0, inter.Value.X, precision: 4);
        Assert.Equal(5.0, inter.Value.Y, precision: 4);
    }

    [Fact]
    public void ExtendSegment_ExtendsLineToBoundary()
    {
        var p1 = new Point2D(0, 0);
        var p2 = new Point2D(5, 0);

        var boundA = new Point2D(8, -5);
        var boundB = new Point2D(8, 5);

        var extended = CadConstructionMath.ExtendSegment(p1, p2, boundA, boundB);
        Assert.NotNull(extended);
        Assert.Equal(0.0, extended.Value.Start.X, precision: 4);
        Assert.Equal(8.0, extended.Value.End.X, precision: 4);
    }

    [Fact]
    public void TrimSegment_TrimsLineAtIntersection()
    {
        var p1 = new Point2D(0, 0);
        var p2 = new Point2D(10, 0);

        var cutA = new Point2D(4, -5);
        var cutB = new Point2D(4, 5);

        var clickNearStart = new Point2D(1, 0);
        var trimmed = CadConstructionMath.TrimSegment(p1, p2, cutA, cutB, clickNearStart);

        Assert.NotNull(trimmed);
        Assert.Equal(4.0, trimmed.Value.Start.X, precision: 4);
        Assert.Equal(10.0, trimmed.Value.End.X, precision: 4);
    }

    [Fact]
    public void CreateFillet_CalculatesFilletCenterAndTangentPoints()
    {
        var a1 = new Point2D(0, 10);
        var a2 = new Point2D(0, 0);
        var b1 = new Point2D(0, 0);
        var b2 = new Point2D(10, 0);

        var fillet = CadConstructionMath.CreateFillet(a1, a2, b1, b2, radius: 2.0);
        Assert.NotNull(fillet);
        Assert.Equal(2.0, fillet.Value.Center.X, precision: 3);
        Assert.Equal(2.0, fillet.Value.Center.Y, precision: 3);
        Assert.Equal(2.0, fillet.Value.Center.DistanceTo(fillet.Value.TangentA), precision: 3);
        Assert.Equal(2.0, fillet.Value.Center.DistanceTo(fillet.Value.TangentB), precision: 3);
    }
}

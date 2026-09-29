using DotNetCad.Core.Geometry;
using Xunit;

namespace DotNetCad.Core.Tests;

public class GeometryMathTests
{
    [Fact]
    public void Point2D_DistanceAndOperators_WorkCorrectly()
    {
        var p1 = new Point2D(3, 4);
        var p2 = new Point2D(0, 0);

        Assert.Equal(5.0, p1.DistanceTo(p2), precision: 4);
        Assert.Equal(25.0, p1.DistanceToSquared(p2), precision: 4);

        var pSum = p1 + new Point2D(1, 2);
        Assert.Equal(new Point2D(4, 6), pSum);

        var pSub = p1 - new Point2D(1, 1);
        Assert.Equal(new Point2D(2, 3), pSub);

        var pScaled = p1 * 2.0;
        Assert.Equal(new Point2D(6, 8), pScaled);
    }

    [Fact]
    public void Vector2D_Calculations_AreAccurate()
    {
        var v = new Vector2D(3, 4);
        Assert.Equal(5.0, v.Length, precision: 4);

        var unit = v.Normalize();
        Assert.Equal(0.6, unit.X, precision: 4);
        Assert.Equal(0.8, unit.Y, precision: 4);
        Assert.Equal(1.0, unit.Length, precision: 4);

        var v1 = new Vector2D(1, 0);
        var v2 = new Vector2D(0, 1);
        Assert.Equal(0.0, v1.Dot(v2), precision: 4);
        Assert.Equal(1.0, v1.Cross(v2), precision: 4);
    }

    [Fact]
    public void BoundingBox2D_ContainsAndIntersects_CalculatesAccurately()
    {
        var bbox1 = new BoundingBox2D(0, 0, 10, 10);
        var bboxInside = new BoundingBox2D(2, 2, 8, 8);
        var bboxOverlap = new BoundingBox2D(5, 5, 15, 15);
        var bboxOutside = new BoundingBox2D(20, 20, 30, 30);

        Assert.True(bbox1.Contains(new Point2D(5, 5)));
        Assert.False(bbox1.Contains(new Point2D(15, 5)));

        Assert.True(bbox1.Contains(bboxInside));
        Assert.False(bbox1.Contains(bboxOverlap));

        Assert.True(bbox1.Intersects(bboxOverlap));
        Assert.False(bbox1.Intersects(bboxOutside));
    }

    [Fact]
    public void PolygonMath_CalculatePolygonArea_ReturnsAccurateShoelaceArea()
    {
        var rect = PolygonMath.CreateRectangle(new Point2D(0, 0), 10.0, 5.0);
        var area = PolygonMath.CalculatePolygonArea(rect);
        Assert.Equal(50.0, area, precision: 3);

        var perimeter = PolygonMath.CalculatePerimeter(rect);
        Assert.Equal(30.0, perimeter, precision: 3);

        var centroid = PolygonMath.CalculateCentroid(rect);
        Assert.Equal(5.0, centroid.X, precision: 3);
        Assert.Equal(2.5, centroid.Y, precision: 3);
    }

    [Fact]
    public void PolygonMath_IsPointInsidePolygon_RaycastingIdentifiesInAndOut()
    {
        var rect = PolygonMath.CreateRectangle(new Point2D(0, 0), 10.0, 10.0);
        Assert.True(PolygonMath.IsPointInsidePolygon(new Point2D(5, 5), rect));
        Assert.False(PolygonMath.IsPointInsidePolygon(new Point2D(15, 5), rect));
        Assert.False(PolygonMath.IsPointInsidePolygon(new Point2D(-1, 5), rect));
    }
}

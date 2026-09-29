using DotNetCad.Core.Geometry;
using Xunit;

namespace DotNetCad.Core.Tests;

public class GeometryValidatorTests
{
    [Fact]
    public void ValidatePoint_RejectsNaNAndInfinity()
    {
        var validPt = new Point2D(10.5, 20.3);
        Assert.True(GeometryValidator.ValidatePoint(validPt).IsValid);

        var nanPt = new Point2D(double.NaN, 5.0);
        var nanResult = GeometryValidator.ValidatePoint(nanPt);
        Assert.False(nanResult.IsValid);

        var infPt = new Point2D(10.0, double.PositiveInfinity);
        Assert.False(GeometryValidator.ValidatePoint(infPt).IsValid);
    }

    [Fact]
    public void ValidateSegment_RejectsDegenerateOrTooShortSegments()
    {
        var p1 = new Point2D(0, 0);
        var p2 = new Point2D(10, 0);
        Assert.True(GeometryValidator.ValidateSegment(p1, p2, 0.05).IsValid);

        var pSame = new Point2D(0.0001, 0.0001);
        var res = GeometryValidator.ValidateSegment(p1, pSame, 0.05);
        Assert.False(res.IsValid);
    }

    [Fact]
    public void ValidateDimensions_RejectsNonPositiveDimensions()
    {
        Assert.True(GeometryValidator.ValidateDimensions(10.0, 5.0, 0.10).IsValid);

        var invalidLength = GeometryValidator.ValidateDimensions(-1.0, 5.0, 0.10);
        Assert.False(invalidLength.IsValid);

        var invalidWidth = GeometryValidator.ValidateDimensions(10.0, 0.0, 0.10);
        Assert.False(invalidWidth.IsValid);
    }

    [Fact]
    public void ValidateCircle_RejectsNonPositiveRadius()
    {
        var center = new Point2D(5, 5);
        Assert.True(GeometryValidator.ValidateCircle(center, 3.5).IsValid);

        var zeroRadius = GeometryValidator.ValidateCircle(center, 0.0);
        Assert.False(zeroRadius.IsValid);
    }

    [Fact]
    public void ValidatePolygon_ValidatesSimplePolygon_AndRejectsSelfIntersectingHourglass()
    {
        var validRect = PolygonMath.CreateRectangle(new Point2D(0, 0), 10, 5);
        Assert.True(GeometryValidator.ValidatePolygon(validRect).IsValid);

        var tooFew = new List<Point2D> { new(0, 0), new(5, 0) };
        Assert.False(GeometryValidator.ValidatePolygon(tooFew).IsValid);

        // Self-intersecting polygon (hourglass/bow-tie)
        var selfIntersecting = new List<Point2D>
        {
            new(0, 0),
            new(10, 10),
            new(10, 0),
            new(0, 10)
        };
        var selfResult = GeometryValidator.ValidatePolygon(selfIntersecting);
        Assert.False(selfResult.IsValid);
    }
}

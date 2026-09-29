using DotNetCad.Core.Geometry;
using Xunit;

namespace DotNetCad.Core.Tests;

public class CadTransformMathTests
{
    [Fact]
    public void RotatePoint_Rotates90DegreesAroundOrigin_Correctly()
    {
        var origin = new Point2D(0, 0);
        var pt = new Point2D(10, 0);

        var rotated90 = CadTransformMath.RotatePoint(pt, origin, 90.0);
        Assert.Equal(0.0, rotated90.X, precision: 4);
        Assert.Equal(10.0, rotated90.Y, precision: 4);

        var rotated180 = CadTransformMath.RotatePoint(pt, origin, 180.0);
        Assert.Equal(-10.0, rotated180.X, precision: 4);
        Assert.Equal(0.0, rotated180.Y, precision: 4);
    }

    [Fact]
    public void MirrorPoint_MirrorsAcrossVerticalAxis_Correctly()
    {
        var axisP1 = new Point2D(5, 0);
        var axisP2 = new Point2D(5, 10);
        var pt = new Point2D(2, 4);

        // Distance from x=2 to axis x=5 is 3m -> mirrored point should be x=8, y=4
        var mirrored = CadTransformMath.MirrorPoint(pt, axisP1, axisP2);
        Assert.Equal(8.0, mirrored.X, precision: 4);
        Assert.Equal(4.0, mirrored.Y, precision: 4);
    }

    [Fact]
    public void MirrorPolygon_PreservesAreaAndMirrorsCoordinates()
    {
        var poly = new List<Point2D>
        {
            new(1, 1),
            new(4, 1),
            new(4, 3),
            new(1, 3)
        };
        var axisP1 = new Point2D(0, 0);
        var axisP2 = new Point2D(0, 10); // Y axis

        var mirrored = CadTransformMath.MirrorPolygon(poly, axisP1, axisP2);

        Assert.Equal(4, mirrored.Count);
        Assert.Equal(PolygonMath.CalculatePolygonArea(poly), PolygonMath.CalculatePolygonArea(mirrored), precision: 4);
        Assert.All(mirrored, v => Assert.True(v.X < 0));
    }
}

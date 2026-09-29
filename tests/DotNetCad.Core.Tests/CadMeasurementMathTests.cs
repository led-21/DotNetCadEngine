using DotNetCad.Core.Geometry;
using Xunit;

namespace DotNetCad.Core.Tests;

public class CadMeasurementMathTests
{
    [Fact]
    public void CadMeasurementMath_MeasureDistanceAndAzimuth_CalculatesAccurately()
    {
        var p1 = new Point2D(0, 0);
        var p2 = new Point2D(0, 10); // North direction (+Y)

        var measNorth = CadMeasurementMath.MeasureDistance(p1, p2);
        Assert.Equal(10.0, measNorth.LengthMeters, precision: 4);
        Assert.Equal(0.0, measNorth.DeltaX, precision: 4);
        Assert.Equal(10.0, measNorth.DeltaY, precision: 4);
        Assert.Equal(0.0, measNorth.AzimuthDegrees, precision: 1); // 0° = North

        var pEast = new Point2D(10, 0); // East direction (+X)
        var measEast = CadMeasurementMath.MeasureDistance(p1, pEast);
        Assert.Equal(10.0, measEast.LengthMeters, precision: 4);
        Assert.Equal(90.0, measEast.AzimuthDegrees, precision: 1); // 90° = East
    }

    [Fact]
    public void CadMeasurementMath_MeasureAngle_CalculatesDegreesBetweenVectors()
    {
        var vertex = new Point2D(0, 0);
        var p1 = new Point2D(10, 0);
        var p2 = new Point2D(0, 10);

        var angle90 = CadMeasurementMath.MeasureAngle(vertex, p1, p2);
        Assert.Equal(90.0, angle90, precision: 2);

        var p3 = new Point2D(10, 10);
        var angle45 = CadMeasurementMath.MeasureAngle(vertex, p1, p3);
        Assert.Equal(45.0, angle45, precision: 2);
    }

    [Fact]
    public void CadMeasurementMath_MeasurePolygon_CalculatesAreaAndPerimeter()
    {
        var rect = PolygonMath.CreateRectangle(new Point2D(0, 0), width: 20.0, height: 10.0);
        var meas = CadMeasurementMath.MeasurePolygon(rect);

        Assert.Equal(200.0, meas.AreaSquareMeters, precision: 3);
        Assert.Equal(60.0, meas.PerimeterMeters, precision: 3);
        Assert.Equal(10.0, meas.Centroid.X, precision: 2);
        Assert.Equal(5.0, meas.Centroid.Y, precision: 2);
    }
}

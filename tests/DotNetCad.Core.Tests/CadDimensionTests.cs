using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Core.Selection;
using Xunit;

namespace DotNetCad.Core.Tests;

public class CadDimensionTests
{
    [Fact]
    public void CalculateDimensionLinePoints_HorizontalLine_OffsetsCorrectly()
    {
        var start = new Point2D(0, 0);
        var end = new Point2D(10, 0);
        var offset = new Point2D(5, 3); // Depth of +3 above the line

        var (d1, d2) = CadDimension.CalculateDimensionLinePoints(start, end, offset);

        Assert.Equal(0, d1.X, precision: 4);
        Assert.Equal(3, d1.Y, precision: 4);
        Assert.Equal(10, d2.X, precision: 4);
        Assert.Equal(3, d2.Y, precision: 4);
    }

    [Fact]
    public void CalculateDimensionLinePoints_VerticalLine_OffsetsCorrectly()
    {
        var start = new Point2D(0, 0);
        var end = new Point2D(0, 10);
        var offset = new Point2D(-2, 5); // Depth of 2 units to the left

        var (d1, d2) = CadDimension.CalculateDimensionLinePoints(start, end, offset);

        Assert.Equal(-2, d1.X, precision: 4);
        Assert.Equal(0, d1.Y, precision: 4);
        Assert.Equal(-2, d2.X, precision: 4);
        Assert.Equal(10, d2.Y, precision: 4);
    }

    [Fact]
    public void CadDimension_GetDimensionLinePoints_MatchesInstanceProperties()
    {
        var dim = new CadDimension
        {
            StartPoint = new Point2D(-10, 5),
            EndPoint = new Point2D(10, 5),
            OffsetPoint = new Point2D(0, 7)
        };

        var (d1, d2) = dim.GetDimensionLinePoints();

        Assert.Equal(-10, d1.X, precision: 4);
        Assert.Equal(7, d1.Y, precision: 4);
        Assert.Equal(10, d2.X, precision: 4);
        Assert.Equal(7, d2.Y, precision: 4);
        Assert.Equal(20, dim.CalculatedLengthMeters, precision: 4);
    }

    [Fact]
    public void CadDimension_Selection_HitsOffsetDimensionLine()
    {
        var dim = new CadDimension
        {
            StartPoint = new Point2D(0, 0),
            EndPoint = new Point2D(10, 0),
            OffsetPoint = new Point2D(5, 4)
        };

        // Click directly on the offset dimension line at (5, 4)
        var hitOnDimLine = CadSelectionEngine.SelectByPoint([dim], new Point2D(5, 4), toleranceMeters: 0.2);
        Assert.NotNull(hitOnDimLine);

        // Click on extension line 1 at (0, 2)
        var hitOnExt1 = CadSelectionEngine.SelectByPoint([dim], new Point2D(0, 2), toleranceMeters: 0.2);
        Assert.NotNull(hitOnExt1);

        // Click on original measured baseline at (5, 0)
        var hitOnBase = CadSelectionEngine.SelectByPoint([dim], new Point2D(5, 0), toleranceMeters: 0.2);
        Assert.NotNull(hitOnBase);

        // Click far away at (5, 10)
        var miss = CadSelectionEngine.SelectByPoint([dim], new Point2D(5, 10), toleranceMeters: 0.2);
        Assert.Null(miss);
    }
}

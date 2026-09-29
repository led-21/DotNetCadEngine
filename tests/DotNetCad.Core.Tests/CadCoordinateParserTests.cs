using DotNetCad.Core.Editor;
using DotNetCad.Core.Geometry;
using Xunit;

namespace DotNetCad.Core.Tests;

public class CadCoordinateParserTests
{
    [Theory]
    [InlineData("10;20", 10, 20)]
    [InlineData("10,5;20,25", 10.5, 20.25)]
    [InlineData("10.5,20.25", 10.5, 20.25)]
    [InlineData("10,5m;20,25m", 10.5, 20.25)]
    [InlineData("@2;-3", 12, 17)]
    [InlineData("@2,5;-3,5m", 12.5, 16.5)]
    [InlineData("@10<90", 10, 30)]
    [InlineData("@10m<90°", 10, 30)]
    [InlineData("@10<-90deg", 10, 10)]
    public void ResolvesMetricCoordinates(string text, double x, double y)
    {
        Assert.True(CadCoordinateParser.TryParse(text, new Point2D(10, 20), out var point, out var error), error);
        Assert.Equal(x, point.X, 6);
        Assert.Equal(y, point.Y, 6);
    }

    [Theory]
    [InlineData("NaN;0")]
    [InlineData("Infinity;0")]
    [InlineData("@-1<0")]
    [InlineData("@0<90")]
    [InlineData("1,2,3")]
    [InlineData("1e309;0")]
    [InlineData("")]
    public void RejectsInvalidInput(string text) =>
        Assert.False(CadCoordinateParser.TryParse(text, new Point2D(0, 0), out _, out _));

    [Fact]
    public void RelativeInputRequiresAnchor() =>
        Assert.False(CadCoordinateParser.TryParse("@1;2", null, out _, out _));

    [Theory]
    [InlineData("15", 10, 0, null, 25, 10)]
    [InlineData("15,5", 10, 0, null, 25.5, 10)]
    [InlineData("0,25m", 0, 10, null, 10, 10.25)]
    [InlineData("10", 0, 10, null, 10, 20)]
    [InlineData("@20", 0, -5, null, 10, -10)]
    [InlineData("@2,5m", 0, 5, null, 10, 12.5)]
    [InlineData("10", 10, 2, 90.0, 20, 10)]
    [InlineData("10", 2, 10, 90.0, 10, 20)]
    public void ResolvesDirectDistanceEntryWithCursorHint(string text, double cursorRelX, double cursorRelY, double? snapAngle, double expectedX, double expectedY)
    {
        var origin = new Point2D(10, 10);
        var cursor = new Point2D(origin.X + cursorRelX, origin.Y + cursorRelY);
        Assert.True(CadCoordinateParser.TryParse(text, origin, cursor, snapAngle, out var pt, out var err), err);
        Assert.Equal(expectedX, pt.X, 4);
        Assert.Equal(expectedY, pt.Y, 4);
    }
}

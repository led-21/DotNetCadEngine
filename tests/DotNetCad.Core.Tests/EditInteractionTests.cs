using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Core.History;
using Xunit;
namespace DotNetCad.Core.Tests;
public class EditInteractionTests
{
    [Fact]
    public void RotationHoverIsTransientAndUsesSharedPivotAndReference()
    {
        var d=new CadDocument();var h=new UndoRedoManager();var a=new CadLine(new(1,0),new(2,0)){IsSelected=true};var b=new CadLine(new(3,0),new(4,0)){IsSelected=true};d.AddEntity(a);d.AddEntity(b);
        var s=new CadModificationSession(CadToolType.Rotate);s.Select(d.Entities);s.Point(new(0,0));
        s.Hover(new(0,2));Assert.Equal(2,s.Step);Assert.False(s.IsReady);Assert.Equal(90,s.AngleDegrees,8);
        Assert.Equal(0,((CadLine)s.Preview[0]).StartPoint.X,8);Assert.Equal(3,((CadLine)s.Preview[1]).StartPoint.Y,8);
        Assert.Equal(new Point2D(1,0),a.StartPoint);Assert.Equal(0,h.UndoCount);
        Assert.True(s.Input("R"));Assert.True(s.Point(new(0,1))); // reference 90 degrees
        s.Hover(new(-1,0));Assert.Equal(90,s.AngleDegrees,8);Assert.Equal(2,s.Step);
        Assert.True(s.Point(new(-1,0)));Assert.True(s.IsReady);Assert.True(s.Confirm(d,h));Assert.Equal(1,h.UndoCount);
        Assert.Equal(0,a.StartPoint.X,8);Assert.Equal(1,a.StartPoint.Y,8);h.Undo();Assert.Equal(new Point2D(1,0),a.StartPoint);h.Redo();Assert.Equal(1,a.StartPoint.Y,8);
    }
    [Fact]
    public void InvalidFinalPickCanBeCorrectedWithoutRestarting()
    {
        var d=new CadDocument();d.AddEntity(new CadLine(new(0,0),new(1,0)){IsSelected=true});var s=new CadModificationSession(CadToolType.Move);s.Select(d.Entities);s.Point(new(0,0));
        Assert.False(s.Point(new(0,0)));Assert.Equal(2,s.Step);Assert.False(s.IsReady);
        Assert.True(s.Point(new(2.35,0)));Assert.True(s.IsReady);
        var r=new CadModificationSession(CadToolType.Rotate);r.Select(d.Entities);r.Point(new(0,0));Assert.False(r.Point(new(0,0)));Assert.Equal(2,r.Step);Assert.True(r.Input("37"));
    }
    [Fact]
    public void OffsetDistanceCanBeMeasuredWithTwoMousePoints()
    {
        var d=new CadDocument();d.AddEntity(new CadLine(new(0,0),new(4,0)){IsSelected=true});var s=new CadModificationSession(CadToolType.Offset);s.Select(d.Entities);
        Assert.True(s.Point(new(0,0)));Assert.Equal(6,s.Step);s.Hover(new(0,2.35));Assert.Equal(2.35,s.Distance,8);Assert.False(s.IsReady);
        Assert.True(s.Point(new(0,2.35)));Assert.Equal(2,s.Step);Assert.Null(s.PrecisionOrigin);
        s.Hover(new(1,-1));Assert.False(s.IsReady);Assert.Equal(-2.35,((CadLine)s.Preview.Single()).StartPoint.Y,8);Assert.Single(d.Entities);
        Assert.True(s.Point(new(1,-1)));var h=new UndoRedoManager();Assert.True(s.Confirm(d,h));Assert.Equal(2,d.Entities.Count);Assert.Equal(1,h.UndoCount);
    }
}

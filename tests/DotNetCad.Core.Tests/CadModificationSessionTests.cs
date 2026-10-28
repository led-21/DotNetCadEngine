using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Core.History;
using Xunit;

namespace DotNetCad.Core.Tests;
public class CadModificationSessionTests
{
    private static (CadDocument, CadLine, CadLine) Drawing()
    {
        var d = new CadDocument();
        var a = new CadLine(new(1,0),new(2,0)) { IsSelected = true };
        var b = new CadLine(new(3,0),new(4,0)) { IsSelected = true };
        d.AddEntity(a); d.AddEntity(b); return (d,a,b);
    }
    [Theory]
    [InlineData(CadToolType.Move,"@2,35;0",2.35,0)]
    [InlineData(CadToolType.Copy,"@-2;-3",-2,-3)]
    [InlineData(CadToolType.Move,"2,35<180",-2.35,0)]
    public void VectorPreviewAndHistory(CadToolType tool,string input,double x,double y)
    {
        var (d,a,b) = Drawing(); var h = new UndoRedoManager(); var s = new CadModificationSession(tool);
        Assert.True(s.Select(d.Entities)); Assert.True(s.Input("0;0")); Assert.True(s.Input(input));
        Assert.Equal(new Point2D(1,0),a.StartPoint); Assert.Equal(0,h.UndoCount);
        Assert.Equal(1+x,((CadLine)s.Preview[0]).StartPoint.X,8);
        Assert.Equal(y,((CadLine)s.Preview[0]).StartPoint.Y,8);
        Assert.True(s.Confirm(d,h)); Assert.Equal(1,h.UndoCount);
        h.Undo(); Assert.Equal(2,d.Entities.Count); Assert.Equal(new Point2D(1,0),a.StartPoint);
        h.Redo(); Assert.Equal(tool == CadToolType.Copy ? 4 : 2,d.Entities.Count);
        Assert.True(a.IsSelected); Assert.True(b.IsSelected);
    }
    [Fact]
    public void SharedPivotAndInclinedMirror()
    {
        var (d,a,b) = Drawing(); var h = new UndoRedoManager(); var s = new CadModificationSession(CadToolType.Rotate);
        s.Select(d.Entities); s.Input("0;0"); s.Input("37"); Assert.True(s.Confirm(d,h));
        Assert.Equal(3*a.StartPoint.X,b.StartPoint.X,8); Assert.Equal(3*a.StartPoint.Y,b.StartPoint.Y,8);
        h.Undo(); s = new(CadToolType.Mirror); s.Select(d.Entities); s.Input("0;0"); s.Input("1;1");
        Assert.False(s.Confirm(d,h)); s.Input("não"); Assert.True(s.Confirm(d,h));
        Assert.Equal(0,a.StartPoint.X,8); Assert.Equal(1,a.StartPoint.Y,8);
        h.Undo(); Assert.Equal(new Point2D(1,0),a.StartPoint); h.Redo(); Assert.Equal(1,a.StartPoint.Y,8);
    }
    [Theory]
    [InlineData("NaN")][InlineData("Infinity")][InlineData("1e999")][InlineData("abc")]
    public void InvalidAngleDoesNotTouchDocument(string value)
    {
        var (d,a,_) = Drawing(); var h = new UndoRedoManager(); var s = new CadModificationSession(CadToolType.Rotate);
        s.Select(d.Entities); s.Input("0;0"); Assert.False(s.Input(value)); Assert.False(s.Confirm(d,h));
        Assert.Equal(new Point2D(1,0),a.StartPoint); Assert.Equal(0,h.UndoCount);
    }
    [Fact]
    public void OffsetRejectsEntireInvalidSelection()
    {
        var (d,a,_) = Drawing(); d.AddEntity(new CadCircle(new(0,0),0.1) { IsSelected = true });
        var s = new CadModificationSession(CadToolType.Offset); var h = new UndoRedoManager();
        s.Select(d.Entities); s.Input("2,35"); s.Point(new(0,0.05));
        Assert.False(s.Confirm(d,h)); Assert.Equal(3,d.Entities.Count); Assert.Equal(0,h.UndoCount);
    }
}

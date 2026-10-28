using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Core.History;
using DotNetCad.Core.History.Actions;
using Xunit;
namespace DotNetCad.Core.Tests;
public class OffsetIntegrityTests
{
    [Theory]
    [InlineData(2)][InlineData(-2)]
    public void SignedLineOffsetAndHistory(double distance)
    {
        var d = new CadDocument(); var line = new CadLine(new(0,0),new(10,0)) { LineThickness=3 };
        d.AddEntity(line); var h = new UndoRedoManager(); h.ExecuteAction(new OffsetEntityAction(d,line,distance));
        var added = Assert.IsType<CadLine>(d.Entities.Last()); Assert.Equal(distance,added.StartPoint.Y); Assert.Equal(3,added.LineThickness);
        h.Undo(); Assert.Single(d.Entities); h.Redo(); Assert.Same(added,d.Entities.Last());
    }
    [Fact]
    public void InvalidOffsetsPreserveHistoryAndDocument()
    {
        var d = new CadDocument(); var h = new UndoRedoManager(); var line = new CadLine(new(0,0),new(1,0));
        h.ExecuteAction(new AddEntityAction(d,line)); h.Undo();
        ICadEntity[] invalid = [new CadCircle(new(0,0),1),new CadPolyline([new(0,0),new(1,0)],false),new CadText(),new CadLine(new(0,0),new(0,0))];
        foreach(var e in invalid) Assert.Throws<ArgumentException>(() => h.ExecuteAction(new OffsetEntityAction(d,e,-2)));
        Assert.Empty(d.Entities); Assert.Equal(1,h.RedoCount); h.Redo(); Assert.Same(line,d.Entities.Single());
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void PolygonOrientationExpansionAndCollapse(bool reverse)
    {
        var points = new List<Point2D> { new(0,0),new(4,0),new(4,4),new(0,4) };
        if(reverse) points.Reverse();
        var result = CadConstructionMath.OffsetPolygon(points,1);
        Assert.Equal(-1,result.Min(p=>p.X),8); Assert.Equal(5,result.Max(p=>p.Y),8);
        var inset = CadConstructionMath.OffsetPolygon(points,-1); Assert.Equal(1,inset.Min(p=>p.X),8);
        Assert.Throws<ArgumentException>(()=>CadConstructionMath.OffsetPolygon(points,-3));
        Assert.Throws<ArgumentException>(()=>CadConstructionMath.OffsetPolygon([new(0,0),new(2,2),new(0,2),new(2,0)],1));
    }
}

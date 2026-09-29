using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Core.Selection;
using Xunit;

namespace DotNetCad.Core.Tests;

public class CadSelectionEngineTests
{
    [Fact]
    public void SelectByBox_WindowSelection_SelectsOnlyFullyContainedEntities()
    {
        var lineInside = new CadLine(new Point2D(2, 2), new Point2D(4, 4));
        var lineCrossing = new CadLine(new Point2D(5, 5), new Point2D(15, 15));
        var lineOutside = new CadLine(new Point2D(20, 20), new Point2D(25, 25));

        var entities = new List<ICadEntity> { lineInside, lineCrossing, lineOutside };

        // Drag left to right: (0,0) -> (10,10) is Window Selection
        var selected = CadSelectionEngine.SelectByBox(entities, new Point2D(0, 0), new Point2D(10, 10));

        Assert.Single(selected);
        Assert.Contains(lineInside, selected);
        Assert.DoesNotContain(lineCrossing, selected);
    }

    [Fact]
    public void SelectByBox_CrossingSelection_SelectsContainedAndIntersectingEntities()
    {
        var lineInside = new CadLine(new Point2D(2, 2), new Point2D(4, 4));
        var lineCrossing = new CadLine(new Point2D(5, 5), new Point2D(15, 15));
        var lineOutside = new CadLine(new Point2D(20, 20), new Point2D(25, 25));

        var entities = new List<ICadEntity> { lineInside, lineCrossing, lineOutside };

        // Drag right to left: (10,10) -> (0,0) is Crossing Selection
        var selected = CadSelectionEngine.SelectByBox(entities, new Point2D(10, 10), new Point2D(0, 0));

        Assert.Equal(2, selected.Count);
        Assert.Contains(lineInside, selected);
        Assert.Contains(lineCrossing, selected);
        Assert.DoesNotContain(lineOutside, selected);
    }

    [Fact]
    public void HitTestEntity_PointClick_DetectsEntityUnderCursor()
    {
        var line = new CadLine(new Point2D(0, 0), new Point2D(10, 0));
        var circle = new CadCircle(new Point2D(5, 5), radius: 2.0);

        var hitLine = CadSelectionEngine.HitTestEntity(line, new Point2D(5, 0.1), toleranceMeters: 0.3);
        Assert.NotNull(hitLine);

        var hitCircle = CadSelectionEngine.HitTestEntity(circle, new Point2D(5, 5), toleranceMeters: 0.3);
        Assert.NotNull(hitCircle);

        var hitMiss = CadSelectionEngine.HitTestEntity(line, new Point2D(5, 5), toleranceMeters: 0.3);
        Assert.Null(hitMiss);
    }

    [Fact]
    public void CadGripEngine_GeneratesAndAppliesGripMoves()
    {
        var line = new CadLine(new Point2D(0, 0), new Point2D(10, 0));
        var grips = CadGripEngine.GetGripsForEntity(line);

        Assert.Equal(3, grips.Count); // Start, End, Midpoint

        // Move End grip to (10, 5)
        var endGrip = grips.First(g => g.HandleIndex == 1);
        CadGripEngine.ApplyGripMove(line, endGrip, new Point2D(10, 5));

        Assert.Equal(new Point2D(10, 5), line.EndPoint);
        Assert.Equal(new Point2D(0, 0), line.StartPoint);
    }
}

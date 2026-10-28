using System.Windows.Controls;
using System.Windows.Input;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Core.Selection;
using DotNetCad.Demo.Controls;
using DotNetCad.Demo.ViewModels;
using Xunit;

namespace DotNetCad.Demo.Tests;

public class SelectionAndFilteringTests
{
    [Fact]
    public void HollowEntities_HitTest_ClickInsideHollowInterior_DoesNotSelect()
    {
        // Hollow Circle at center (10, 10), radius 5
        var circle = new CadCircle(new Point2D(10, 10), 5.0, "0", "#FFFFFF");
        var entities = new List<ICadEntity> { circle };

        // Click at center (interior) with 0.3m tolerance
        var hitInterior = CadSelectionEngine.SelectByPoint(entities, new Point2D(10, 10), toleranceMeters: 0.3);
        Assert.Null(hitInterior);

        // Click near perimeter at (15, 10)
        var hitPerimeter = CadSelectionEngine.SelectByPoint(entities, new Point2D(15, 10), toleranceMeters: 0.3);
        Assert.Same(circle, hitPerimeter);

        // Hollow Rectangle from (0,0) to (10,10)
        var rect = new CadRectangle(new Point2D(0, 0), 10.0, 10.0, 0.0, "0", "#FFFFFF");
        var rectEntities = new List<ICadEntity> { rect };

        // Click inside rectangle center (5, 5)
        var hitRectInterior = CadSelectionEngine.SelectByPoint(rectEntities, new Point2D(5, 5), toleranceMeters: 0.3);
        Assert.Null(hitRectInterior);

        // Click on edge (5, 0)
        var hitRectEdge = CadSelectionEngine.SelectByPoint(rectEntities, new Point2D(5, 0), toleranceMeters: 0.3);
        Assert.Same(rect, hitRectEdge);

        // Hollow closed polyline (triangle (0,0)-(10,0)-(5,10))
        var poly = new CadPolyline(
            new List<Point2D> { new(0, 0), new(10, 0), new(5, 10) },
            isClosed: true,
            layer: "0",
            colorHex: "#FFFFFF");
        var polyEntities = new List<ICadEntity> { poly };

        // Click inside triangle (5, 3)
        var hitPolyInterior = CadSelectionEngine.SelectByPoint(polyEntities, new Point2D(5, 3), toleranceMeters: 0.3);
        Assert.Null(hitPolyInterior);

        // Click on base edge (5, 0)
        var hitPolyEdge = CadSelectionEngine.SelectByPoint(polyEntities, new Point2D(5, 0), toleranceMeters: 0.3);
        Assert.Same(poly, hitPolyEdge);
    }

    [Fact]
    public void WindowVsCrossing_DragRightVsDragLeft_BehavesDistinctly()
    {
        // A line from (0, 0) to (10, 0)
        var line = new CadLine(new Point2D(0, 0), new Point2D(10, 0), "0", "#FFFFFF");
        var entities = new List<ICadEntity> { line };

        // Window selection: p1.X < p2.X (drag right from (2, -2) to (8, 2))
        // The line extends to 0 and 10, so it is NOT completely contained in [2..8].
        var windowSelected = CadSelectionEngine.SelectByBox(entities, new Point2D(2, -2), new Point2D(8, 2));
        Assert.Empty(windowSelected);

        // Crossing selection: p1.X > p2.X (drag left from (8, 2) to (2, -2))
        // The selection box crosses/intersects the line.
        var crossingSelected = CadSelectionEngine.SelectByBox(entities, new Point2D(8, 2), new Point2D(2, -2));
        Assert.Single(crossingSelected);
        Assert.Same(line, crossingSelected[0]);

        // When entity is fully contained, both Window and Crossing select it
        var smallLine = new CadLine(new Point2D(3, 0), new Point2D(7, 0), "0", "#FFFFFF");
        var smallEntities = new List<ICadEntity> { smallLine };

        var winFull = CadSelectionEngine.SelectByBox(smallEntities, new Point2D(2, -2), new Point2D(8, 2));
        var crossFull = CadSelectionEngine.SelectByBox(smallEntities, new Point2D(8, 2), new Point2D(2, -2));
        Assert.Single(winFull);
        Assert.Single(crossFull);
    }

    [Fact]
    public void OverlappingEntities_CanBeCycledByIndex()
    {
        var line1 = new CadLine(new Point2D(0, 0), new Point2D(10, 10), "0", "#FF0000");
        var line2 = new CadLine(new Point2D(0, 10), new Point2D(10, 0), "0", "#00FF00");
        var entities = new List<ICadEntity> { line1, line2 };

        // Point of intersection is (5, 5)
        var allHits = CadSelectionEngine.HitTestAllEntities(entities, new Point2D(5, 5), toleranceMeters: 0.3);
        Assert.Equal(2, allHits.Count);

        var first = CadSelectionEngine.SelectByPoint(entities, new Point2D(5, 5), toleranceMeters: 0.3, cycleIndex: 0);
        var second = CadSelectionEngine.SelectByPoint(entities, new Point2D(5, 5), toleranceMeters: 0.3, cycleIndex: 1);
        var looped = CadSelectionEngine.SelectByPoint(entities, new Point2D(5, 5), toleranceMeters: 0.3, cycleIndex: 2);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.Same(first, looped);
    }

    [Fact]
    public void SelectionFiltering_ByTypeAndLayer_FiltersCorrectly()
    {
        var vm = new MainViewModel();
        vm.Document.Layers.Add(new CadLayer { Name = "Paredes", ColorHex = "#FF0000", IsVisible = true, IsLocked = false });
        vm.Document.Layers.Add(new CadLayer { Name = "Esquadrias", ColorHex = "#00FF00", IsVisible = true, IsLocked = true });

        var line1 = new CadLine(new Point2D(0, 0), new Point2D(5, 5), "Paredes", "#FF0000");
        var line2 = new CadLine(new Point2D(5, 5), new Point2D(10, 10), "0", "#FFFFFF");
        var circle = new CadCircle(new Point2D(2, 2), 1.0, "Paredes", "#FF0000");
        var lockedLine = new CadLine(new Point2D(1, 1), new Point2D(2, 2), "Esquadrias", "#00FF00");

        vm.Document.Entities.Add(line1);
        vm.Document.Entities.Add(line2);
        vm.Document.Entities.Add(circle);
        vm.Document.Entities.Add(lockedLine);

        // Filter by Circle only
        vm.SelectedTypeFilter = CadEntityType.Circle;
        Assert.True(vm.HasActiveSelectionFilter);
        vm.SelectAll();
        Assert.True(circle.IsSelected);
        Assert.False(line1.IsSelected);
        Assert.False(line2.IsSelected);
        Assert.False(lockedLine.IsSelected);

        // Clear filter, select by layer Paredes
        vm.ClearSelection();
        vm.SelectedTypeFilter = null;
        vm.SelectedLayerFilter = "Paredes";
        vm.SelectAll();
        Assert.True(line1.IsSelected);
        Assert.True(circle.IsSelected);
        Assert.False(line2.IsSelected);
        Assert.False(lockedLine.IsSelected); // Locked layer is never selected!

        // Invert selection with Paredes filter
        vm.InvertSelection();
        Assert.False(line1.IsSelected);
        Assert.False(circle.IsSelected);
        Assert.False(lockedLine.IsSelected);

        // Reset filter
        vm.ClearSelectionFilterCommand.Execute(null);
        Assert.False(vm.HasActiveSelectionFilter);
    }
}

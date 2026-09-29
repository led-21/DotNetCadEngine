using System.Windows;
using DotNetCad.Core.Geometry;
using Xunit;

namespace DotNetCad.Demo.Tests;

public class ViewportTransformTests
{
    [Fact]
    public void ViewportTransform_WorldToScreenAndBack_PreservesCoordinates()
    {
        var transform = new ViewportTransform
        {
            Zoom = 20.0,
            PanX = 150.0,
            PanY = 300.0
        };

        var worldPt = new Point2D(12.5, -8.4);
        var screenPt = transform.WorldToScreen(worldPt);

        var restoredWorld = transform.ScreenToWorld(screenPt);

        Assert.Equal(worldPt.X, restoredWorld.X, precision: 4);
        Assert.Equal(worldPt.Y, restoredWorld.Y, precision: 4);
    }

    [Fact]
    public void ViewportTransform_ZoomAtScreenPoint_MaintainsPointUnderCursor()
    {
        var transform = new ViewportTransform
        {
            Zoom = 10.0,
            PanX = 200.0,
            PanY = 200.0
        };

        var cursorScreen = new ScreenPoint(400, 300);
        var worldUnderCursorBefore = transform.ScreenToWorld(cursorScreen);

        // Zoom in to 25.0
        transform.ZoomAtScreenPoint(25.0, cursorScreen);

        var worldUnderCursorAfter = transform.ScreenToWorld(cursorScreen);

        Assert.Equal(worldUnderCursorBefore.X, worldUnderCursorAfter.X, precision: 4);
        Assert.Equal(worldUnderCursorBefore.Y, worldUnderCursorAfter.Y, precision: 4);
    }

    [Fact]
    public void ViewportTransform_PanDrag_MovesWpfScreenCoordinatesInDragDirection()
    {
        var transform = new ViewportTransform
        {
            Zoom = 25.0,
            PanX = 400.0,
            PanY = 300.0
        };

        const double actualHeight = 600.0;
        Point WorldToWpf(Point2D world)
        {
            var scr = transform.WorldToScreen(world);
            return new Point(scr.X, actualHeight - scr.Y);
        }

        var originWorld = Point2D.Zero;
        var wpfBefore = WorldToWpf(originWorld);

        // User drags mouse by +50 in X (right) and +30 in Y (down)
        var startMouse = new Point(100, 100);
        var currentMouse = new Point(150, 130);

        var dx = currentMouse.X - startMouse.X; // +50
        var dy = startMouse.Y - currentMouse.Y; // -30

        transform.PanX += dx;
        transform.PanY += dy;

        var wpfAfter = WorldToWpf(originWorld);

        // WPF coordinate should move +50 in X and +30 in Y (in the direction of mouse drag)
        Assert.Equal(wpfBefore.X + 50.0, wpfAfter.X, precision: 4);
        Assert.Equal(wpfBefore.Y + 30.0, wpfAfter.Y, precision: 4);
    }
}

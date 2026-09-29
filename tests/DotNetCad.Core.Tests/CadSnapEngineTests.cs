using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Core.Snapping;
using Xunit;

namespace DotNetCad.Core.Tests;

public class CadSnapEngineTests
{
    [Fact]
    public void SnapToGrid_SnapsToNearestInterval()
    {
        var pt = new Point2D(10.22, 15.78);
        var snapped = CadSnapEngine.SnapToGrid(pt, gridIntervalMeters: 0.50);

        Assert.Equal(10.0, snapped.X, precision: 3);
        Assert.Equal(16.0, snapped.Y, precision: 3);
    }

    [Fact]
    public void SnapOrthoAndPolar_OrthoMode_SnapsStrictlyToHorizontalOrVertical()
    {
        var origin = new Point2D(10, 10);

        // Movement predominantly horizontal (dx=8, dy=2) -> lock Y=10
        var mouseH = new Point2D(18, 12);
        var orthoH = CadSnapEngine.SnapOrthoAndPolar(origin, mouseH, isOrtho: true, isPolar: false);
        Assert.True(orthoH.IsOrthoSnapped);
        Assert.Equal(18.0, orthoH.Point.X, precision: 3);
        Assert.Equal(10.0, orthoH.Point.Y, precision: 3);
        Assert.Equal(0.0, orthoH.AngleDegrees, precision: 1);

        // Movement predominantly vertical (dx=-1, dy=9) -> lock X=10
        var mouseV = new Point2D(9, 19);
        var orthoV = CadSnapEngine.SnapOrthoAndPolar(origin, mouseV, isOrtho: true, isPolar: false);
        Assert.True(orthoV.IsOrthoSnapped);
        Assert.Equal(10.0, orthoV.Point.X, precision: 3);
        Assert.Equal(19.0, orthoV.Point.Y, precision: 3);
        Assert.Equal(90.0, orthoV.AngleDegrees, precision: 1);
    }

    [Fact]
    public void SnapOrthoAndPolar_PolarTracking_SnapsNearStandardAngles()
    {
        var origin = new Point2D(0, 0);

        // Point near 45 degrees
        var mouse46 = new Point2D(7.0, 7.2);
        var polarRes = CadSnapEngine.SnapOrthoAndPolar(origin, mouse46, isOrtho: false, isPolar: true, polarIncrementDegrees: 45.0, toleranceDegrees: 5.0);

        Assert.True(polarRes.IsPolarSnapped);
        Assert.Equal(45.0, polarRes.AngleDegrees, precision: 1);
        Assert.Equal(polarRes.Point.X, polarRes.Point.Y, precision: 3);
    }

    [Fact]
    public void FindOsnap_FindsEndpointsMidpointsAndCenters()
    {
        var line = new CadLine(new Point2D(0, 0), new Point2D(10, 0));
        var circle = new CadCircle(new Point2D(20, 20), radius: 5.0);
        var entities = new List<ICadEntity> { line, circle };

        var prefs = new CadPreferences { IsOsnapEnabled = true };

        // Test Endpoint near (0,0)
        var snapNearStart = CadSnapEngine.FindOsnap(new Point2D(0.1, 0.1), entities, prefs, toleranceMeters: 0.5);
        Assert.NotNull(snapNearStart);
        Assert.Equal(OsnapType.Endpoint, snapNearStart.Type);
        Assert.Equal(new Point2D(0, 0), snapNearStart.Point);

        // Test Midpoint near (5,0)
        var snapNearMid = CadSnapEngine.FindOsnap(new Point2D(4.9, 0.1), entities, prefs, toleranceMeters: 0.5);
        Assert.NotNull(snapNearMid);
        Assert.Equal(OsnapType.Midpoint, snapNearMid.Type);
        Assert.Equal(new Point2D(5, 0), snapNearMid.Point);

        // Test Center near (20,20)
        var snapNearCenter = CadSnapEngine.FindOsnap(new Point2D(20.1, 19.9), entities, prefs, toleranceMeters: 0.5);
        Assert.NotNull(snapNearCenter);
        Assert.Equal(OsnapType.Center, snapNearCenter.Type);
        Assert.Equal(new Point2D(20, 20), snapNearCenter.Point);
    }
}

using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Infrastructure.Native;
using Xunit;

namespace DotNetCad.Core.Tests;

public class CadDrawingSerializerTests
{
    [Fact]
    public void NativeDrawingRoundTripPreservesEntitiesLayersPreferencesAndIds()
    {
        var document = new CadDocument { Title = "Casa.cad", ActiveLayer = "PAREDES" };
        document.Layers.Add(new CadLayer { Name = "PAREDES", ColorHex = "#123456", IsLocked = true });
        document.Preferences.GridIntervalMeters = 0.25;
        document.Entities.AddRange([
            new CadLine(new(0, 0), new(12, 0), "PAREDES"),
            new CadCircle(new(2, 3), 1, "PAREDES"),
            new CadArc(new(3, 3), 2, 30, 150, "PAREDES"),
            new CadRectangle(new(1, 1), 2, 3, 15, "PAREDES"),
            new CadPolyline([new(0, 0), new(1, 1), new(2, 0)], layer: "PAREDES"),
            new CadDimension { StartPoint = new(0, 0), EndPoint = new(4, 0), OffsetPoint = new(2, 1), Layer = "PAREDES" },
            new CadText { Position = new(2, 2), Text = "Quarto 1", Height = 0.5, RotationDegrees = 45, Layer = "PAREDES" }
        ]);

        var restored = CadDrawingSerializer.Deserialize(CadDrawingSerializer.Serialize(document));

        Assert.Equal("Casa.cad", restored.Title);
        Assert.Equal("0", restored.ActiveLayer); // Legacy locked active layers migrate to a usable successor.
        Assert.Equal(0.25, restored.Preferences.GridIntervalMeters);
        Assert.True(restored.GetLayer("PAREDES")!.IsLocked);
        Assert.Equal(document.Entities.Select(entity => entity.Id), restored.Entities.Select(entity => entity.Id));
        Assert.Equal(document.Entities.Select(entity => entity.EntityType), restored.Entities.Select(entity => entity.EntityType));
        Assert.Equal(12, Assert.IsType<CadLine>(restored.Entities[0]).EndPoint.X);
        Assert.Equal(15, Assert.IsType<CadRectangle>(restored.Entities[3]).RotationDegrees);
        Assert.Equal("Quarto 1", Assert.IsType<CadText>(restored.Entities[6]).Text);
    }

    [Fact]
    public void NativeDrawingRejectsUnsupportedVersionAndMalformedJson()
    {
        var json = CadDrawingSerializer.Serialize(new CadDocument());
        Assert.Throws<InvalidDataException>(() => CadDrawingSerializer.Deserialize(json.Replace("\"Version\": 1", "\"Version\": 99")));
        Assert.Throws<InvalidDataException>(() => CadDrawingSerializer.Deserialize("{ invalid JSON"));
    }
}

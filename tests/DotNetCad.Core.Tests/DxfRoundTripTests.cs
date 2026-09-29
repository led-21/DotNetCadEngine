using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Infrastructure.Dxf;
using Xunit;

namespace DotNetCad.Core.Tests;

public class DxfRoundTripTests
{
    [Fact]
    public void DxfCad_ExportAndImport_RoundTripMaintainsIntegrity()
    {
        var doc = new CadDocument { Title = "TestDocument" };

        var line = new CadLine(new Point2D(0, 0), new Point2D(10, 20), layer: "Geometry", colorHex: "#38BDF8");
        var circle = new CadCircle(new Point2D(5, 5), radius: 3.5, layer: "Geometry", colorHex: "#EF4444");
        var poly = new CadPolyline([new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 5), new Point2D(0, 5)], isClosed: true, layer: "Dimensions");

        doc.AddEntity(line);
        doc.AddEntity(circle);
        doc.AddEntity(poly);

        // 1. Export to DXF ASCII
        var dxfContent = DxfCadExporter.ExportToDxf(doc);
        Assert.Contains("SECTION", dxfContent);
        Assert.Contains("HEADER", dxfContent);
        Assert.Contains("TABLES", dxfContent);
        Assert.Contains("ENTITIES", dxfContent);
        Assert.Contains("LINE", dxfContent);
        Assert.Contains("CIRCLE", dxfContent);
        Assert.Contains("LWPOLYLINE", dxfContent);

        // 2. Reimport from DXF
        var importedDoc = DxfCadImporter.ImportFromDxf(dxfContent, "Reimported");

        Assert.Equal(3, importedDoc.Entities.Count);

        var importedLine = importedDoc.Entities.OfType<CadLine>().FirstOrDefault();
        Assert.NotNull(importedLine);
        Assert.Equal(0.0, importedLine.StartPoint.X, precision: 2);
        Assert.Equal(0.0, importedLine.StartPoint.Y, precision: 2);
        Assert.Equal(10.0, importedLine.EndPoint.X, precision: 2);
        Assert.Equal(20.0, importedLine.EndPoint.Y, precision: 2);

        var importedCircle = importedDoc.Entities.OfType<CadCircle>().FirstOrDefault();
        Assert.NotNull(importedCircle);
        Assert.Equal(5.0, importedCircle.Center.X, precision: 2);
        Assert.Equal(5.0, importedCircle.Center.Y, precision: 2);
        Assert.Equal(3.5, importedCircle.Radius, precision: 2);

        var importedPoly = importedDoc.Entities.OfType<CadPolyline>().FirstOrDefault();
        Assert.NotNull(importedPoly);
        Assert.True(importedPoly.IsClosed);
        Assert.Equal(4, importedPoly.Vertices.Count);
    }
}

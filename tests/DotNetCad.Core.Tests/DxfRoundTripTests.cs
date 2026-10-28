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

    [Fact]
    public void DxfCad_TextRoundTripsWithPositionHeightAndRotation()
    {
        var document = new CadDocument();
        document.AddEntity(new CadText
        {
            Position = new Point2D(2, 3),
            Height = 0.5,
            RotationDegrees = 30,
            Text = "Quarto 1",
            Layer = "Annotations"
        });

        var imported = DxfCadImporter.ImportFromDxf(DxfCadExporter.ExportToDxf(document));

        var label = Assert.Single(imported.Entities.OfType<CadText>());
        Assert.Equal("Quarto 1", label.Text);
        Assert.Equal(2, label.Position.X);
        Assert.Equal(3, label.Position.Y);
        Assert.Equal(0.5, label.Height);
        Assert.Equal(30, label.RotationDegrees);
    }

    [Fact]
    public void Dxf_RoundTrip_CadDimension_PreservedSemantically()
    {
        var doc = new CadDocument();
        var dim = new CadDimension(new Point2D(0, 0), new Point2D(12.5, 0), new Point2D(6.25, 2.5), "Dimensions", "#EF4444")
        {
            CustomTextOverride = "VIGA V1"
        };
        doc.AddEntity(dim);

        // Export to DXF
        var dxf = DxfCadExporter.ExportToDxf(doc);
        Assert.Contains("0\nDIMENSION", dxf);
        Assert.Contains("VIGA V1", dxf);

        // Reimport with report
        var report = DxfCadImporter.ImportFromDxfWithReport(dxf);
        Assert.Equal(1, report.ImportedEntityCount);

        var importedDim = Assert.IsType<CadDimension>(Assert.Single(report.Document.Entities));
        Assert.Equal(0.0, importedDim.StartPoint.X, precision: 4);
        Assert.Equal(0.0, importedDim.StartPoint.Y, precision: 4);
        Assert.Equal(12.5, importedDim.EndPoint.X, precision: 4);
        Assert.Equal(0.0, importedDim.EndPoint.Y, precision: 4);
        Assert.Equal(6.25, importedDim.OffsetPoint.X, precision: 4);
        Assert.Equal(2.5, importedDim.OffsetPoint.Y, precision: 4);
        Assert.Equal("VIGA V1", importedDim.CustomTextOverride);
        Assert.Equal("Dimensions", importedDim.Layer);
        Assert.Equal("#EF4444", importedDim.ColorHex);
    }

    [Fact]
    public void Dxf_Import_LegacyPolylineVertexSeqEnd()
    {
        var legacyDxf = """
        0
        SECTION
        2
        HEADER
        9
        $ACADVER
        1
        AC1009
        0
        ENDSEC
        0
        SECTION
        2
        ENTITIES
        0
        POLYLINE
        8
        Geometry
        70
        1
        0
        VERTEX
        8
        Geometry
        10
        0.0
        20
        0.0
        0
        VERTEX
        8
        Geometry
        10
        10.0
        20
        0.0
        0
        VERTEX
        8
        Geometry
        10
        10.0
        20
        5.0
        0
        SEQEND
        8
        Geometry
        0
        ENDSEC
        0
        EOF
        """;

        var report = DxfCadImporter.ImportFromDxfWithReport(legacyDxf);
        Assert.Equal("AC1009", report.DetectedAcadVersion);
        Assert.Equal(1, report.ImportedEntityCount);

        var poly = Assert.IsType<CadPolyline>(Assert.Single(report.Document.Entities));
        Assert.True(poly.IsClosed);
        Assert.Equal(3, poly.Vertices.Count);
        Assert.Equal(0.0, poly.Vertices[0].X);
        Assert.Equal(10.0, poly.Vertices[1].X);
        Assert.Equal(5.0, poly.Vertices[2].Y);
    }

    [Fact]
    public void Dxf_Import_HeaderInsUnits_SetsDisplayUnit()
    {
        // 4 = Millimeters
        var dxfMm = """
        0
        SECTION
        2
        HEADER
        9
        $INSUNITS
        70
        4
        0
        ENDSEC
        0
        SECTION
        2
        ENTITIES
        0
        LINE
        8
        0
        10
        0
        20
        0
        11
        100
        21
        0
        0
        ENDSEC
        0
        EOF
        """;

        var reportMm = DxfCadImporter.ImportFromDxfWithReport(dxfMm);
        Assert.Equal("mm", reportMm.Document.Preferences.DisplayUnit);

        // 5 = Centimeters
        var dxfCm = dxfMm.Replace("70\n4", "70\n5");
        var reportCm = DxfCadImporter.ImportFromDxfWithReport(dxfCm);
        Assert.Equal("cm", reportCm.Document.Preferences.DisplayUnit);

        // 6 = Meters
        var dxfM = dxfMm.Replace("70\n4", "70\n6");
        var reportM = DxfCadImporter.ImportFromDxfWithReport(dxfM);
        Assert.Equal("m", reportM.Document.Preferences.DisplayUnit);
    }

    [Fact]
    public void Dxf_Import_Report_CapturesWarningsAndSkippedEntities()
    {
        var mixedDxf = """
        0
        SECTION
        2
        ENTITIES
        0
        LINE
        8
        Geometry
        10
        0.0
        20
        0.0
        11
        10.0
        21
        0.0
        0
        HATCH
        8
        HatchLayer
        0
        SPLINE
        8
        SplineLayer
        0
        ENDSEC
        0
        EOF
        """;

        var report = DxfCadImporter.ImportFromDxfWithReport(mixedDxf);
        Assert.Equal(1, report.ImportedEntityCount);
        Assert.Equal(2, report.SkippedEntityCount);
        Assert.True(report.HasWarnings);
        Assert.Contains(report.Warnings, w => w.EntityType == "HATCH" && w.Code == "UNSUPPORTED_ENTITY");
        Assert.Contains(report.Warnings, w => w.EntityType == "SPLINE" && w.Code == "UNSUPPORTED_ENTITY");
    }

    [Fact]
    public void Dxf_ExportVersion_R12_And_R2000()
    {
        var doc = new CadDocument();
        doc.AddEntity(new CadPolyline([new Point2D(0, 0), new Point2D(5, 5), new Point2D(5, 0)], isClosed: true));

        // R12 Export
        var r12 = DxfCadExporter.ExportToDxf(doc, DxfExportVersion.R12);
        Assert.Contains("AC1009", r12);
        Assert.Contains("0\nPOLYLINE", r12);
        Assert.Contains("0\nVERTEX", r12);
        Assert.Contains("0\nSEQEND", r12);
        Assert.DoesNotContain("0\nLWPOLYLINE", r12);

        // R2000 Export (default)
        var r2000 = DxfCadExporter.ExportToDxf(doc, DxfExportVersion.R2000);
        Assert.Contains("AC1015", r2000);
        Assert.Contains("0\nLWPOLYLINE", r2000);
        Assert.DoesNotContain("0\nSEQEND", r2000);
    }

    [Fact]
    public void Dxf_TrueColor_PreservesArbitraryHex()
    {
        var doc = new CadDocument();
        doc.AddEntity(new CadLine(new Point2D(1, 2), new Point2D(3, 4), "Geometry", "#4A90E2"));

        var dxf = DxfCadExporter.ExportToDxf(doc);
        Assert.Contains("420\n", dxf);

        var imported = DxfCadImporter.ImportFromDxf(dxf);
        var line = Assert.IsType<CadLine>(Assert.Single(imported.Entities));
        Assert.Equal("#4A90E2", line.ColorHex);
    }

    [Fact]
    public void Dxf_MText_BasicFormattingStripped()
    {
        var mtextDxf = """
        0
        SECTION
        2
        ENTITIES
        0
        MTEXT
        8
        Notes
        10
        5.0
        20
        10.0
        40
        1.2
        50
        0.0
        1
        {\fArial;Primeira Linha\P\~Segunda Linha}
        0
        ENDSEC
        0
        EOF
        """;

        var report = DxfCadImporter.ImportFromDxfWithReport(mtextDxf);
        Assert.Equal(1, report.ImportedEntityCount);
        var text = Assert.IsType<CadText>(Assert.Single(report.Document.Entities));
        Assert.Contains("Primeira Linha", text.Text);
        Assert.Contains("Segunda Linha", text.Text);
        Assert.DoesNotContain(@"\fArial", text.Text);
        Assert.DoesNotContain(@"\P", text.Text);
    }
}

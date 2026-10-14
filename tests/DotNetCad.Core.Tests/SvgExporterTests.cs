using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Infrastructure.Svg;
using Xunit;

namespace DotNetCad.Core.Tests;

public class SvgExporterTests
{
    [Fact]
    public void ExportToSvg_GeneratesValidSvgTagsAndViewBox()
    {
        var doc = new CadDocument { Title = "SvgTestDoc" };

        var line = new CadLine(new Point2D(0, 0), new Point2D(10, 10), layer: "Geometry", colorHex: "#38BDF8");
        var circle = new CadCircle(new Point2D(5, 5), radius: 2.0, layer: "Geometry", colorHex: "#10B981");

        doc.AddEntity(line);
        doc.AddEntity(circle);

        var svg = SvgCadExporter.ExportToSvg(doc);

        Assert.Contains("<svg", svg);
        Assert.Contains("viewBox=", svg);
        Assert.Contains("<line", svg);
        Assert.Contains("<circle", svg);
        Assert.Contains("</svg>", svg);
    }

    [Fact]
    public void ExportToSvg_EscapesTextLabels()
    {
        var document = new CadDocument();
        document.AddEntity(new CadText
        {
            Position = new Point2D(1, 2), Height = 0.5, Text = "Sala & Cozinha"
        });

        var svg = SvgCadExporter.ExportToSvg(document);

        Assert.Contains("Sala &amp; Cozinha", svg);
        Assert.Contains("font-size=\"0.500\"", svg);
    }
}

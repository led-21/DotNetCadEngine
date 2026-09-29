using System.Globalization;
using System.Text;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;

namespace DotNetCad.Infrastructure.Dxf;

public static class DxfCadExporter
{
    public static string ExportToDxf(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;

        // 1. Header Section (AutoCAD R12 ASCII)
        sb.AppendLine("0\nSECTION\n2\nHEADER\n9\n$ACADVER\n1\nAC1009");
        sb.AppendLine("9\n$INSUNITS\n70\n6"); // 6 = Meters
        sb.AppendLine("0\nENDSEC");

        // 2. Tables Section (LAYERS)
        sb.AppendLine("0\nSECTION\n2\nTABLES\n0\nTABLE\n2\nLAYER\n70\n" + document.Layers.Count);
        foreach (var layer in document.Layers)
        {
            var aci = ColorHexToAci(layer.ColorHex);
            WriteLayer(sb, layer.Name, aci);
        }
        sb.AppendLine("0\nENDTAB\n0\nENDSEC");

        // 3. Entities Section
        sb.AppendLine("0\nSECTION\n2\nENTITIES");

        foreach (var entity in document.Entities)
        {
            var layer = string.IsNullOrWhiteSpace(entity.Layer) ? "0" : entity.Layer;

            if (entity is CadLine line)
            {
                WriteLine(sb, layer, line.StartPoint.X, line.StartPoint.Y, line.EndPoint.X, line.EndPoint.Y, inv);
            }
            else if (entity is CadCircle circle)
            {
                WriteCircle(sb, layer, circle.Center.X, circle.Center.Y, circle.Radius, inv);
            }
            else if (entity is CadArc arc)
            {
                WriteArc(sb, layer, arc.Center.X, arc.Center.Y, arc.Radius, arc.StartAngleDegrees, arc.EndAngleDegrees, inv);
            }
            else if (entity is CadRectangle rect)
            {
                var v = rect.GetVertices();
                WritePolyline(sb, layer, v, isClosed: true, inv);
            }
            else if (entity is CadPolyline poly)
            {
                WritePolyline(sb, layer, poly.Vertices, poly.IsClosed, inv);
            }
            else if (entity is CadDimension dim)
            {
                var (d1, d2) = dim.GetDimensionLinePoints();
                WriteLine(sb, layer, dim.StartPoint.X, dim.StartPoint.Y, d1.X, d1.Y, inv);
                WriteLine(sb, layer, dim.EndPoint.X, dim.EndPoint.Y, d2.X, d2.Y, inv);
                WriteLine(sb, layer, d1.X, d1.Y, d2.X, d2.Y, inv);
                var mid = new Point2D((d1.X + d2.X) / 2.0, (d1.Y + d2.Y) / 2.0);
                WriteText(sb, layer, mid.X, mid.Y + 0.2, 0.35, dim.FormattedText, inv);
            }
        }

        sb.AppendLine("0\nENDSEC\n0\nEOF");
        return sb.ToString();
    }

    private static void WriteLayer(StringBuilder sb, string name, int colorIndex)
    {
        sb.AppendLine($"0\nLAYER\n2\n{name}\n70\n0\n62\n{colorIndex}\n6\nCONTINUOUS");
    }

    private static void WriteLine(StringBuilder sb, string layer, double x1, double y1, double x2, double y2, CultureInfo inv)
    {
        sb.AppendLine($"0\nLINE\n8\n{layer}\n10\n{x1.ToString("F4", inv)}\n20\n{y1.ToString("F4", inv)}\n30\n0.0\n11\n{x2.ToString("F4", inv)}\n21\n{y2.ToString("F4", inv)}\n31\n0.0");
    }

    private static void WriteCircle(StringBuilder sb, string layer, double cx, double cy, double radius, CultureInfo inv)
    {
        sb.AppendLine($"0\nCIRCLE\n8\n{layer}\n10\n{cx.ToString("F4", inv)}\n20\n{cy.ToString("F4", inv)}\n30\n0.0\n40\n{radius.ToString("F4", inv)}");
    }

    private static void WriteArc(StringBuilder sb, string layer, double cx, double cy, double radius, double startAngle, double endAngle, CultureInfo inv)
    {
        sb.AppendLine($"0\nARC\n8\n{layer}\n10\n{cx.ToString("F4", inv)}\n20\n{cy.ToString("F4", inv)}\n30\n0.0\n40\n{radius.ToString("F4", inv)}\n50\n{startAngle.ToString("F2", inv)}\n51\n{endAngle.ToString("F2", inv)}");
    }

    private static void WritePolyline(StringBuilder sb, string layer, IReadOnlyList<Point2D> vertices, bool isClosed, CultureInfo inv)
    {
        if (vertices.Count < 2) return;

        sb.AppendLine("0\nLWPOLYLINE");
        sb.AppendLine($"8\n{layer}");
        sb.AppendLine($"90\n{vertices.Count}");
        sb.AppendLine($"70\n{(isClosed ? "1" : "0")}");

        foreach (var v in vertices)
        {
            sb.AppendLine($"10\n{v.X.ToString("F4", inv)}");
            sb.AppendLine($"20\n{v.Y.ToString("F4", inv)}");
        }
    }

    private static void WriteText(StringBuilder sb, string layer, double x, double y, double height, string text, CultureInfo inv)
    {
        sb.AppendLine($"0\nTEXT\n8\n{layer}\n10\n{x.ToString("F4", inv)}\n20\n{y.ToString("F4", inv)}\n30\n0.0\n40\n{height.ToString("F4", inv)}\n1\n{text}");
    }

    public static int ColorHexToAci(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return 7;
        hex = hex.Trim().ToUpperInvariant();
        if (hex.Contains("EF4444") || hex.Contains("FF0000")) return 1;
        if (hex.Contains("EAB308") || hex.Contains("FFFF00")) return 2;
        if (hex.Contains("10B981") || hex.Contains("00FF00") || hex.Contains("22C55E")) return 3;
        if (hex.Contains("06B6D4") || hex.Contains("00FFFF") || hex.Contains("38BDF8")) return 4;
        if (hex.Contains("3B82F6") || hex.Contains("0000FF")) return 5;
        if (hex.Contains("D946EF") || hex.Contains("FF00FF")) return 6;
        if (hex.Contains("FFFFFF")) return 7;
        if (hex.Contains("94A3B8") || hex.Contains("808080")) return 8;
        return 7;
    }

    public static string AciToColorHex(int aci)
    {
        return aci switch
        {
            1 => "#EF4444",
            2 => "#EAB308",
            3 => "#10B981",
            4 => "#06B6D4",
            5 => "#3B82F6",
            6 => "#D946EF",
            7 => "#FFFFFF",
            8 => "#94A3B8",
            9 => "#CBD5E1",
            _ => "#FFFFFF"
        };
    }
}

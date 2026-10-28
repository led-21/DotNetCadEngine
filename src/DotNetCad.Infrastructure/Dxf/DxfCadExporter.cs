using System.Globalization;
using System.Text;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;

namespace DotNetCad.Infrastructure.Dxf;

/// <summary>
/// Versões do padrão DXF suportadas na exportação.
/// </summary>
public enum DxfExportVersion
{
    /// <summary>AutoCAD R12 ASCII (AC1009) — máxima retrocompatibilidade com softwares legados.</summary>
    R12,

    /// <summary>AutoCAD 2000 ASCII (AC1015) — padrão moderno com LWPOLYLINE e TrueColor.</summary>
    R2000
}

public static class DxfCadExporter
{
    public static string ExportToDxf(CadDocument document, DxfExportVersion version = DxfExportVersion.R2000)
    {
        ArgumentNullException.ThrowIfNull(document);

        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;

        var acadVer = version == DxfExportVersion.R12 ? "AC1009" : "AC1015";

        // 1. Header Section
        sb.AppendLine("0\nSECTION\n2\nHEADER\n9\n$ACADVER\n1\n" + acadVer);
        var insUnits = DotNetCad.Core.Measurements.CadUnitConverter.GetDxfInsUnits(document.Preferences.DisplayUnit);
        sb.AppendLine($"9\n$INSUNITS\n70\n{insUnits}");

        var bbox = document.BoundingBox;
        if (!bbox.IsEmpty)
        {
            sb.AppendLine($"9\n$EXTMIN\n10\n{bbox.MinX.ToString("F6", inv)}\n20\n{bbox.MinY.ToString("F6", inv)}\n30\n0.0");
            sb.AppendLine($"9\n$EXTMAX\n10\n{bbox.MaxX.ToString("F6", inv)}\n20\n{bbox.MaxY.ToString("F6", inv)}\n30\n0.0");
        }
        sb.AppendLine("0\nENDSEC");

        // 2. Tables Section (LAYERS)
        sb.AppendLine("0\nSECTION\n2\nTABLES\n0\nTABLE\n2\nLAYER\n70\n" + document.Layers.Count);
        foreach (var layer in document.Layers)
        {
            var aci = ColorHexToAci(layer.ColorHex);
            var trueColor = HexToTrueColor(layer.ColorHex);
            WriteLayer(sb, layer.Name, layer.IsVisible ? aci : -aci, trueColor, layer.IsLocked);
        }
        sb.AppendLine("0\nENDTAB\n0\nENDSEC");

        // 3. Entities Section
        sb.AppendLine("0\nSECTION\n2\nENTITIES");

        foreach (var entity in document.Entities)
        {
            var layer = string.IsNullOrWhiteSpace(entity.Layer) ? "0" : entity.Layer;
            var aci = ColorHexToAci(entity.ColorHex);
            var trueColor = HexToTrueColor(entity.ColorHex);

            if (entity is CadLine line)
            {
                WriteLine(sb, layer, aci, trueColor, line.StartPoint.X, line.StartPoint.Y, line.EndPoint.X, line.EndPoint.Y, inv);
            }
            else if (entity is CadCircle circle)
            {
                WriteCircle(sb, layer, aci, trueColor, circle.Center.X, circle.Center.Y, circle.Radius, inv);
            }
            else if (entity is CadArc arc)
            {
                WriteArc(sb, layer, aci, trueColor, arc.Center.X, arc.Center.Y, arc.Radius, arc.StartAngleDegrees, arc.EndAngleDegrees, inv);
            }
            else if (entity is CadRectangle rect)
            {
                var v = rect.GetVertices();
                if (version == DxfExportVersion.R12)
                    WritePolylineR12(sb, layer, aci, trueColor, v, isClosed: true, inv);
                else
                    WriteLwPolyline(sb, layer, aci, trueColor, v, isClosed: true, inv);
            }
            else if (entity is CadPolyline poly)
            {
                if (version == DxfExportVersion.R12)
                    WritePolylineR12(sb, layer, aci, trueColor, poly.Vertices, poly.IsClosed, inv);
                else
                    WriteLwPolyline(sb, layer, aci, trueColor, poly.Vertices, poly.IsClosed, inv);
            }
            else if (entity is CadDimension dim)
            {
                WriteDimension(sb, layer, aci, trueColor, dim, inv);
            }
            else if (entity is CadText label)
            {
                WriteText(sb, layer, aci, trueColor, label.Position.X, label.Position.Y, label.Height,
                    label.Text, inv, label.RotationDegrees);
            }
        }

        sb.AppendLine("0\nENDSEC\n0\nEOF");
        return sb.ToString();
    }

    private static void WriteLayer(StringBuilder sb, string name, int colorIndex, int trueColor, bool locked = false)
    {
        sb.AppendLine($"0\nLAYER\n2\n{name}\n70\n{(locked ? 4 : 0)}\n62\n{colorIndex}\n420\n{trueColor}\n6\nCONTINUOUS");
    }

    private static void WriteLine(StringBuilder sb, string layer, int aci, int trueColor, double x1, double y1, double x2, double y2, CultureInfo inv)
    {
        sb.AppendLine($"0\nLINE\n8\n{layer}\n62\n{aci}\n420\n{trueColor}\n10\n{x1.ToString("F6", inv)}\n20\n{y1.ToString("F6", inv)}\n30\n0.0\n11\n{x2.ToString("F6", inv)}\n21\n{y2.ToString("F6", inv)}\n31\n0.0");
    }

    private static void WriteCircle(StringBuilder sb, string layer, int aci, int trueColor, double cx, double cy, double radius, CultureInfo inv)
    {
        sb.AppendLine($"0\nCIRCLE\n8\n{layer}\n62\n{aci}\n420\n{trueColor}\n10\n{cx.ToString("F6", inv)}\n20\n{cy.ToString("F6", inv)}\n30\n0.0\n40\n{radius.ToString("F6", inv)}");
    }

    private static void WriteArc(StringBuilder sb, string layer, int aci, int trueColor, double cx, double cy, double radius, double startAngle, double endAngle, CultureInfo inv)
    {
        sb.AppendLine($"0\nARC\n8\n{layer}\n62\n{aci}\n420\n{trueColor}\n10\n{cx.ToString("F6", inv)}\n20\n{cy.ToString("F6", inv)}\n30\n0.0\n40\n{radius.ToString("F6", inv)}\n50\n{startAngle.ToString("F6", inv)}\n51\n{endAngle.ToString("F6", inv)}");
    }

    private static void WriteLwPolyline(StringBuilder sb, string layer, int aci, int trueColor, IReadOnlyList<Point2D> vertices, bool isClosed, CultureInfo inv)
    {
        if (vertices.Count < 2) return;

        sb.AppendLine("0\nLWPOLYLINE");
        sb.AppendLine($"8\n{layer}");
        sb.AppendLine($"62\n{aci}");
        sb.AppendLine($"420\n{trueColor}");
        sb.AppendLine($"90\n{vertices.Count}");
        sb.AppendLine($"70\n{(isClosed ? "1" : "0")}");

        foreach (var v in vertices)
        {
            sb.AppendLine($"10\n{v.X.ToString("F6", inv)}");
            sb.AppendLine($"20\n{v.Y.ToString("F6", inv)}");
        }
    }

    private static void WritePolylineR12(StringBuilder sb, string layer, int aci, int trueColor, IReadOnlyList<Point2D> vertices, bool isClosed, CultureInfo inv)
    {
        if (vertices.Count < 2) return;

        sb.AppendLine("0\nPOLYLINE");
        sb.AppendLine($"8\n{layer}");
        sb.AppendLine($"62\n{aci}");
        sb.AppendLine($"420\n{trueColor}");
        sb.AppendLine($"70\n{(isClosed ? "1" : "0")}");

        foreach (var v in vertices)
        {
            sb.AppendLine("0\nVERTEX");
            sb.AppendLine($"8\n{layer}");
            sb.AppendLine($"62\n{aci}");
            sb.AppendLine($"420\n{trueColor}");
            sb.AppendLine($"10\n{v.X.ToString("F6", inv)}");
            sb.AppendLine($"20\n{v.Y.ToString("F6", inv)}");
            sb.AppendLine("30\n0.0");
        }

        sb.AppendLine("0\nSEQEND");
        sb.AppendLine($"8\n{layer}");
    }

    private static void WriteDimension(StringBuilder sb, string layer, int aci, int trueColor, CadDimension dim, CultureInfo inv)
    {
        sb.AppendLine("0\nDIMENSION");
        sb.AppendLine($"8\n{layer}");
        sb.AppendLine($"62\n{aci}");
        sb.AppendLine($"420\n{trueColor}");
        sb.AppendLine("70\n1"); // 1 = Aligned dimension
        if (!string.IsNullOrEmpty(dim.CustomTextOverride))
        {
            sb.AppendLine($"1\n{dim.CustomTextOverride}");
        }
        // Dimension line definition point
        sb.AppendLine($"10\n{dim.OffsetPoint.X.ToString("F6", inv)}");
        sb.AppendLine($"20\n{dim.OffsetPoint.Y.ToString("F6", inv)}");
        sb.AppendLine("30\n0.0");
        // Extension line start point
        sb.AppendLine($"13\n{dim.StartPoint.X.ToString("F6", inv)}");
        sb.AppendLine($"23\n{dim.StartPoint.Y.ToString("F6", inv)}");
        sb.AppendLine("33\n0.0");
        // Extension line end point
        sb.AppendLine($"14\n{dim.EndPoint.X.ToString("F6", inv)}");
        sb.AppendLine($"24\n{dim.EndPoint.Y.ToString("F6", inv)}");
        sb.AppendLine("34\n0.0");
    }

    private static void WriteText(StringBuilder sb, string layer, int aci, int trueColor, double x, double y, double height,
        string text, CultureInfo inv, double rotationDegrees = 0)
    {
        sb.AppendLine($"0\nTEXT\n8\n{layer}\n62\n{aci}\n420\n{trueColor}\n10\n{x.ToString("F6", inv)}\n20\n{y.ToString("F6", inv)}\n30\n0.0\n40\n{height.ToString("F6", inv)}\n50\n{rotationDegrees.ToString("F6", inv)}\n1\n{text}");
    }

    public static int HexToTrueColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return 0xFFFFFF;
        var trimmed = hex.Trim().TrimStart('#');
        if (trimmed.Length == 6 && int.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int val))
        {
            return val;
        }
        return 0xFFFFFF;
    }

    public static string TrueColorToHex(int trueColor)
    {
        int r = (trueColor >> 16) & 0xFF;
        int g = (trueColor >> 8) & 0xFF;
        int b = trueColor & 0xFF;
        return $"#{r:X2}{g:X2}{b:X2}";
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
        if (hex.Contains("D946EF") || hex.Contains("FF00FF") || hex.Contains("EC4899")) return 6;
        if (hex.Contains("FFFFFF")) return 7;
        if (hex.Contains("94A3B8") || hex.Contains("808080")) return 8;
        if (hex.Contains("64748B") || hex.Contains("CBD5E1")) return 9;
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

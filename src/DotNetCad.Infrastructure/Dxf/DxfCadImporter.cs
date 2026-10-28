using System.Globalization;
using System.Text.RegularExpressions;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;

namespace DotNetCad.Infrastructure.Dxf;

public static class DxfCadImporter
{
    private readonly record struct DxfCodePair(int Code, string Value, int LineIndex);

    public static CadDocument ImportFromDxf(string dxfContent, string documentTitle = "ImportedDrawing.dxf")
    {
        return ImportFromDxfWithReport(dxfContent, documentTitle).Document;
    }

    public static DxfImportResult ImportFromDxfWithReport(string dxfContent, string documentTitle = "ImportedDrawing.dxf")
    {
        ArgumentNullException.ThrowIfNull(dxfContent);

        var document = new CadDocument { Title = documentTitle };
        var pairs = ParseCodePairs(dxfContent);
        var warnings = new List<DxfImportWarning>();
        string detectedVersion = "UNKNOWN";
        int importedCount = 0;
        int skippedCount = 0;

        int index = 0;
        while (index < pairs.Count)
        {
            var pair = pairs[index];
            if (pair.Code == 0 && pair.Value == "SECTION")
            {
                index++;
                if (index < pairs.Count && pairs[index].Code == 2)
                {
                    var sectionName = pairs[index].Value;
                    index++;

                    if (sectionName == "HEADER")
                    {
                        index = ParseHeaderSection(pairs, index, document, ref detectedVersion);
                    }
                    else if (sectionName == "TABLES")
                    {
                        index = ParseTablesSection(pairs, index, document);
                    }
                    else if (sectionName == "ENTITIES")
                    {
                        index = ParseEntitiesSection(pairs, index, document, warnings, ref importedCount, ref skippedCount);
                    }
                }
            }
            else
            {
                index++;
            }
        }

        if (document.GetLayer(document.ActiveLayer) is not { IsVisible: true, IsLocked: false })
        {
            var usable = document.Layers.FirstOrDefault(l => l.IsVisible && !l.IsLocked)?.Name;
            if (usable != null)
            {
                document.ActiveLayer = usable;
            }
            else if (document.Layers.Count == 0)
            {
                var def = new CadLayer { Name = "0", IsProtected = true, ColorHex = "#FFFFFF" };
                document.Layers.Add(def);
                document.ActiveLayer = "0";
            }
            else
            {
                document.Layers[0].IsVisible = true;
                document.Layers[0].IsLocked = false;
                document.ActiveLayer = document.Layers[0].Name;
            }
        }

        return new DxfImportResult
        {
            Document = document,
            DetectedAcadVersion = detectedVersion,
            ImportedEntityCount = importedCount,
            SkippedEntityCount = skippedCount,
            Warnings = warnings
        };
    }

    private static List<DxfCodePair> ParseCodePairs(string content)
    {
        var list = new List<DxfCodePair>();
        using var reader = new StringReader(content);

        string? lineCode;
        int lineNum = 0;
        while ((lineCode = reader.ReadLine()) != null)
        {
            lineNum++;
            var codeStr = lineCode.Trim();
            if (string.IsNullOrEmpty(codeStr)) continue;

            if (int.TryParse(codeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
            {
                lineNum++;
                var val = reader.ReadLine()?.Trim() ?? "";
                list.Add(new DxfCodePair(code, val, lineNum));
            }
        }

        return list;
    }

    private static int ParseHeaderSection(List<DxfCodePair> pairs, int startIndex, CadDocument document, ref string detectedVersion)
    {
        int i = startIndex;
        while (i < pairs.Count)
        {
            var p = pairs[i];
            if (p.Code == 0 && p.Value == "ENDSEC") return i + 1;

            if (p.Code == 9 && p.Value == "$ACADVER" && i + 1 < pairs.Count && pairs[i + 1].Code == 1)
            {
                detectedVersion = pairs[i + 1].Value;
                i += 2;
                continue;
            }

            if (p.Code == 9 && p.Value == "$INSUNITS" && i + 1 < pairs.Count && pairs[i + 1].Code == 70)
            {
                if (int.TryParse(pairs[i + 1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int unitCode))
                {
                    document.Preferences.DisplayUnit = unitCode switch
                    {
                        1 => "in",
                        2 => "ft",
                        4 => "mm",
                        5 => "cm",
                        6 => "m",
                        _ => document.Preferences.DisplayUnit
                    };
                }
                i += 2;
                continue;
            }

            i++;
        }
        return i;
    }

    private static int ParseTablesSection(List<DxfCodePair> pairs, int startIndex, CadDocument document)
    {
        int i = startIndex;

        while (i < pairs.Count)
        {
            var p = pairs[i];
            if (p.Code == 0 && p.Value == "ENDSEC") return i + 1;

            if (p.Code == 0 && p.Value == "LAYER")
            {
                string layerName = "0";
                int colorAci = 7;
                int? trueColor = null;
                bool visible = true, locked = false;
                i++;

                while (i < pairs.Count && pairs[i].Code != 0)
                {
                    if (pairs[i].Code == 2) layerName = pairs[i].Value;
                    if (pairs[i].Code == 62 && int.TryParse(pairs[i].Value, out int c)) { colorAci = Math.Abs(c); visible = c >= 0; }
                    if (pairs[i].Code == 420 && int.TryParse(pairs[i].Value, out int tc)) { trueColor = tc; }
                    if (pairs[i].Code == 70 && int.TryParse(pairs[i].Value, out int flags)) locked = (flags & 4) != 0;
                    i++;
                }

                if (!string.IsNullOrWhiteSpace(layerName))
                {
                    var existing = document.Layers.FirstOrDefault(l => l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        existing = new CadLayer { Name = layerName, IsProtected = layerName == "0" };
                        document.Layers.Add(existing);
                    }
                    existing.ColorHex = trueColor.HasValue
                        ? DxfCadExporter.TrueColorToHex(trueColor.Value)
                        : DxfCadExporter.AciToColorHex(colorAci);
                    existing.IsVisible = visible;
                    existing.IsLocked = locked;
                }
                continue;
            }

            i++;
        }

        return i;
    }

    private static int ParseEntitiesSection(List<DxfCodePair> pairs, int startIndex, CadDocument document,
        List<DxfImportWarning> warnings, ref int importedCount, ref int skippedCount)
    {
        int i = startIndex;
        var inv = CultureInfo.InvariantCulture;

        while (i < pairs.Count)
        {
            var p = pairs[i];
            if (p.Code == 0 && p.Value == "ENDSEC") return i + 1;

            if (p.Code == 0)
            {
                int entityPairIndex = i;
                var entity = ParseSingleEntity(pairs, ref i, inv, warnings, entityPairIndex, ref skippedCount);
                if (entity != null)
                {
                    document.Entities.Add(entity);
                    importedCount++;
                }
                continue;
            }

            i++;
        }

        return i;
    }

    private static ICadEntity? ParseSingleEntity(List<DxfCodePair> pairs, ref int i, CultureInfo inv,
        List<DxfImportWarning> warnings, int entityPairIndex, ref int skippedCount)
    {
        var entityType = pairs[i].Value;
        i++;

        string layer = "Geometry";
        int colorAci = 7;
        int? trueColor = null;

        if (entityType == "LINE")
        {
            double x1 = 0, y1 = 0, x2 = 0, y2 = 0;
            bool hasNonZeroZ = false;

            while (i < pairs.Count && pairs[i].Code != 0)
            {
                if (pairs[i].Code == 8) layer = pairs[i].Value;
                if (pairs[i].Code == 62 && int.TryParse(pairs[i].Value, out int c)) colorAci = Math.Abs(c);
                if (pairs[i].Code == 420 && int.TryParse(pairs[i].Value, out int tc)) trueColor = tc;
                if (pairs[i].Code == 10) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out x1);
                if (pairs[i].Code == 20) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out y1);
                if (pairs[i].Code == 30 && double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out double z1) && Math.Abs(z1) > 1e-4) hasNonZeroZ = true;
                if (pairs[i].Code == 11) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out x2);
                if (pairs[i].Code == 21) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out y2);
                if (pairs[i].Code == 31 && double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out double z2) && Math.Abs(z2) > 1e-4) hasNonZeroZ = true;
                i++;
            }

            if (hasNonZeroZ)
            {
                warnings.Add(new DxfImportWarning("3D_PROJECTION", "LINE", entityPairIndex, "Coordenadas Z descartadas; entidade projetada no plano 2D (Z=0)."));
            }

            var colorHex = trueColor.HasValue ? DxfCadExporter.TrueColorToHex(trueColor.Value) : DxfCadExporter.AciToColorHex(colorAci);
            return new CadLine(new Point2D(x1, y1), new Point2D(x2, y2), layer, colorHex);
        }
        else if (entityType == "CIRCLE")
        {
            double cx = 0, cy = 0, radius = 1.0;
            while (i < pairs.Count && pairs[i].Code != 0)
            {
                if (pairs[i].Code == 8) layer = pairs[i].Value;
                if (pairs[i].Code == 62 && int.TryParse(pairs[i].Value, out int c)) colorAci = Math.Abs(c);
                if (pairs[i].Code == 420 && int.TryParse(pairs[i].Value, out int tc)) trueColor = tc;
                if (pairs[i].Code == 10) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out cx);
                if (pairs[i].Code == 20) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out cy);
                if (pairs[i].Code == 40) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out radius);
                i++;
            }

            var colorHex = trueColor.HasValue ? DxfCadExporter.TrueColorToHex(trueColor.Value) : DxfCadExporter.AciToColorHex(colorAci);
            return new CadCircle(new Point2D(cx, cy), radius, layer, colorHex);
        }
        else if (entityType == "ARC")
        {
            double cx = 0, cy = 0, radius = 1.0, startAngle = 0, endAngle = 0;
            while (i < pairs.Count && pairs[i].Code != 0)
            {
                if (pairs[i].Code == 8) layer = pairs[i].Value;
                if (pairs[i].Code == 62 && int.TryParse(pairs[i].Value, out int c)) colorAci = Math.Abs(c);
                if (pairs[i].Code == 420 && int.TryParse(pairs[i].Value, out int tc)) trueColor = tc;
                if (pairs[i].Code == 10) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out cx);
                if (pairs[i].Code == 20) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out cy);
                if (pairs[i].Code == 40) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out radius);
                if (pairs[i].Code == 50) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out startAngle);
                if (pairs[i].Code == 51) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out endAngle);
                i++;
            }

            var colorHex = trueColor.HasValue ? DxfCadExporter.TrueColorToHex(trueColor.Value) : DxfCadExporter.AciToColorHex(colorAci);
            return new CadArc(new Point2D(cx, cy), radius, startAngle, endAngle, layer, colorHex);
        }
        else if (entityType == "LWPOLYLINE")
        {
            var vertices = new List<Point2D>();
            bool isClosed = false;
            double currX = 0, currY = 0;
            bool hasX = false;
            bool hasBulge = false;

            while (i < pairs.Count && pairs[i].Code != 0)
            {
                if (pairs[i].Code == 8) layer = pairs[i].Value;
                if (pairs[i].Code == 62 && int.TryParse(pairs[i].Value, out int c)) colorAci = Math.Abs(c);
                if (pairs[i].Code == 420 && int.TryParse(pairs[i].Value, out int tc)) trueColor = tc;
                if (pairs[i].Code == 70 && int.TryParse(pairs[i].Value, out int flag)) isClosed = (flag & 1) == 1;

                if (pairs[i].Code == 42 && double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out double bulge) && Math.Abs(bulge) > 1e-4)
                {
                    hasBulge = true;
                }

                if (pairs[i].Code == 10 && double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out currX))
                {
                    hasX = true;
                }
                if (pairs[i].Code == 20 && double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out currY))
                {
                    if (hasX)
                    {
                        vertices.Add(new Point2D(currX, currY));
                        hasX = false;
                    }
                }
                i++;
            }

            if (hasBulge)
            {
                warnings.Add(new DxfImportWarning("BULGE_SIMPLIFIED", "LWPOLYLINE", entityPairIndex, "LWPOLYLINE com curvatura de arco (bulge) simplificada para segmentos lineares retos."));
            }

            var colorHex = trueColor.HasValue ? DxfCadExporter.TrueColorToHex(trueColor.Value) : DxfCadExporter.AciToColorHex(colorAci);
            return new CadPolyline(vertices, isClosed, layer, colorHex);
        }
        else if (entityType == "POLYLINE")
        {
            // AutoCAD R12 Classic Polyline with VERTEX sub-entities and SEQEND
            var vertices = new List<Point2D>();
            bool isClosed = false;

            // 1. Read POLYLINE header pairs
            while (i < pairs.Count && pairs[i].Code != 0)
            {
                if (pairs[i].Code == 8) layer = pairs[i].Value;
                if (pairs[i].Code == 62 && int.TryParse(pairs[i].Value, out int c)) colorAci = Math.Abs(c);
                if (pairs[i].Code == 420 && int.TryParse(pairs[i].Value, out int tc)) trueColor = tc;
                if (pairs[i].Code == 70 && int.TryParse(pairs[i].Value, out int flag)) isClosed = (flag & 1) == 1;
                i++;
            }

            // 2. Loop VERTEX records until SEQEND
            while (i < pairs.Count)
            {
                if (pairs[i].Code == 0 && pairs[i].Value == "VERTEX")
                {
                    i++;
                    double vx = 0, vy = 0;
                    while (i < pairs.Count && pairs[i].Code != 0)
                    {
                        if (pairs[i].Code == 10) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out vx);
                        if (pairs[i].Code == 20) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out vy);
                        i++;
                    }
                    vertices.Add(new Point2D(vx, vy));
                }
                else if (pairs[i].Code == 0 && pairs[i].Value == "SEQEND")
                {
                    i++;
                    // Skip SEQEND pairs
                    while (i < pairs.Count && pairs[i].Code != 0) i++;
                    break;
                }
                else if (pairs[i].Code == 0)
                {
                    // Unexpected entity without SEQEND
                    break;
                }
                else
                {
                    i++;
                }
            }

            var colorHex = trueColor.HasValue ? DxfCadExporter.TrueColorToHex(trueColor.Value) : DxfCadExporter.AciToColorHex(colorAci);
            return new CadPolyline(vertices, isClosed, layer, colorHex);
        }
        else if (entityType == "DIMENSION")
        {
            double x10 = 0, y10 = 0; // Dimension line definition point
            double x13 = 0, y13 = 0; // Extension line start
            double x14 = 0, y14 = 0; // Extension line end
            string textOverride = string.Empty;

            while (i < pairs.Count && pairs[i].Code != 0)
            {
                if (pairs[i].Code == 8) layer = pairs[i].Value;
                if (pairs[i].Code == 62 && int.TryParse(pairs[i].Value, out int c)) colorAci = Math.Abs(c);
                if (pairs[i].Code == 420 && int.TryParse(pairs[i].Value, out int tc)) trueColor = tc;
                if (pairs[i].Code == 1) textOverride = pairs[i].Value;
                if (pairs[i].Code == 10) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out x10);
                if (pairs[i].Code == 20) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out y10);
                if (pairs[i].Code == 13) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out x13);
                if (pairs[i].Code == 23) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out y13);
                if (pairs[i].Code == 14) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out x14);
                if (pairs[i].Code == 24) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out y14);
                i++;
            }

            var startPoint = new Point2D(x13, y13);
            var endPoint = new Point2D(x14, y14);
            var offsetPoint = new Point2D(x10, y10);

            var colorHex = trueColor.HasValue ? DxfCadExporter.TrueColorToHex(trueColor.Value) : DxfCadExporter.AciToColorHex(colorAci);

            var dim = new CadDimension(startPoint, endPoint, offsetPoint, layer, colorHex);
            if (!string.IsNullOrWhiteSpace(textOverride) && textOverride != "<>")
            {
                dim.CustomTextOverride = textOverride;
            }
            return dim;
        }
        else if (entityType == "TEXT")
        {
            double x = 0, y = 0, height = 1, rotation = 0;
            string text = string.Empty;
            while (i < pairs.Count && pairs[i].Code != 0)
            {
                if (pairs[i].Code == 8) layer = pairs[i].Value;
                if (pairs[i].Code == 62 && int.TryParse(pairs[i].Value, out int c)) colorAci = Math.Abs(c);
                if (pairs[i].Code == 420 && int.TryParse(pairs[i].Value, out int tc)) trueColor = tc;
                if (pairs[i].Code == 10) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out x);
                if (pairs[i].Code == 20) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out y);
                if (pairs[i].Code == 40) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out height);
                if (pairs[i].Code == 50) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out rotation);
                if (pairs[i].Code == 1) text = pairs[i].Value;
                i++;
            }

            if (string.IsNullOrWhiteSpace(text) || !double.IsFinite(height) || height < 0.05 ||
                !double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(rotation)) return null;

            var colorHex = trueColor.HasValue ? DxfCadExporter.TrueColorToHex(trueColor.Value) : DxfCadExporter.AciToColorHex(colorAci);
            return new CadText
            {
                Position = new Point2D(x, y),
                Text = text,
                Height = height,
                RotationDegrees = rotation,
                Layer = layer,
                ColorHex = colorHex
            };
        }
        else if (entityType == "MTEXT")
        {
            double x = 0, y = 0, height = 1, rotation = 0;
            var textParts = new List<string>();

            while (i < pairs.Count && pairs[i].Code != 0)
            {
                if (pairs[i].Code == 8) layer = pairs[i].Value;
                if (pairs[i].Code == 62 && int.TryParse(pairs[i].Value, out int c)) colorAci = Math.Abs(c);
                if (pairs[i].Code == 420 && int.TryParse(pairs[i].Value, out int tc)) trueColor = tc;
                if (pairs[i].Code == 10) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out x);
                if (pairs[i].Code == 20) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out y);
                if (pairs[i].Code == 40) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out height);
                if (pairs[i].Code == 50) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out rotation);
                if (pairs[i].Code == 3 || pairs[i].Code == 1) textParts.Add(pairs[i].Value);
                i++;
            }

            var rawText = string.Join("", textParts);
            var cleanText = CleanMTextFormatting(rawText);

            if (string.IsNullOrWhiteSpace(cleanText)) return null;

            warnings.Add(new DxfImportWarning("MTEXT_CONVERTED", "MTEXT", entityPairIndex, "Entidade MTEXT convertida para CadText 2D (formatação simplificada)."));

            var colorHex = trueColor.HasValue ? DxfCadExporter.TrueColorToHex(trueColor.Value) : DxfCadExporter.AciToColorHex(colorAci);
            return new CadText
            {
                Position = new Point2D(x, y),
                Text = cleanText,
                Height = height > 0.05 ? height : 1.0,
                RotationDegrees = rotation,
                Layer = layer,
                ColorHex = colorHex
            };
        }
        else
        {
            // Unsupported entity (e.g. HATCH, SPLINE, 3DFACE, SOLID, LEADER, etc.)
            skippedCount++;
            warnings.Add(new DxfImportWarning("UNSUPPORTED_ENTITY", entityType, entityPairIndex, $"Entidade '{entityType}' não suportada em CAD 2D puro; ignorada com segurança."));

            // Skip entity pairs until next 0
            while (i < pairs.Count && pairs[i].Code != 0) i++;
            return null;
        }
    }

    private static string CleanMTextFormatting(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        // Replace \P and \p with newline
        var text = Regex.Replace(raw, @"\\[Pp]", "\n");
        // Replace \~ with space
        text = text.Replace(@"\~", " ");
        // Strip format codes like \A1;, \C1;, \fArial|b0|i0|c0|p34;, \H1.5;, \W0.8;
        text = Regex.Replace(text, @"\\[A-Za-z0-9]+[^;]*;", "");
        // Remove braces { and }
        text = text.Replace("{", "").Replace("}", "");

        return text.Trim();
    }
}

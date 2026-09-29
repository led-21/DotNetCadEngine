using System.Globalization;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;

namespace DotNetCad.Infrastructure.Dxf;

public sealed class DxfImportPayload
{
    public string DocumentTitle { get; set; } = string.Empty;
    public List<CadLayer> Layers { get; set; } = [];
    public List<ICadEntity> Entities { get; set; } = [];
    public int IgnoredEntitiesCount { get; set; }
}

public static class DxfCadImporter
{
    private readonly record struct DxfCodePair(int Code, string Value);

    public static CadDocument ImportFromDxf(string dxfContent, string documentTitle = "ImportedDrawing.dxf")
    {
        ArgumentNullException.ThrowIfNull(dxfContent);

        var document = new CadDocument { Title = documentTitle };
        var pairs = ParseCodePairs(dxfContent);

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

                    if (sectionName == "TABLES")
                    {
                        index = ParseTablesSection(pairs, index, document);
                    }
                    else if (sectionName == "ENTITIES")
                    {
                        index = ParseEntitiesSection(pairs, index, document);
                    }
                }
            }
            else
            {
                index++;
            }
        }

        return document;
    }

    private static List<DxfCodePair> ParseCodePairs(string content)
    {
        var list = new List<DxfCodePair>();
        using var reader = new StringReader(content);

        string? lineCode;
        while ((lineCode = reader.ReadLine()) != null)
        {
            var codeStr = lineCode.Trim();
            if (string.IsNullOrEmpty(codeStr)) continue;

            if (int.TryParse(codeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
            {
                var val = reader.ReadLine()?.Trim() ?? "";
                list.Add(new DxfCodePair(code, val));
            }
        }

        return list;
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
                i++;

                while (i < pairs.Count && pairs[i].Code != 0)
                {
                    if (pairs[i].Code == 2) layerName = pairs[i].Value;
                    if (pairs[i].Code == 62 && int.TryParse(pairs[i].Value, out int c)) colorAci = Math.Abs(c);
                    i++;
                }

                if (!string.IsNullOrWhiteSpace(layerName))
                {
                    var existing = document.Layers.FirstOrDefault(l => l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        document.Layers.Add(new CadLayer
                        {
                            Name = layerName,
                            ColorHex = DxfCadExporter.AciToColorHex(colorAci),
                            IsProtected = layerName == "0"
                        });
                    }
                }
                continue;
            }

            i++;
        }

        return i;
    }

    private static int ParseEntitiesSection(List<DxfCodePair> pairs, int startIndex, CadDocument document)
    {
        int i = startIndex;
        var inv = CultureInfo.InvariantCulture;

        while (i < pairs.Count)
        {
            var p = pairs[i];
            if (p.Code == 0 && p.Value == "ENDSEC") return i + 1;

            if (p.Code == 0)
            {
                var entity = ParseSingleEntity(pairs, ref i, inv);
                if (entity != null)
                {
                    document.Entities.Add(entity);
                    continue;
                }
            }

            i++;
        }

        return i;
    }

    private static ICadEntity? ParseSingleEntity(List<DxfCodePair> pairs, ref int i, CultureInfo inv)
    {
        var entityType = pairs[i].Value;
        i++;

        string layer = "Geometry";
        int colorAci = 7;

        if (entityType == "LINE")
        {
            double x1 = 0, y1 = 0, x2 = 0, y2 = 0;
            while (i < pairs.Count && pairs[i].Code != 0)
            {
                if (pairs[i].Code == 8) layer = pairs[i].Value;
                if (pairs[i].Code == 62 && int.TryParse(pairs[i].Value, out int c)) colorAci = Math.Abs(c);
                if (pairs[i].Code == 10) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out x1);
                if (pairs[i].Code == 20) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out y1);
                if (pairs[i].Code == 11) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out x2);
                if (pairs[i].Code == 21) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out y2);
                i++;
            }

            return new CadLine(new Point2D(x1, y1), new Point2D(x2, y2), layer, DxfCadExporter.AciToColorHex(colorAci));
        }
        else if (entityType == "CIRCLE")
        {
            double cx = 0, cy = 0, radius = 1.0;
            while (i < pairs.Count && pairs[i].Code != 0)
            {
                if (pairs[i].Code == 8) layer = pairs[i].Value;
                if (pairs[i].Code == 62 && int.TryParse(pairs[i].Value, out int c)) colorAci = Math.Abs(c);
                if (pairs[i].Code == 10) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out cx);
                if (pairs[i].Code == 20) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out cy);
                if (pairs[i].Code == 40) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out radius);
                i++;
            }

            return new CadCircle(new Point2D(cx, cy), radius, layer, DxfCadExporter.AciToColorHex(colorAci));
        }
        else if (entityType == "ARC")
        {
            double cx = 0, cy = 0, radius = 1.0, startAngle = 0, endAngle = 0;
            while (i < pairs.Count && pairs[i].Code != 0)
            {
                if (pairs[i].Code == 8) layer = pairs[i].Value;
                if (pairs[i].Code == 62 && int.TryParse(pairs[i].Value, out int c)) colorAci = Math.Abs(c);
                if (pairs[i].Code == 10) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out cx);
                if (pairs[i].Code == 20) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out cy);
                if (pairs[i].Code == 40) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out radius);
                if (pairs[i].Code == 50) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out startAngle);
                if (pairs[i].Code == 51) double.TryParse(pairs[i].Value, NumberStyles.Float, inv, out endAngle);
                i++;
            }

            return new CadArc(new Point2D(cx, cy), radius, startAngle, endAngle, layer, DxfCadExporter.AciToColorHex(colorAci));
        }
        else if (entityType == "LWPOLYLINE" || entityType == "POLYLINE")
        {
            var vertices = new List<Point2D>();
            bool isClosed = false;
            double currX = 0, currY = 0;
            bool hasX = false;

            while (i < pairs.Count && pairs[i].Code != 0)
            {
                if (pairs[i].Code == 8) layer = pairs[i].Value;
                if (pairs[i].Code == 62 && int.TryParse(pairs[i].Value, out int c)) colorAci = Math.Abs(c);
                if (pairs[i].Code == 70 && int.TryParse(pairs[i].Value, out int flag)) isClosed = (flag & 1) == 1;

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

            return new CadPolyline(vertices, isClosed, layer, DxfCadExporter.AciToColorHex(colorAci));
        }

        return null;
    }
}

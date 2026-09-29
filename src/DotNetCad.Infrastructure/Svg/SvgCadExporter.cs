using System.Globalization;
using System.Text;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;

namespace DotNetCad.Infrastructure.Svg;

public static class SvgCadExporter
{
    public static string ExportToSvg(CadDocument document, double paddingMeters = 5.0)
    {
        ArgumentNullException.ThrowIfNull(document);

        var bbox = document.BoundingBox;
        if (bbox.IsEmpty)
        {
            bbox = new BoundingBox2D(-10, -10, 10, 10);
        }

        var minX = bbox.MinX - paddingMeters;
        var minY = bbox.MinY - paddingMeters;
        var maxX = bbox.MaxX + paddingMeters;
        var maxY = bbox.MaxY + paddingMeters;

        var width = maxX - minX;
        var height = maxY - minY;

        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();

        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine($"<svg version=\"1.1\" xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width.ToString("F3", inv)} {height.ToString("F3", inv)}\">");
        sb.AppendLine($"  <rect width=\"100%\" height=\"100%\" fill=\"#0F172A\" />"); // Dark CAD background

        // CAD to SVG transformation: Y is inverted (CAD Y grows upwards, SVG Y downwards)
        double ToSvgX(double worldX) => worldX - minX;
        double ToSvgY(double worldY) => maxY - worldY;

        // Group entities by layer
        var grouped = document.Entities.GroupBy(e => e.Layer);

        foreach (var group in grouped)
        {
            var layerName = group.Key;
            var layer = document.GetLayer(layerName);
            var isVisible = layer?.IsVisible ?? true;
            if (!isVisible) continue;

            sb.AppendLine($"  <g id=\"Layer_{layerName}\" stroke-linecap=\"round\" stroke-linejoin=\"round\">");

            foreach (var entity in group)
            {
                var stroke = string.IsNullOrWhiteSpace(entity.ColorHex) ? "#38BDF8" : entity.ColorHex;
                var strokeWidth = Math.Max(0.05, (entity.LineThickness * 0.1)).ToString("F2", inv);

                if (entity is CadLine line)
                {
                    var x1 = ToSvgX(line.StartPoint.X).ToString("F3", inv);
                    var y1 = ToSvgY(line.StartPoint.Y).ToString("F3", inv);
                    var x2 = ToSvgX(line.EndPoint.X).ToString("F3", inv);
                    var y2 = ToSvgY(line.EndPoint.Y).ToString("F3", inv);
                    sb.AppendLine($"    <line x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\" />");
                }
                else if (entity is CadCircle circle)
                {
                    var cx = ToSvgX(circle.Center.X).ToString("F3", inv);
                    var cy = ToSvgY(circle.Center.Y).ToString("F3", inv);
                    var r = circle.Radius.ToString("F3", inv);
                    sb.AppendLine($"    <circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"none\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\" />");
                }
                else if (entity is CadArc arc)
                {
                    var sx = ToSvgX(arc.StartPoint.X).ToString("F3", inv);
                    var sy = ToSvgY(arc.StartPoint.Y).ToString("F3", inv);
                    var ex = ToSvgX(arc.EndPoint.X).ToString("F3", inv);
                    var ey = ToSvgY(arc.EndPoint.Y).ToString("F3", inv);
                    var r = arc.Radius.ToString("F3", inv);
                    var largeArc = arc.SweepAngleDegrees > 180.0 ? "1" : "0";
                    // In SVG inverted Y, CCW CAD angle becomes CW SVG angle, sweep-flag 0
                    sb.AppendLine($"    <path d=\"M {sx} {sy} A {r} {r} 0 {largeArc} 0 {ex} {ey}\" fill=\"none\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\" />");
                }
                else if (entity is CadRectangle rect)
                {
                    var pts = rect.GetVertices();
                    var pointsStr = string.Join(" ", pts.Select(p => $"{ToSvgX(p.X).ToString("F3", inv)},{ToSvgY(p.Y).ToString("F3", inv)}"));
                    sb.AppendLine($"    <polygon points=\"{pointsStr}\" fill=\"none\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\" />");
                }
                else if (entity is CadPolyline poly)
                {
                    if (poly.Vertices.Count >= 2)
                    {
                        var pointsStr = string.Join(" ", poly.Vertices.Select(p => $"{ToSvgX(p.X).ToString("F3", inv)},{ToSvgY(p.Y).ToString("F3", inv)}"));
                        var tag = poly.IsClosed ? "polygon" : "polyline";
                        sb.AppendLine($"    <{tag} points=\"{pointsStr}\" fill=\"none\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\" />");
                    }
                }
                else if (entity is CadDimension dim)
                {
                    var (d1, d2) = dim.GetDimensionLinePoints();
                    var sx1 = ToSvgX(dim.StartPoint.X).ToString("F3", inv);
                    var sy1 = ToSvgY(dim.StartPoint.Y).ToString("F3", inv);
                    var dx1 = ToSvgX(d1.X).ToString("F3", inv);
                    var dy1 = ToSvgY(d1.Y).ToString("F3", inv);

                    var sx2 = ToSvgX(dim.EndPoint.X).ToString("F3", inv);
                    var sy2 = ToSvgY(dim.EndPoint.Y).ToString("F3", inv);
                    var dx2 = ToSvgX(d2.X).ToString("F3", inv);
                    var dy2 = ToSvgY(d2.Y).ToString("F3", inv);

                    // Extension lines
                    sb.AppendLine($"    <line x1=\"{sx1}\" y1=\"{sy1}\" x2=\"{dx1}\" y2=\"{dy1}\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\" stroke-dasharray=\"0.1,0.1\" />");
                    sb.AppendLine($"    <line x1=\"{sx2}\" y1=\"{sy2}\" x2=\"{dx2}\" y2=\"{dy2}\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\" stroke-dasharray=\"0.1,0.1\" />");

                    // Dimension line
                    sb.AppendLine($"    <line x1=\"{dx1}\" y1=\"{dy1}\" x2=\"{dx2}\" y2=\"{dy2}\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\" />");

                    var mid = new Point2D((d1.X + d2.X) / 2.0, (d1.Y + d2.Y) / 2.0);
                    var tx = ToSvgX(mid.X).ToString("F3", inv);
                    var ty = (ToSvgY(mid.Y) - 0.2).ToString("F3", inv);
                    sb.AppendLine($"    <text x=\"{tx}\" y=\"{ty}\" fill=\"{stroke}\" font-size=\"0.4\" text-anchor=\"middle\" font-family=\"monospace\">{dim.FormattedText}</text>");
                }
            }

            sb.AppendLine("  </g>");
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }
}

using System.Globalization;
using DotNetCad.Core.Geometry;

namespace DotNetCad.Core.Editor;

public static class CadCoordinateParser
{
    public static bool TryParse(string text, Point2D? origin, out Point2D point, out string error)
    {
        return TryParse(text, origin, null, null, out point, out error);
    }

    public static bool TryParse(string text, Point2D? origin, Point2D? cursorHint, double? snapAngleDeg, out Point2D point, out string error)
    {
        point = default;
        error = "Use X;Y, @dx;dy, @distance<angle or direct distance (meters).";
        if (string.IsNullOrWhiteSpace(text)) return false;

        text = text.Trim();
        var relative = text.StartsWith('@');
        if (relative)
        {
            if (origin == null)
            {
                error = "Base point required before using relative coordinates.";
                return false;
            }
            text = text[1..];
        }

        bool Number(string value, out double result)
        {
            var v = value.Trim();
            if (v.EndsWith('m') || v.EndsWith('M')) v = v[..^1].Trim();
            else if (v.EndsWith('°')) v = v[..^1].Trim();
            else if (v.EndsWith("deg", StringComparison.OrdinalIgnoreCase)) v = v[..^3].Trim();

            return double.TryParse(v.Replace(',', '.'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out result) && double.IsFinite(result);
        }

        // 1. Direct Distance Entry: single number with cursor indicating direction
        var isTwoPointWithComma = !text.Contains(';') && text.Contains(',') && text.Contains('.');
        if (!text.Contains(';') && !isTwoPointWithComma && !text.Contains('<'))
        {
            if (Number(text, out var directDist))
            {
                if (directDist <= 0)
                {
                    error = "Distance must be greater than zero.";
                    return false;
                }
                if (origin == null || cursorHint == null)
                {
                    if (relative || cursorHint != null)
                    {
                        error = "Specify an origin and position cursor to indicate distance direction.";
                        return false;
                    }
                }
                else
                {
                    var dx = cursorHint.Value.X - origin.Value.X;
                    var dy = cursorHint.Value.Y - origin.Value.Y;
                    var cursorDist = Math.Sqrt((dx * dx) + (dy * dy));
                    var angleRad = cursorDist > 0.0001 ? Math.Atan2(dy, dx) : 0.0;

                    if (snapAngleDeg is double snapDeg && snapDeg > 0)
                    {
                        var angleDeg = angleRad * 180.0 / Math.PI;
                        var snappedDeg = Math.Round(angleDeg / snapDeg) * snapDeg;
                        angleRad = snappedDeg * Math.PI / 180.0;
                    }

                    point = new Point2D(origin.Value.X + (directDist * Math.Cos(angleRad)), origin.Value.Y + (directDist * Math.Sin(angleRad)));
                    error = string.Empty;
                    return true;
                }
            }
        }

        double x, y;
        if (relative && text.Contains('<'))
        {
            var parts = text.Split('<');
            if (parts.Length != 2 || !Number(parts[0], out var distance) || !Number(parts[1], out var angle) || distance <= 0) return false;
            var radians = (angle % 360) * Math.PI / 180;
            x = distance * Math.Cos(radians);
            y = distance * Math.Sin(radians);
        }
        else
        {
            var parts = text.Split(text.Contains(';') ? ';' : ',');
            if (parts.Length != 2 || !Number(parts[0], out x) || !Number(parts[1], out y)) return false;
        }

        if (relative)
        {
            x += origin!.Value.X;
            y += origin.Value.Y;
        }

        if (!double.IsFinite(x) || !double.IsFinite(y)) return false;

        point = new Point2D(x, y);
        error = string.Empty;
        return true;
    }
}

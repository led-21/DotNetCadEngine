namespace DotNetCad.Core.Geometry;

public sealed class GeometryValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }
    public string? SuggestedFix { get; }

    private GeometryValidationResult(bool isValid, string? errorMessage = null, string? suggestedFix = null)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
        SuggestedFix = suggestedFix;
    }

    public static GeometryValidationResult Success() => new(true);

    public static GeometryValidationResult Failure(string errorMessage, string suggestedFix)
        => new(false, errorMessage, suggestedFix);

    public override string ToString()
    {
        if (IsValid) return "Valid";
        return $"{ErrorMessage} ({SuggestedFix})";
    }
}

public static class GeometryValidator
{
    private const double DefaultEpsilon = 1e-4;

    public static GeometryValidationResult ValidatePoint(Point2D pt)
    {
        if (double.IsNaN(pt.X) || double.IsNaN(pt.Y) || double.IsInfinity(pt.X) || double.IsInfinity(pt.Y))
        {
            return GeometryValidationResult.Failure(
                "Point coordinates are invalid (NaN or Infinity).",
                "Check numeric input or click location.");
        }

        return GeometryValidationResult.Success();
    }

    public static GeometryValidationResult ValidateSegment(Point2D p1, Point2D p2, double minLengthMeters = 0.01)
    {
        var v1 = ValidatePoint(p1);
        if (!v1.IsValid) return v1;

        var v2 = ValidatePoint(p2);
        if (!v2.IsValid) return v2;

        var length = p1.DistanceTo(p2);
        if (length < minLengthMeters)
        {
            return GeometryValidationResult.Failure(
                $"Degenerate segment: length {length:F3} m is below the minimum allowed ({minLengthMeters:F2} m).",
                "Ensure start and end points are sufficiently separated.");
        }

        return GeometryValidationResult.Success();
    }

    public static GeometryValidationResult ValidateDimensions(double length, double width, double minDimension = 0.05)
    {
        if (double.IsNaN(length) || double.IsInfinity(length) || length < minDimension)
        {
            return GeometryValidationResult.Failure(
                $"Length ({length:F2} m) must be positive and greater than {minDimension:F2} m.",
                "Enter a valid positive number for length.");
        }

        if (double.IsNaN(width) || double.IsInfinity(width) || width < minDimension)
        {
            return GeometryValidationResult.Failure(
                $"Width ({width:F2} m) must be positive and greater than {minDimension:F2} m.",
                "Enter a valid positive number for width.");
        }

        return GeometryValidationResult.Success();
    }

    public static GeometryValidationResult ValidateCircle(Point2D center, double radius, double minRadius = 0.05)
    {
        var vCenter = ValidatePoint(center);
        if (!vCenter.IsValid) return vCenter;

        if (double.IsNaN(radius) || double.IsInfinity(radius) || radius < minRadius)
        {
            return GeometryValidationResult.Failure(
                $"Radius ({radius:F2} m) must be greater than {minRadius:F2} m.",
                "Drag or enter a valid positive radius.");
        }

        return GeometryValidationResult.Success();
    }

    public static GeometryValidationResult ValidatePolygon(IReadOnlyList<Point2D>? vertices, double minArea = 0.01)
    {
        if (vertices == null || vertices.Count < 3)
        {
            return GeometryValidationResult.Failure(
                "Invalid polygon: must contain at least 3 vertices.",
                "Provide at least 3 distinct points to form a closed shape.");
        }

        for (int i = 0; i < vertices.Count; i++)
        {
            var vPt = ValidatePoint(vertices[i]);
            if (!vPt.IsValid) return vPt;

            var next = vertices[(i + 1) % vertices.Count];
            if (vertices[i].DistanceTo(next) < DefaultEpsilon)
            {
                return GeometryValidationResult.Failure(
                    $"Duplicate consecutive vertices detected at index {i}.",
                    "Remove duplicate vertex or separate the coordinates.");
            }
        }

        int n = vertices.Count;
        for (int i = 0; i < n; i++)
        {
            var a1 = vertices[i];
            var a2 = vertices[(i + 1) % n];

            for (int j = i + 1; j < n; j++)
            {
                if (Math.Abs(i - j) <= 1 || (i == 0 && j == n - 1))
                    continue;

                var b1 = vertices[j];
                var b2 = vertices[(j + 1) % n];

                if (SegmentsIntersectStrict(a1, a2, b1, b2))
                {
                    return GeometryValidationResult.Failure(
                        $"Self-intersection detected between edges [{i}-{i + 1}] and [{j}-{(j + 1) % n}].",
                        "Adjust polygon vertices to eliminate crossing edges.");
                }
            }
        }

        var area = PolygonMath.CalculatePolygonArea(vertices);
        if (double.IsNaN(area) || area < minArea)
        {
            return GeometryValidationResult.Failure(
                $"Polygon area ({area:F3} m²) is below the minimum allowed ({minArea:F2} m²).",
                "Ensure vertices describe a non-collapsed surface.");
        }

        return GeometryValidationResult.Success();
    }

    public static bool SegmentsIntersectStrict(Point2D a1, Point2D a2, Point2D b1, Point2D b2)
    {
        double Orientation(Point2D p, Point2D q, Point2D r)
        {
            double val = (q.Y - p.Y) * (r.X - q.X) - (q.X - p.X) * (r.Y - q.Y);
            if (Math.Abs(val) < 1e-9) return 0;
            return val > 0 ? 1 : 2;
        }

        bool OnSegment(Point2D p, Point2D q, Point2D r)
        {
            return q.X <= Math.Max(p.X, r.X) + 1e-9 && q.X >= Math.Min(p.X, r.X) - 1e-9 &&
                   q.Y <= Math.Max(p.Y, r.Y) + 1e-9 && q.Y >= Math.Min(p.Y, r.Y) - 1e-9;
        }

        double o1 = Orientation(a1, a2, b1);
        double o2 = Orientation(a1, a2, b2);
        double o3 = Orientation(b1, b2, a1);
        double o4 = Orientation(b1, b2, a2);

        if (o1 != o2 && o3 != o4)
            return true;

        if (o1 == 0 && OnSegment(a1, b1, a2)) return true;
        if (o2 == 0 && OnSegment(a1, b2, a2)) return true;
        if (o3 == 0 && OnSegment(b1, a1, b2)) return true;
        if (o4 == 0 && OnSegment(b1, a2, b2)) return true;

        return false;
    }
}

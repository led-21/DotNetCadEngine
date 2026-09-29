using DotNetCad.Core.Geometry;

namespace DotNetCad.Core.Entities;

public enum CadDimensionType
{
    Aligned,
    Horizontal,
    Vertical,
    Radial
}

public sealed class CadDimension : CadEntity
{
    public override CadEntityType EntityType => CadEntityType.Dimension;

    public string Identifier { get; set; } = "DIM-01";
    public CadDimensionType DimensionType { get; set; } = CadDimensionType.Aligned;

    public Point2D StartPoint { get; set; }
    public Point2D EndPoint { get; set; }
    public Point2D OffsetPoint { get; set; }

    public string? CustomTextOverride { get; set; }
    public string Prefix { get; set; } = "";
    public string Suffix { get; set; } = "m";

    public double CalculatedLengthMeters => StartPoint.DistanceTo(EndPoint);

    public string FormattedText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(CustomTextOverride))
                return CustomTextOverride;

            var val = CalculatedLengthMeters;
            return $"{Prefix}{val:F2} {Suffix}".Trim();
        }
    }

    public CadDimension()
    {
        Layer = "Dimensions";
        ColorHex = "#EF4444";
        LineThickness = 1.0;
    }

    public override BoundingBox2D BoundingBox
    {
        get
        {
            var (d1, d2) = GetDimensionLinePoints();
            return BoundingBox2D.FromPoints([StartPoint, EndPoint, d1, d2, OffsetPoint]);
        }
    }

    public static (Point2D DimLineStart, Point2D DimLineEnd) CalculateDimensionLinePoints(Point2D start, Point2D end, Point2D offset)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var len = Math.Sqrt((dx * dx) + (dy * dy));
        if (len < 1e-9)
        {
            return (start, end);
        }

        var ux = dx / len;
        var uy = dy / len;
        var nx = -uy;
        var ny = ux;

        // Project offset onto perpendicular normal
        var h = ((offset.X - start.X) * nx) + ((offset.Y - start.Y) * ny);

        var d1 = new Point2D(start.X + (h * nx), start.Y + (h * ny));
        var d2 = new Point2D(end.X + (h * nx), end.Y + (h * ny));
        return (d1, d2);
    }

    public (Point2D DimLineStart, Point2D DimLineEnd) GetDimensionLinePoints()
    {
        return CalculateDimensionLinePoints(StartPoint, EndPoint, OffsetPoint);
    }

    public override ICadEntity Clone()
    {
        return new CadDimension
        {
            Id = Guid.NewGuid(),
            Identifier = Identifier,
            DimensionType = DimensionType,
            StartPoint = StartPoint,
            EndPoint = EndPoint,
            OffsetPoint = OffsetPoint,
            CustomTextOverride = CustomTextOverride,
            Prefix = Prefix,
            Suffix = Suffix,
            Layer = Layer,
            ColorHex = ColorHex,
            LineThickness = LineThickness
        };
    }

    public override void Translate(double dx, double dy)
    {
        StartPoint = CadTransformMath.TranslatePoint(StartPoint, dx, dy);
        EndPoint = CadTransformMath.TranslatePoint(EndPoint, dx, dy);
        OffsetPoint = CadTransformMath.TranslatePoint(OffsetPoint, dx, dy);
    }

    public override void Rotate(Point2D origin, double angleDegrees)
    {
        StartPoint = CadTransformMath.RotatePoint(StartPoint, origin, angleDegrees);
        EndPoint = CadTransformMath.RotatePoint(EndPoint, origin, angleDegrees);
        OffsetPoint = CadTransformMath.RotatePoint(OffsetPoint, origin, angleDegrees);
    }

    public override void Mirror(Point2D axisP1, Point2D axisP2)
    {
        StartPoint = CadTransformMath.MirrorPoint(StartPoint, axisP1, axisP2);
        EndPoint = CadTransformMath.MirrorPoint(EndPoint, axisP1, axisP2);
        OffsetPoint = CadTransformMath.MirrorPoint(OffsetPoint, axisP1, axisP2);
    }
}

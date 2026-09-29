using DotNetCad.Core.Geometry;

namespace DotNetCad.Core.Entities;

public sealed class CadLine : CadEntity
{
    public override CadEntityType EntityType => CadEntityType.Line;

    public Point2D StartPoint { get; set; }
    public Point2D EndPoint { get; set; }

    public double Length => StartPoint.DistanceTo(EndPoint);
    public Point2D Midpoint => new((StartPoint.X + EndPoint.X) / 2.0, (StartPoint.Y + EndPoint.Y) / 2.0);

    public CadLine() { }

    public CadLine(Point2D start, Point2D end, string layer = "Geometry", string colorHex = "#38BDF8")
    {
        StartPoint = start;
        EndPoint = end;
        Layer = layer;
        ColorHex = colorHex;
    }

    public override BoundingBox2D BoundingBox => BoundingBox2D.FromCorners(StartPoint, EndPoint);

    public override ICadEntity Clone()
    {
        return new CadLine
        {
            Id = Guid.NewGuid(),
            StartPoint = StartPoint,
            EndPoint = EndPoint,
            Layer = Layer,
            ColorHex = ColorHex,
            LineThickness = LineThickness
        };
    }

    public override void Translate(double dx, double dy)
    {
        StartPoint = CadTransformMath.TranslatePoint(StartPoint, dx, dy);
        EndPoint = CadTransformMath.TranslatePoint(EndPoint, dx, dy);
    }

    public override void Rotate(Point2D origin, double angleDegrees)
    {
        StartPoint = CadTransformMath.RotatePoint(StartPoint, origin, angleDegrees);
        EndPoint = CadTransformMath.RotatePoint(EndPoint, origin, angleDegrees);
    }

    public override void Mirror(Point2D axisP1, Point2D axisP2)
    {
        StartPoint = CadTransformMath.MirrorPoint(StartPoint, axisP1, axisP2);
        EndPoint = CadTransformMath.MirrorPoint(EndPoint, axisP1, axisP2);
    }
}

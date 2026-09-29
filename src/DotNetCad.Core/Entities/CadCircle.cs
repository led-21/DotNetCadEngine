using DotNetCad.Core.Geometry;

namespace DotNetCad.Core.Entities;

public sealed class CadCircle : CadEntity
{
    public override CadEntityType EntityType => CadEntityType.Circle;

    public Point2D Center { get; set; }
    public double Radius { get; set; }

    public double Diameter => Radius * 2.0;
    public double Circumference => 2.0 * Math.PI * Radius;
    public double Area => Math.PI * Radius * Radius;

    public CadCircle() { }

    public CadCircle(Point2D center, double radius, string layer = "Geometry", string colorHex = "#38BDF8")
    {
        Center = center;
        Radius = radius;
        Layer = layer;
        ColorHex = colorHex;
    }

    public override BoundingBox2D BoundingBox => new(
        Center.X - Radius,
        Center.Y - Radius,
        Center.X + Radius,
        Center.Y + Radius);

    public override ICadEntity Clone()
    {
        return new CadCircle
        {
            Id = Guid.NewGuid(),
            Center = Center,
            Radius = Radius,
            Layer = Layer,
            ColorHex = ColorHex,
            LineThickness = LineThickness
        };
    }

    public override void Translate(double dx, double dy)
    {
        Center = CadTransformMath.TranslatePoint(Center, dx, dy);
    }

    public override void Rotate(Point2D origin, double angleDegrees)
    {
        Center = CadTransformMath.RotatePoint(Center, origin, angleDegrees);
    }

    public override void Mirror(Point2D axisP1, Point2D axisP2)
    {
        Center = CadTransformMath.MirrorPoint(Center, axisP1, axisP2);
    }
}

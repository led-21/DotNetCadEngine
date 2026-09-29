using DotNetCad.Core.Geometry;

namespace DotNetCad.Core.Entities;

public sealed class CadRectangle : CadEntity
{
    public override CadEntityType EntityType => CadEntityType.Rectangle;

    public Point2D Origin { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double RotationDegrees { get; set; }

    public double Area => Width * Height;
    public double Perimeter => 2.0 * (Width + Height);

    public CadRectangle() { }

    public CadRectangle(Point2D origin, double width, double height, double rotationDegrees = 0.0, string layer = "Geometry", string colorHex = "#38BDF8")
    {
        Origin = origin;
        Width = width;
        Height = height;
        RotationDegrees = rotationDegrees;
        Layer = layer;
        ColorHex = colorHex;
    }

    public List<Point2D> GetVertices()
    {
        return PolygonMath.CreateRectangle(Origin, Width, Height, RotationDegrees);
    }

    public override BoundingBox2D BoundingBox => BoundingBox2D.FromPoints(GetVertices());

    public override ICadEntity Clone()
    {
        return new CadRectangle
        {
            Id = Guid.NewGuid(),
            Origin = Origin,
            Width = Width,
            Height = Height,
            RotationDegrees = RotationDegrees,
            Layer = Layer,
            ColorHex = ColorHex,
            LineThickness = LineThickness
        };
    }

    public override void Translate(double dx, double dy)
    {
        Origin = CadTransformMath.TranslatePoint(Origin, dx, dy);
    }

    public override void Rotate(Point2D origin, double angleDegrees)
    {
        var vertices = GetVertices();
        var rotated = CadTransformMath.RotatePolygon(vertices, origin, angleDegrees);
        Origin = rotated[0];
        RotationDegrees = (RotationDegrees + angleDegrees) % 360.0;
        if (RotationDegrees < 0) RotationDegrees += 360.0;
    }

    public override void Mirror(Point2D axisP1, Point2D axisP2)
    {
        var vertices = GetVertices();
        var mirrored = CadTransformMath.MirrorPolygon(vertices, axisP1, axisP2);
        Origin = mirrored[0];
        // Mirroring flips winding and orientation
        var dx = mirrored[1].X - mirrored[0].X;
        var dy = mirrored[1].Y - mirrored[0].Y;
        RotationDegrees = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        if (RotationDegrees < 0) RotationDegrees += 360.0;
    }
}

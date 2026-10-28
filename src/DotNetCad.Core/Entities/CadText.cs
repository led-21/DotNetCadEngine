using DotNetCad.Core.Geometry;

namespace DotNetCad.Core.Entities;

/// <summary>A single-line drawing label, anchored at its lower-left insertion point.</summary>
public sealed class CadText : CadEntity
{
    public override CadEntityType EntityType => CadEntityType.Text;

    public Point2D Position { get; set; }
    public Point2D InsertionPoint => Position;
    public string Text { get; set; } = string.Empty;
    public double Height { get; set; } = 1.0;
    public double RotationDegrees { get; set; }

    public CadText() { }

    public CadText(Point2D position, string text, double height = 1.0, double rotationDegrees = 0.0, string layer = "Geometry", string colorHex = "#38BDF8")
    {
        Position = position;
        Text = text;
        Height = height;
        RotationDegrees = rotationDegrees;
        Layer = layer;
        ColorHex = colorHex;
    }

    public override BoundingBox2D BoundingBox
    {
        get
        {
            var width = Text.Length * Height * 0.6;
            var angle = RotationDegrees * Math.PI / 180.0;
            var ux = new Point2D(Math.Cos(angle), Math.Sin(angle));
            var uy = new Point2D(-Math.Sin(angle), Math.Cos(angle));
            return BoundingBox2D.FromPoints([
                Position,
                Corner(width, 0),
                Corner(0, Height),
                Corner(width, Height)
            ]);

            Point2D Corner(double x, double y) => new(
                Position.X + x * ux.X + y * uy.X,
                Position.Y + x * ux.Y + y * uy.Y);
        }
    }

    public override ICadEntity Clone() => new CadText
    {
        Position = Position,
        Text = Text,
        Height = Height,
        RotationDegrees = RotationDegrees,
        Layer = Layer,
        ColorHex = ColorHex,
        LineThickness = LineThickness
    };

    public override void Translate(double dx, double dy) =>
        Position = CadTransformMath.TranslatePoint(Position, dx, dy);

    public override void Rotate(Point2D origin, double angleDegrees)
    {
        Position = CadTransformMath.RotatePoint(Position, origin, angleDegrees);
        RotationDegrees += angleDegrees;
    }

    public override void Mirror(Point2D axisP1, Point2D axisP2)
    {
        var angle = RotationDegrees * Math.PI / 180.0;
        var baseline = new Point2D(Position.X + Math.Cos(angle), Position.Y + Math.Sin(angle));
        var mirroredPosition = CadTransformMath.MirrorPoint(Position, axisP1, axisP2);
        var mirroredBaseline = CadTransformMath.MirrorPoint(baseline, axisP1, axisP2);
        Position = mirroredPosition;
        RotationDegrees = Math.Atan2(mirroredBaseline.Y - Position.Y,
            mirroredBaseline.X - Position.X) * 180.0 / Math.PI;
    }
}

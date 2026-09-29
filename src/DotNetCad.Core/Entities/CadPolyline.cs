using DotNetCad.Core.Geometry;

namespace DotNetCad.Core.Entities;

public sealed class CadPolyline : CadEntity
{
    public override CadEntityType EntityType => CadEntityType.Polyline;

    public List<Point2D> Vertices { get; set; } = [];
    public bool IsClosed { get; set; }

    public double Length => IsClosed ? PolygonMath.CalculatePerimeter(Vertices) : CalculateOpenLength();
    public double Area => IsClosed ? PolygonMath.CalculatePolygonArea(Vertices) : 0.0;

    public CadPolyline() { }

    public CadPolyline(IEnumerable<Point2D> vertices, bool isClosed = false, string layer = "Geometry", string colorHex = "#38BDF8")
    {
        Vertices = vertices.ToList();
        IsClosed = isClosed;
        Layer = layer;
        ColorHex = colorHex;
    }

    private double CalculateOpenLength()
    {
        if (Vertices.Count < 2) return 0.0;
        double len = 0.0;
        for (int i = 0; i < Vertices.Count - 1; i++)
        {
            len += Vertices[i].DistanceTo(Vertices[i + 1]);
        }
        return len;
    }

    public override BoundingBox2D BoundingBox => BoundingBox2D.FromPoints(Vertices);

    public override ICadEntity Clone()
    {
        return new CadPolyline
        {
            Id = Guid.NewGuid(),
            Vertices = Vertices.Select(v => new Point2D(v.X, v.Y)).ToList(),
            IsClosed = IsClosed,
            Layer = Layer,
            ColorHex = ColorHex,
            LineThickness = LineThickness
        };
    }

    public override void Translate(double dx, double dy)
    {
        Vertices = CadTransformMath.TranslatePolygon(Vertices, dx, dy);
    }

    public override void Rotate(Point2D origin, double angleDegrees)
    {
        Vertices = CadTransformMath.RotatePolygon(Vertices, origin, angleDegrees);
    }

    public override void Mirror(Point2D axisP1, Point2D axisP2)
    {
        Vertices = CadTransformMath.MirrorPolygon(Vertices, axisP1, axisP2);
    }
}

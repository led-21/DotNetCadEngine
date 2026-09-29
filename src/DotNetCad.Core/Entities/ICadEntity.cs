using DotNetCad.Core.Geometry;

namespace DotNetCad.Core.Entities;

public interface ICadEntity
{
    Guid Id { get; }
    CadEntityType EntityType { get; }
    string Layer { get; set; }
    string ColorHex { get; set; }
    double LineThickness { get; set; }
    bool IsSelected { get; set; }
    BoundingBox2D BoundingBox { get; }

    ICadEntity Clone();
    void Translate(double dx, double dy);
    void Rotate(Point2D origin, double angleDegrees);
    void Mirror(Point2D axisP1, Point2D axisP2);
}

public abstract class CadEntity : ICadEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public abstract CadEntityType EntityType { get; }
    public string Layer { get; set; } = "Geometry";
    public string ColorHex { get; set; } = "#38BDF8";
    public double LineThickness { get; set; } = 1.5;
    public bool IsSelected { get; set; }

    public abstract BoundingBox2D BoundingBox { get; }

    public abstract ICadEntity Clone();
    public abstract void Translate(double dx, double dy);
    public abstract void Rotate(Point2D origin, double angleDegrees);
    public abstract void Mirror(Point2D axisP1, Point2D axisP2);
}

using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;

namespace DotNetCad.Core.Selection;

public enum GripType
{
    Vertex,
    Midpoint,
    Center,
    RadiusQuadrant,
    DimensionOffset
}

public sealed class GripHandle
{
    public Guid EntityId { get; set; }
    public int HandleIndex { get; set; }
    public GripType Type { get; set; }
    public Point2D Position { get; set; }
    public string Label { get; set; } = string.Empty;
}

public static class CadGripEngine
{
    public static List<GripHandle> GetGripsForEntity(ICadEntity entity)
    {
        var grips = new List<GripHandle>();
        if (entity == null) return grips;

        if (entity is CadLine line)
        {
            grips.Add(new GripHandle { EntityId = line.Id, HandleIndex = 0, Type = GripType.Vertex, Position = line.StartPoint, Label = "Start" });
            grips.Add(new GripHandle { EntityId = line.Id, HandleIndex = 1, Type = GripType.Vertex, Position = line.EndPoint, Label = "End" });
            grips.Add(new GripHandle { EntityId = line.Id, HandleIndex = 2, Type = GripType.Midpoint, Position = line.Midpoint, Label = "Midpoint" });
        }
        else if (entity is CadPolyline poly)
        {
            for (int i = 0; i < poly.Vertices.Count; i++)
            {
                grips.Add(new GripHandle
                {
                    EntityId = poly.Id,
                    HandleIndex = i,
                    Type = GripType.Vertex,
                    Position = poly.Vertices[i],
                    Label = $"Vertex {i + 1}"
                });
            }

            for (int i = 0; i < poly.Vertices.Count - 1; i++)
            {
                var mid = new Point2D(
                    (poly.Vertices[i].X + poly.Vertices[i + 1].X) / 2.0,
                    (poly.Vertices[i].Y + poly.Vertices[i + 1].Y) / 2.0);

                grips.Add(new GripHandle
                {
                    EntityId = poly.Id,
                    HandleIndex = 100 + i,
                    Type = GripType.Midpoint,
                    Position = mid,
                    Label = $"Midpoint {i + 1}"
                });
            }
        }
        else if (entity is CadRectangle rect)
        {
            var corners = rect.GetVertices();
            for (int i = 0; i < corners.Count; i++)
            {
                grips.Add(new GripHandle
                {
                    EntityId = rect.Id,
                    HandleIndex = i,
                    Type = GripType.Vertex,
                    Position = corners[i],
                    Label = $"Corner {i + 1}"
                });
            }
        }
        else if (entity is CadCircle circle)
        {
            grips.Add(new GripHandle { EntityId = circle.Id, HandleIndex = 0, Type = GripType.Center, Position = circle.Center, Label = "Center" });
            grips.Add(new GripHandle { EntityId = circle.Id, HandleIndex = 1, Type = GripType.RadiusQuadrant, Position = new Point2D(circle.Center.X + circle.Radius, circle.Center.Y), Label = "Radius East" });
            grips.Add(new GripHandle { EntityId = circle.Id, HandleIndex = 2, Type = GripType.RadiusQuadrant, Position = new Point2D(circle.Center.X, circle.Center.Y + circle.Radius), Label = "Radius North" });
            grips.Add(new GripHandle { EntityId = circle.Id, HandleIndex = 3, Type = GripType.RadiusQuadrant, Position = new Point2D(circle.Center.X - circle.Radius, circle.Center.Y), Label = "Radius West" });
            grips.Add(new GripHandle { EntityId = circle.Id, HandleIndex = 4, Type = GripType.RadiusQuadrant, Position = new Point2D(circle.Center.X, circle.Center.Y - circle.Radius), Label = "Radius South" });
        }
        else if (entity is CadArc arc)
        {
            grips.Add(new GripHandle { EntityId = arc.Id, HandleIndex = 0, Type = GripType.Center, Position = arc.Center, Label = "Center" });
            grips.Add(new GripHandle { EntityId = arc.Id, HandleIndex = 1, Type = GripType.Vertex, Position = arc.StartPoint, Label = "Arc Start" });
            grips.Add(new GripHandle { EntityId = arc.Id, HandleIndex = 2, Type = GripType.Vertex, Position = arc.EndPoint, Label = "Arc End" });
            grips.Add(new GripHandle { EntityId = arc.Id, HandleIndex = 3, Type = GripType.Midpoint, Position = arc.Midpoint, Label = "Arc Midpoint" });
        }
        else if (entity is CadDimension dim)
        {
            var (d1, d2) = dim.GetDimensionLinePoints();
            var mid = new Point2D((d1.X + d2.X) / 2.0, (d1.Y + d2.Y) / 2.0);
            grips.Add(new GripHandle { EntityId = dim.Id, HandleIndex = 0, Type = GripType.Vertex, Position = dim.StartPoint, Label = "Dimension Origin" });
            grips.Add(new GripHandle { EntityId = dim.Id, HandleIndex = 1, Type = GripType.Vertex, Position = dim.EndPoint, Label = "Dimension Target" });
            grips.Add(new GripHandle { EntityId = dim.Id, HandleIndex = 2, Type = GripType.DimensionOffset, Position = mid, Label = "Dimension Line" });
        }

        return grips;
    }

    public static GripHandle? HitTestGrips(IEnumerable<GripHandle> grips, Point2D clickPoint, double toleranceMeters = 0.50)
    {
        GripHandle? bestGrip = null;
        double minDistance = toleranceMeters;

        foreach (var grip in grips)
        {
            var dist = clickPoint.DistanceTo(grip.Position);
            if (dist < minDistance)
            {
                minDistance = dist;
                bestGrip = grip;
            }
        }

        return bestGrip;
    }

    public static void ApplyGripMove(ICadEntity entity, GripHandle grip, Point2D newPosition)
    {
        if (entity is CadLine line)
        {
            if (grip.HandleIndex == 0) line.StartPoint = newPosition;
            else if (grip.HandleIndex == 1) line.EndPoint = newPosition;
            else if (grip.HandleIndex == 2)
            {
                var dx = newPosition.X - line.Midpoint.X;
                var dy = newPosition.Y - line.Midpoint.Y;
                line.Translate(dx, dy);
            }
        }
        else if (entity is CadPolyline poly)
        {
            if (grip.Type == GripType.Vertex && grip.HandleIndex < poly.Vertices.Count)
            {
                poly.Vertices[grip.HandleIndex] = newPosition;
            }
        }
        else if (entity is CadCircle circle)
        {
            if (grip.HandleIndex == 0)
            {
                circle.Center = newPosition;
            }
            else
            {
                circle.Radius = Math.Max(0.01, circle.Center.DistanceTo(newPosition));
            }
        }
        else if (entity is CadRectangle rect)
        {
            if (grip.HandleIndex == 0)
            {
                rect.Origin = newPosition;
            }
            else if (grip.HandleIndex == 2)
            {
                rect.Width = Math.Max(0.1, newPosition.X - rect.Origin.X);
                rect.Height = Math.Max(0.1, newPosition.Y - rect.Origin.Y);
            }
        }
        else if (entity is CadDimension dim)
        {
            if (grip.HandleIndex == 0) dim.StartPoint = newPosition;
            else if (grip.HandleIndex == 1) dim.EndPoint = newPosition;
            else if (grip.HandleIndex == 2) dim.OffsetPoint = newPosition;
        }
    }
}

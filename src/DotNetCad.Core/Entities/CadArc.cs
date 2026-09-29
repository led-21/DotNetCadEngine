using DotNetCad.Core.Geometry;

namespace DotNetCad.Core.Entities;

public sealed class CadArc : CadEntity
{
    public override CadEntityType EntityType => CadEntityType.Arc;

    public Point2D Center { get; set; }
    public double Radius { get; set; }
    public double StartAngleDegrees { get; set; }
    public double EndAngleDegrees { get; set; }

    public double SweepAngleDegrees
    {
        get
        {
            var sweep = EndAngleDegrees - StartAngleDegrees;
            while (sweep < 0) sweep += 360.0;
            return sweep;
        }
    }

    public double ArcLength => (SweepAngleDegrees * Math.PI / 180.0) * Radius;

    public Point2D StartPoint
    {
        get
        {
            var rad = StartAngleDegrees * Math.PI / 180.0;
            return new Point2D(Center.X + (Radius * Math.Cos(rad)), Center.Y + (Radius * Math.Sin(rad)));
        }
    }

    public Point2D EndPoint
    {
        get
        {
            var rad = EndAngleDegrees * Math.PI / 180.0;
            return new Point2D(Center.X + (Radius * Math.Cos(rad)), Center.Y + (Radius * Math.Sin(rad)));
        }
    }

    public Point2D Midpoint
    {
        get
        {
            var midAngle = StartAngleDegrees + (SweepAngleDegrees / 2.0);
            var rad = midAngle * Math.PI / 180.0;
            return new Point2D(Center.X + (Radius * Math.Cos(rad)), Center.Y + (Radius * Math.Sin(rad)));
        }
    }

    public CadArc() { }

    public CadArc(Point2D center, double radius, double startAngleDegrees, double endAngleDegrees, string layer = "Geometry", string colorHex = "#38BDF8")
    {
        Center = center;
        Radius = radius;
        StartAngleDegrees = startAngleDegrees;
        EndAngleDegrees = endAngleDegrees;
        Layer = layer;
        ColorHex = colorHex;
    }

    public override BoundingBox2D BoundingBox
    {
        get
        {
            var bbox = BoundingBox2D.FromPoints([StartPoint, EndPoint]);
            // Check quadrants 0, 90, 180, 270 inside sweep
            double[] cardinalAngles = [0, 90, 180, 270];
            foreach (var angle in cardinalAngles)
            {
                if (IsAngleBetween(angle, StartAngleDegrees, EndAngleDegrees))
                {
                    var rad = angle * Math.PI / 180.0;
                    bbox = bbox.Expand(new Point2D(Center.X + (Radius * Math.Cos(rad)), Center.Y + (Radius * Math.Sin(rad))));
                }
            }
            return bbox;
        }
    }

    private static bool IsAngleBetween(double angle, double start, double end)
    {
        angle = (angle % 360 + 360) % 360;
        start = (start % 360 + 360) % 360;
        end = (end % 360 + 360) % 360;

        if (start <= end)
            return angle >= start && angle <= end;
        return angle >= start || angle <= end;
    }

    public override ICadEntity Clone()
    {
        return new CadArc
        {
            Id = Guid.NewGuid(),
            Center = Center,
            Radius = Radius,
            StartAngleDegrees = StartAngleDegrees,
            EndAngleDegrees = EndAngleDegrees,
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
        StartAngleDegrees = (StartAngleDegrees + angleDegrees) % 360.0;
        EndAngleDegrees = (EndAngleDegrees + angleDegrees) % 360.0;
        if (StartAngleDegrees < 0) StartAngleDegrees += 360.0;
        if (EndAngleDegrees < 0) EndAngleDegrees += 360.0;
    }

    public override void Mirror(Point2D axisP1, Point2D axisP2)
    {
        var sp = CadTransformMath.MirrorPoint(StartPoint, axisP1, axisP2);
        var ep = CadTransformMath.MirrorPoint(EndPoint, axisP1, axisP2);
        Center = CadTransformMath.MirrorPoint(Center, axisP1, axisP2);

        // Mirroring flips CCW into CW, so swap start and end to keep CCW
        var newStartRad = Math.Atan2(ep.Y - Center.Y, ep.X - Center.X);
        var newEndRad = Math.Atan2(sp.Y - Center.Y, sp.X - Center.X);

        StartAngleDegrees = (newStartRad * 180.0 / Math.PI + 360.0) % 360.0;
        EndAngleDegrees = (newEndRad * 180.0 / Math.PI + 360.0) % 360.0;
    }
}

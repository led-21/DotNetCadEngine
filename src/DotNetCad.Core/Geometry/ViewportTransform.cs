namespace DotNetCad.Core.Geometry;

public sealed class ViewportTransform
{
    public double Zoom { get; set; } = 1.0;
    public double PanX { get; set; } = 0.0;
    public double PanY { get; set; } = 0.0;

    public ScreenPoint WorldToScreen(Point2D worldPoint)
    {
        return new ScreenPoint(
            (worldPoint.X * Zoom) + PanX,
            (worldPoint.Y * Zoom) + PanY);
    }

    public Point2D ScreenToWorld(ScreenPoint screenPoint)
    {
        if (Math.Abs(Zoom) < 1e-9)
        {
            return Point2D.Zero;
        }

        return new Point2D(
            (screenPoint.X - PanX) / Zoom,
            (screenPoint.Y - PanY) / Zoom);
    }

    public void ZoomAtScreenPoint(double newZoom, ScreenPoint center)
    {
        newZoom = Math.Clamp(newZoom, 0.01, 100.0);
        var worldCenter = ScreenToWorld(center);
        Zoom = newZoom;
        PanX = center.X - (worldCenter.X * Zoom);
        PanY = center.Y - (worldCenter.Y * Zoom);
    }
}

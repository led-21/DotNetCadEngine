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

    public bool FitBounds(BoundingBox2D bounds, double viewportWidth, double viewportHeight, double margin = 32.0)
    {
        if (bounds.IsEmpty || !double.IsFinite(bounds.MinX) || !double.IsFinite(bounds.MinY) ||
            !double.IsFinite(bounds.MaxX) || !double.IsFinite(bounds.MaxY) ||
            !double.IsFinite(viewportWidth) || !double.IsFinite(viewportHeight) ||
            viewportWidth <= 0 || viewportHeight <= 0) return false;

        var availableWidth = Math.Max(1.0, viewportWidth - 2 * margin);
        var availableHeight = Math.Max(1.0, viewportHeight - 2 * margin);
        Zoom = Math.Clamp(Math.Min(availableWidth / Math.Max(bounds.Width, 0.01),
            availableHeight / Math.Max(bounds.Height, 0.01)), 0.01, 100.0);
        PanX = viewportWidth / 2.0 - bounds.Center.X * Zoom;
        PanY = viewportHeight / 2.0 - bounds.Center.Y * Zoom;
        return true;
    }
}

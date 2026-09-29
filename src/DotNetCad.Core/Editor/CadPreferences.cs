namespace DotNetCad.Core.Editor;

public sealed class CadPreferences
{
    public double GridIntervalMeters { get; set; } = 0.50;
    public int DecimalPrecision { get; set; } = 2;
    public string DisplayUnit { get; set; } = "m";

    public bool IsGridSnapEnabled { get; set; } = true;
    public bool IsOsnapEnabled { get; set; } = true;

    public bool IsOsnapEndpointEnabled { get; set; } = true;
    public bool IsOsnapMidpointEnabled { get; set; } = true;
    public bool IsOsnapCenterEnabled { get; set; } = true;
    public bool IsOsnapIntersectionEnabled { get; set; } = true;

    // Precision modes (F8 Ortho & F10 Polar)
    public bool IsOrthoModeEnabled { get; set; } = false;
    public bool IsPolarTrackingEnabled { get; set; } = true;
    public double PolarAngleIncrementDegrees { get; set; } = 45.0;

    // Screen pixel tolerances
    public double GripHitSizePixels { get; set; } = 10.0;
    public double SnapHitSizePixels { get; set; } = 8.0;
    public double SelectionHitSizePixels { get; set; } = 6.0;

    public CadPreferences Clone()
    {
        return new CadPreferences
        {
            GridIntervalMeters = GridIntervalMeters,
            DecimalPrecision = DecimalPrecision,
            DisplayUnit = DisplayUnit,
            IsGridSnapEnabled = IsGridSnapEnabled,
            IsOsnapEnabled = IsOsnapEnabled,
            IsOsnapEndpointEnabled = IsOsnapEndpointEnabled,
            IsOsnapMidpointEnabled = IsOsnapMidpointEnabled,
            IsOsnapCenterEnabled = IsOsnapCenterEnabled,
            IsOsnapIntersectionEnabled = IsOsnapIntersectionEnabled,
            IsOrthoModeEnabled = IsOrthoModeEnabled,
            IsPolarTrackingEnabled = IsPolarTrackingEnabled,
            PolarAngleIncrementDegrees = PolarAngleIncrementDegrees,
            GripHitSizePixels = GripHitSizePixels,
            SnapHitSizePixels = SnapHitSizePixels,
            SelectionHitSizePixels = SelectionHitSizePixels
        };
    }
}

namespace DotNetCad.Core.Entities;

public sealed class CadLayer
{
    public string Name { get; set; } = "0";
    public string ColorHex { get; set; } = "#FFFFFF";
    public double LineThickness { get; set; } = 1.0;
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; } = false;
    public bool IsPlottable { get; set; } = true;
    public bool IsProtected { get; set; } = false;

    public CadLayer Clone()
    {
        return new CadLayer
        {
            Name = Name,
            ColorHex = ColorHex,
            LineThickness = LineThickness,
            IsVisible = IsVisible,
            IsLocked = IsLocked,
            IsPlottable = IsPlottable,
            IsProtected = IsProtected
        };
    }

    public static List<CadLayer> GetDefaultLayers()
    {
        return
        [
            new CadLayer { Name = "0", ColorHex = "#FFFFFF", LineThickness = 1.0, IsProtected = true },
            new CadLayer { Name = "Geometry", ColorHex = "#38BDF8", LineThickness = 1.5 },
            new CadLayer { Name = "Dimensions", ColorHex = "#EF4444", LineThickness = 1.0 },
            new CadLayer { Name = "Hidden", ColorHex = "#94A3B8", LineThickness = 1.0 },
            new CadLayer { Name = "Annotations", ColorHex = "#F59E0B", LineThickness = 1.0 }
        ];
    }
}

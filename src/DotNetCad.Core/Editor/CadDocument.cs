using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;

namespace DotNetCad.Core.Editor;

public sealed class CadDocument
{
    public string Title { get; set; } = "Untitled.cad";
    public string ActiveLayer { get; set; } = "Geometry";
    public List<CadLayer> Layers { get; set; } = [];
    public List<ICadEntity> Entities { get; set; } = [];
    public CadPreferences Preferences { get; set; } = new();

    public CadDocument()
    {
        Layers = CadLayer.GetDefaultLayers();
    }

    public BoundingBox2D BoundingBox
    {
        get
        {
            var bbox = BoundingBox2D.Empty;
            foreach (var entity in Entities)
            {
                bbox = bbox.Expand(entity.BoundingBox);
            }
            return bbox;
        }
    }

    public void AddEntity(ICadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Entities.Add(entity);
    }

    public bool RemoveEntity(ICadEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return Entities.Remove(entity);
    }

    public void Clear()
    {
        Entities.Clear();
    }

    public CadLayer? GetLayer(string name)
    {
        return Layers.FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}

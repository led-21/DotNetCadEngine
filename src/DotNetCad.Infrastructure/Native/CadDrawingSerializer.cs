using System.Text.Json;
using DotNetCad.Core.Audit;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;

namespace DotNetCad.Infrastructure.Native;

/// <summary>Lossless, versioned drawing format for the desktop application's own files.</summary>
public static class CadDrawingSerializer
{
    private const int CurrentVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IgnoreReadOnlyProperties = true
    };

    private sealed class DrawingFile
    {
        public int Version { get; set; }
        public string? Title { get; set; }
        public string? ActiveLayer { get; set; }
        public List<CadLayer>? Layers { get; set; }
        public CadPreferences? Preferences { get; set; }
        public List<EntityFile>? Entities { get; set; }
        public List<CadAiAuditEntry>? AiAuditHistory { get; set; }
    }

    private sealed class EntityFile
    {
        public string? Type { get; set; }
        public JsonElement Data { get; set; }
    }

    public static string Serialize(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var file = new DrawingFile
        {
            Version = CurrentVersion,
            Title = document.Title,
            ActiveLayer = document.ActiveLayer,
            Layers = document.Layers.Select(layer => layer.Clone()).ToList(),
            Preferences = document.Preferences.Clone(),
            Entities = document.Entities.Select(entity => new EntityFile
            {
                Type = entity.EntityType.ToString(),
                Data = JsonSerializer.SerializeToElement(entity, entity.GetType(), JsonOptions)
            }).ToList(),
            AiAuditHistory = document.AiAuditHistory.ToList()
        };
        return JsonSerializer.Serialize(file, JsonOptions);
    }

    public static CadDocument Deserialize(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        try
        {
            var file = JsonSerializer.Deserialize<DrawingFile>(content, JsonOptions)
                ?? throw new InvalidDataException("Drawing file is empty.");
            if (file.Version != CurrentVersion)
                throw new InvalidDataException($"Unsupported drawing version: {file.Version}.");
            if (file.Layers == null || file.Layers.Count == 0 || file.Entities == null || file.Preferences == null ||
                string.IsNullOrWhiteSpace(file.ActiveLayer))
                throw new InvalidDataException("Drawing file is missing required data.");
            if (file.Layers.Any(layer => layer == null || string.IsNullOrWhiteSpace(layer.Name)) ||
                file.Layers.Select(layer => layer.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != file.Layers.Count ||
                !file.Layers.Any(layer => layer.Name.Equals(file.ActiveLayer, StringComparison.OrdinalIgnoreCase) && layer.IsVisible))
                throw new InvalidDataException("Drawing file has invalid layers or active layer.");

            var document = new CadDocument
            {
                Title = file.Title ?? "Untitled.cad",
                ActiveLayer = file.ActiveLayer,
                Layers = file.Layers,
                Preferences = file.Preferences,
                Entities = []
            };
            if (document.GetLayer(document.ActiveLayer)?.IsLocked == true)
                document.ActiveLayer = document.Layers.FirstOrDefault(l => l.IsVisible && !l.IsLocked)?.Name
                    ?? throw new InvalidDataException("Drawing has no visible unlocked layer.");
            var ids = new HashSet<Guid>();
            foreach (var item in file.Entities)
            {
                if (item == null || item.Data.ValueKind != JsonValueKind.Object)
                    throw new InvalidDataException("Drawing contains an invalid entity record.");
                var type = item.Type switch
                {
                    "Line" => typeof(CadLine),
                    "Circle" => typeof(CadCircle),
                    "Arc" => typeof(CadArc),
                    "Rectangle" => typeof(CadRectangle),
                    "Polyline" => typeof(CadPolyline),
                    "Dimension" => typeof(CadDimension),
                    "Text" => typeof(CadText),
                    _ => throw new InvalidDataException($"Unsupported entity type: {item.Type}.")
                };
                var entity = (ICadEntity)(item.Data.Deserialize(type, JsonOptions)
                    ?? throw new InvalidDataException("Drawing contains an empty entity."));
                var bounds = entity.BoundingBox;
                if (entity.Id == Guid.Empty || !ids.Add(entity.Id) || document.GetLayer(entity.Layer) == null ||
                    bounds.IsEmpty || !double.IsFinite(bounds.MinX) || !double.IsFinite(bounds.MinY) ||
                    !double.IsFinite(bounds.MaxX) || !double.IsFinite(bounds.MaxY))
                    throw new InvalidDataException("Drawing contains an invalid entity.");
                entity.IsSelected = false;
                document.Entities.Add(entity);
            }
            if (file.AiAuditHistory is { Count: > 0 })
            {
                document.AiAuditHistory = file.AiAuditHistory.ToList();
            }
            return document;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Drawing file contains invalid JSON.", ex);
        }
        catch (Exception ex) when (ex is NullReferenceException or InvalidOperationException or ArgumentException)
        {
            throw new InvalidDataException("Drawing file contains invalid geometry.", ex);
        }
    }
}

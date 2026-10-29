using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DotNetCad.Core.Editor;
using DotNetCad.Infrastructure.Native;
using DotNetCad.Infrastructure.Svg;
namespace DotNetCad.Infrastructure.MutuaConecta;

/// <summary>Builds a real, immutable snapshot for an explicitly requested remote publication.</summary>
public static class MutuaCadSyncPayload
{
    public static MutuaCadDocumentSyncRequest Create(CadDocument document)
    {
        var native=JsonNode.Parse(CadDrawingSerializer.Serialize(document))!;
        foreach(var entity in native["Entities"]!.AsArray())entity!["Data"]!.AsObject().Remove("IsSelected");
        var bytes=Encoding.UTF8.GetBytes(native.ToJsonString());
        var bounds=document.BoundingBox;
        var metadata=JsonSerializer.Serialize(new {
            schemaVersion=2,engine="DotNetCadEngine",engineVersion=typeof(MutuaCadSyncPayload).Assembly.GetName().Version?.ToString(),
            activeLayer=document.ActiveLayer,entityCount=document.Entities.Count,
            displayUnit=document.Preferences.DisplayUnit,internalUnit="m",
            layers=document.Layers.Select(l=>new {name=l.Name,colorHex=l.ColorHex,isVisible=l.IsVisible,isLocked=l.IsLocked,isProtected=l.IsProtected,entityCount=document.Entities.Count(e=>e.Layer.Equals(l.Name,StringComparison.OrdinalIgnoreCase))}).ToArray(),
            boundingBox=bounds.IsEmpty ? null : new {minX=bounds.MinX,minY=bounds.MinY,maxX=bounds.MaxX,maxY=bounds.MaxY},
            capabilities=CadIntegrationCapabilities.Supported
        });
        var title=string.IsNullOrWhiteSpace(document.Title)?"Drawing.cad":document.Title;
        return new(null,title,Path.GetFileName(title),"CadNative",Convert.ToHexStringLower(SHA256.HashData(bytes)),bytes.LongLength,metadata,SvgCadExporter.ExportToSvg(document),"ProjectTeam",bytes,0);
    }
}
public static class CadIntegrationCapabilities
{
    public static IReadOnlyList<string> Supported {get;}=Array.AsReadOnly(new[]{"drawing_read","geometry_edit","layers_lifecycle","batch_atomic","proposals_stage_commit_discard","entity_queries","spatial_conflicts","topology_analysis","ai_audit","svg_dxf_export","mutua_project_read","mutua_sync_explicit","engineering_skills"});
}

using System.Text.Json;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Infrastructure.MutuaConecta;
using Xunit;
namespace DotNetCad.Core.Tests;
public class MutuaCadSyncPayloadTests
{
    [Fact]
    public void EmptyDrawingProducesValidMetadataAndRealHash()
    {
        var d=new CadDocument();var payload=MutuaCadSyncPayload.Create(d);
        using var meta=JsonDocument.Parse(payload.CadMetadataJson!);
        Assert.Equal(0,meta.RootElement.GetProperty("entityCount").GetInt32());
        Assert.Equal(JsonValueKind.Null,meta.RootElement.GetProperty("boundingBox").ValueKind);
        Assert.Equal(64,payload.Sha256Hash.Length);Assert.True(payload.FileSizeBytes>0);
        Assert.Contains("<svg",payload.ContentPreviewSvg!);
        Assert.Contains("mutua_sync_explicit",meta.RootElement.GetProperty("capabilities").EnumerateArray().Select(x=>x.GetString()));
    }
    [Fact]
    public void SnapshotIgnoresSelectionButTracksGeometryAndLayerProperties()
    {
        var d=new CadDocument();var line=new CadLine(new(0,0),new(1,0));d.AddEntity(line);
        var original=MutuaCadSyncPayload.Create(d);line.IsSelected=true;
        Assert.Equal(original.Sha256Hash,MutuaCadSyncPayload.Create(d).Sha256Hash);
        line.Translate(2,0);Assert.NotEqual(original.Sha256Hash,MutuaCadSyncPayload.Create(d).Sha256Hash);
        d.GetLayer("Geometry")!.IsLocked=true;
        using var meta=JsonDocument.Parse(MutuaCadSyncPayload.Create(d).CadMetadataJson!);
        Assert.True(meta.RootElement.GetProperty("layers").EnumerateArray().Single(x=>x.GetProperty("name").GetString()=="Geometry").GetProperty("isLocked").GetBoolean());
    }
}

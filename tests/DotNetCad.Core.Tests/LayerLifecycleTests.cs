using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.History;
using DotNetCad.Infrastructure.Native;
using DotNetCad.Infrastructure.Dxf;
using Xunit;
namespace DotNetCad.Core.Tests;
public class LayerLifecycleTests
{
    [Fact]
    public void CreateRenameTransferDeleteRestoresOrderAndActiveLayer()
    {
        var d=new CadDocument();var h=new UndoRedoManager();
        CadCommandResult C(string s)=>CadCommandProcessor.Execute(d,h,s);
        Assert.True(C("LAYER \"Paredes novas\"").Success);
        Assert.False(C("LAYER CREATE \"paredes NOVAS\"").Success);
        Assert.True(C("LINE 0;0 1;0").Success);var entity=d.Entities.Single();entity.IsSelected=true;
        var order=d.Layers.ToArray();
        Assert.True(C("LAYER RENAME \"Paredes novas\" \"Paredes finais\"").Success);
        Assert.Equal("Paredes finais",entity.Layer);Assert.Equal("Paredes finais",d.ActiveLayer);
        var before=h.UndoCount;
        Assert.False(C("LAYER DELETE \"Paredes finais\" CANCEL").Success);
        Assert.False(C("LAYER DELETE \"Paredes finais\" TRANSFER inexistente").Success);
        Assert.Equal(before,h.UndoCount);
        Assert.True(C("LAYER DELETE \"Paredes finais\" TRANSFER Geometry").Success);
        Assert.Equal(before+1,h.UndoCount);Assert.Equal("Geometry",entity.Layer);Assert.Null(d.GetLayer("Paredes finais"));
        h.Undo();Assert.Equal("Paredes finais",entity.Layer);Assert.Equal("Paredes finais",d.ActiveLayer);Assert.Equal(order,d.Layers);
        h.Redo();Assert.Equal("Geometry",entity.Layer);h.Undo();
        Assert.True(C("LAYER DELETE \"Paredes finais\" ENTITIES").Success);Assert.Empty(d.Entities);
        h.Undo();Assert.Same(entity,d.Entities.Single());Assert.True(entity.IsSelected);h.Redo();Assert.Empty(d.Entities);
    }
    [Fact]
    public void ProtectsZeroProtectedAndLastUsableLayer()
    {
        var d=new CadDocument();var h=new UndoRedoManager();
        d.Layers.Add(new CadLayer { Name="Protegida",IsProtected=true });
        foreach(var name in new[]{"0","Protegida"}) {
            Assert.False(CadLayerOperations.Execute(d,h,"RENAME",name,"Outra").Success);
            Assert.False(CadLayerOperations.Execute(d,h,"DELETE",name,mode:LayerDeleteMode.DeleteEntities).Success);
        }
        foreach(var l in d.Layers.Where(l=>l.Name!="0").ToArray())Assert.True(CadLayerOperations.Execute(d,h,"HIDE",l.Name).Success);
        var count=h.UndoCount;
        Assert.False(CadLayerOperations.Execute(d,h,"HIDE","0").Success);
        Assert.False(CadLayerOperations.Execute(d,h,"LOCK","0").Success);
        Assert.Equal(count,h.UndoCount);Assert.Equal("0",d.ActiveLayer);Assert.True(d.GetLayer("0")!.IsVisible);
        Assert.False(CadLayerOperations.Execute(d,h,"CREATE","  ").Success);
    }
    [Theory]
    [InlineData("MOVE {0} 1;0")][InlineData("COPY {0} 1;0")][InlineData("ROTATE {0} 0;0 37")]
    [InlineData("MIRROR {0} 0;0 1;1")][InlineData("OFFSET {0} 1")][InlineData("DELETE {0}")]
    [InlineData("BREAK {0} 0,2;0 0,8;0")][InlineData("CLEAR")]
    public void LockedEntitiesRejectMutationsAndPreserveHistory(string command)
    {
        var d=new CadDocument();var h=new UndoRedoManager();
        Assert.True(CadCommandProcessor.Execute(d,h,"LINE 0;0 1;0").Success);var e=(CadLine)d.Entities.Single();
        Assert.True(CadLayerOperations.Execute(d,h,"LOCK","Geometry").Success);
        var count=h.UndoCount;
        Assert.False(CadCommandProcessor.Execute(d,h,string.Format(command,e.Id)).Success);
        Assert.Equal(count,h.UndoCount);Assert.Single(d.Entities);Assert.Equal(0,e.StartPoint.X);
        Assert.True(CadCommandProcessor.Execute(d,h,$"PROPERTIES {e.Id}").Success);
        h.Undo();Assert.False(d.GetLayer("Geometry")!.IsLocked);Assert.Equal("Geometry",d.ActiveLayer);
        h.Redo();Assert.True(d.GetLayer("Geometry")!.IsLocked);
    }
    [Fact]
    public void TransferBatchIsAtomicAndInteractiveSessionRechecksLocks()
    {
        var d=new CadDocument();var h=new UndoRedoManager();
        var a=new CadLine(new(0,0),new(1,0)) { IsSelected=true };
        var b=new CadLine(new(0,1),new(1,1),"Hidden") { IsSelected=true };
        d.AddEntity(a);d.AddEntity(b);
        var session=new CadModificationSession(CadToolType.Move);session.Select(d.Entities);session.Input("0;0");session.Input("@2;0");
        CadLayerOperations.Execute(d,h,"LOCK","Hidden");var count=h.UndoCount;
        Assert.False(CadLayerOperations.Execute(d,h,"TRANSFER","0").Success);Assert.Equal("Geometry",a.Layer);
        Assert.False(session.Confirm(d,h));Assert.Equal(count,h.UndoCount);
        CadLayerOperations.Execute(d,h,"UNLOCK","Hidden");
        Assert.True(CadLayerOperations.Execute(d,h,"TRANSFER","0").Success);Assert.All(d.Entities,e=>Assert.Equal("0",e.Layer));
        h.Undo();Assert.Equal("Geometry",a.Layer);Assert.Equal("Hidden",b.Layer);h.Redo();Assert.All(d.Entities,e=>Assert.Equal("0",e.Layer));
    }
    [Fact]
    public void PersistencePreservesLocksVisibilityAndExplicitColors()
    {
        var d=new CadDocument();var h=new UndoRedoManager();
        CadLayerOperations.Execute(d,h,"CREATE","Com espaços");
        CadLayerOperations.Execute(d,h,"LOCK","Com espaços");CadLayerOperations.Execute(d,h,"HIDE","Com espaços");
        var restored=CadDrawingSerializer.Deserialize(CadDrawingSerializer.Serialize(d));
        Assert.True(restored.GetLayer("Com espaços")!.IsLocked);Assert.False(restored.GetLayer("Com espaços")!.IsVisible);
        var dxf=DxfCadImporter.ImportFromDxf(DxfCadExporter.ExportToDxf(d));
        Assert.True(dxf.GetLayer("Com espaços")!.IsLocked);Assert.False(dxf.GetLayer("Com espaços")!.IsVisible);
        var entity=new CadLine(new(0,0),new(1,0),colorHex:d.GetLayer("Geometry")!.ColorHex);d.AddEntity(entity);var old=entity.ColorHex;
        CadLayerOperations.Execute(d,h,"COLOR","Geometry","#112233");Assert.Equal(old,entity.ColorHex);h.Undo();Assert.Equal(old,entity.ColorHex);
    }
}

using DotNetCad.Core.Entities;
using DotNetCad.Core.History;
namespace DotNetCad.Core.Editor;

public enum LayerDeleteMode { Cancel, Transfer, DeleteEntities }

/// <summary>Shared layer policy for UI, console and MCP. Plans and validates before history changes.</summary>
public static class CadLayerOperations
{
    public static void RequireEditable(CadDocument doc, IEnumerable<ICadEntity> entities)
    {
        if (entities.Any(e => doc.GetLayer(e.Layer) is { IsLocked: true }))
            throw new InvalidOperationException("A seleção contém entidades de camada bloqueada.");
    }
    public static void RequireUsable(CadDocument doc, string name)
    {
        if (doc.GetLayer(name) is not { IsVisible: true, IsLocked: false })
            throw new InvalidOperationException("A camada deve existir, estar visível e desbloqueada.");
    }
    public static CadCommandResult Execute(CadDocument doc, UndoRedoManager history, string operation,
        string name, string? argument = null, LayerDeleteMode mode = LayerDeleteMode.Cancel,
        IEnumerable<ICadEntity>? selected = null)
    {
        try
        {
            var op = operation.ToUpperInvariant();
            name = name.Trim();
            var layer = doc.GetLayer(name);
            Action change;
            if (op is "CREATE" or "ENSURE")
            {
                ValidateName(name);
                if (layer != null && op == "CREATE") throw new ArgumentException("Já existe uma camada com esse nome.");
                if (layer != null) { RequireUsable(doc,name); if(doc.ActiveLayer == layer.Name) return new(true,"Camada já ativa."); change = () => doc.ActiveLayer = layer.Name; }
                else { var created = new CadLayer { Name=name, ColorHex="#38BDF8" }; change=()=> { doc.Layers.Add(created); if(op == "ENSURE") doc.ActiveLayer=created.Name; }; }
            }
            else
            {
                if(layer == null) throw new ArgumentException("Camada não encontrada.");
                var members = doc.Entities.Where(e=>e.Layer.Equals(layer.Name,StringComparison.OrdinalIgnoreCase)).ToArray();
                var next = doc.Layers.FirstOrDefault(l=>l!=layer && l.IsVisible && !l.IsLocked);
                switch(op)
                {
                    case "RENAME":
                        if(layer.Name == "0" || layer.IsProtected) throw new ArgumentException("Camada protegida não pode ser renomeada.");
                        var newName = argument?.Trim() ?? ""; ValidateName(newName);
                        if(doc.Layers.Any(l=>l!=layer && l.Name.Equals(newName,StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Nome duplicado.");
                        if(layer.IsLocked) throw new ArgumentException("Desbloqueie a camada antes de renomear.");
                        change=()=> { if(doc.ActiveLayer.Equals(layer.Name,StringComparison.OrdinalIgnoreCase)) doc.ActiveLayer=newName; layer.Name=newName; foreach(var e in members)e.Layer=newName; }; break;
                    case "DELETE":
                        if(mode == LayerDeleteMode.Cancel) return new(false,$"Exclusão cancelada: {members.Length} entidade(s). Escolha TRANSFER destino ou ENTITIES.");
                        if(layer.Name == "0" || layer.IsProtected) throw new ArgumentException("Camada protegida não pode ser excluída.");
                        if(layer.IsLocked) throw new ArgumentException("Desbloqueie a camada antes de excluir.");
                        if(next == null) throw new ArgumentException("Preserve ao menos uma outra camada visível e desbloqueada.");
                        CadLayer? target=null;
                        if(mode == LayerDeleteMode.Transfer) { target=doc.GetLayer(argument ?? ""); if(target == layer) throw new ArgumentException("Destino deve ser outra camada."); RequireUsable(doc,argument ?? ""); }
                        change=()=> { if(doc.ActiveLayer.Equals(layer.Name,StringComparison.OrdinalIgnoreCase))doc.ActiveLayer=next.Name;
                            if(target!=null) foreach(var e in members)e.Layer=target.Name; else doc.Entities.RemoveAll(e=>members.Contains(e)); doc.Layers.Remove(layer); }; break;
                    case "ACTIVE":
                        RequireUsable(doc,name); if(doc.ActiveLayer==layer.Name)return new(true,"Camada já ativa."); change=()=>doc.ActiveLayer=layer.Name; break;
                    case "HIDE": case "LOCK":
                        if((op=="HIDE" && !layer.IsVisible) || (op=="LOCK" && layer.IsLocked))return new(true,"Estado já aplicado.");
                        if(layer.IsVisible && !layer.IsLocked && next==null) throw new ArgumentException("Preserve ao menos uma camada utilizável.");
                        change=()=> { if(doc.ActiveLayer.Equals(layer.Name,StringComparison.OrdinalIgnoreCase)) { if(next==null)throw new InvalidOperationException("Sem sucessora utilizável."); doc.ActiveLayer=next.Name; } if(op=="HIDE")layer.IsVisible=false; else layer.IsLocked=true; }; break;
                    case "SHOW": change=()=>layer.IsVisible=true; break;
                    case "UNLOCK": change=()=>layer.IsLocked=false; break;
                    case "COLOR":
                        if(argument == null || argument.Length != 7 || argument[0]!='#' || !argument.Skip(1).All(Uri.IsHexDigit))throw new ArgumentException("Use cor #RRGGBB.");
                        change=()=>layer.ColorHex=argument.ToUpperInvariant(); break;
                    case "TRANSFER":
                        RequireUsable(doc,name); var batch=(selected ?? doc.Entities.Where(e=>e.IsSelected)).ToArray();
                        if(batch.Length==0 || batch.Any(e=>!doc.Entities.Contains(e)))throw new ArgumentException("Selecione entidades existentes.");
                        RequireEditable(doc,batch); change=()=> { foreach(var e in batch)e.Layer=layer.Name; }; break;
                    default: throw new ArgumentException("Operação de camada desconhecida.");
                }
            }
            history.ExecuteAction(new LayerStateAction(doc,change,$"Camada: {op} {name}"));
            return new(true,"Camadas atualizadas.",DrawingChanged:true,LayersChanged:true);
        }
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException) { return new(false,ex.Message); }
    }
    private static void ValidateName(string name)
    {
        if(string.IsNullOrWhiteSpace(name) || name.Any(char.IsControl) || name.Contains('"'))throw new ArgumentException("Nome vazio ou com caracteres inválidos.");
    }
    private sealed class LayerStateAction(CadDocument doc, Action change, string description) : IUndoableAction
    {
        private Snapshot? before,after;
        public string Description => description;
        public void Execute()
        {
            if(after!=null) { after.Restore(doc);return; }
            before=new(doc);
            try { change(); after=new(doc); } catch { before.Restore(doc);throw; }
        }
        public void Undo()=>before!.Restore(doc);
    }
    private sealed class Snapshot
    {
        private readonly (CadLayer Ref,CadLayer Value)[] layers;
        private readonly (ICadEntity Ref,string Layer,bool Selected)[] entities;
        private readonly string active;
        public Snapshot(CadDocument doc) { layers=doc.Layers.Select(l=>(l,l.Clone())).ToArray(); entities=doc.Entities.Select(e=>(e,e.Layer,e.IsSelected)).ToArray(); active=doc.ActiveLayer; }
        public void Restore(CadDocument doc)
        {
            doc.Layers.Clear();
            foreach(var (r,v) in layers) { r.Name=v.Name;r.ColorHex=v.ColorHex;r.IsVisible=v.IsVisible;r.IsLocked=v.IsLocked;r.IsProtected=v.IsProtected;r.IsPlottable=v.IsPlottable;r.LineThickness=v.LineThickness;doc.Layers.Add(r); }
            doc.Entities.Clear();foreach(var (e,l,s) in entities) {e.Layer=l;e.IsSelected=s;doc.Entities.Add(e);}doc.ActiveLayer=active;
        }
    }
}

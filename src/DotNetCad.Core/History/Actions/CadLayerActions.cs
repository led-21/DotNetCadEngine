using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;

namespace DotNetCad.Core.History.Actions;

public sealed class SetLayerVisibilityAction : IUndoableAction
{
    private readonly CadDocument _document;
    private readonly CadLayer _layer;
    private readonly bool _wasVisible;
    private readonly bool _isVisible;
    private readonly string _previousActiveLayer;
    private readonly string _nextActiveLayer;

    public SetLayerVisibilityAction(CadDocument document, CadLayer layer, bool isVisible, string nextActiveLayer)
    {
        _document = document;
        _layer = layer;
        _wasVisible = layer.IsVisible;
        _isVisible = isVisible;
        _previousActiveLayer = document.ActiveLayer;
        _nextActiveLayer = nextActiveLayer;
    }

    public string Description => $"{(_isVisible ? "Show" : "Hide")} layer {_layer.Name}";

    public void Execute()
    {
        _layer.IsVisible = _isVisible;
        _document.ActiveLayer = _nextActiveLayer;
    }

    public void Undo()
    {
        _layer.IsVisible = _wasVisible;
        _document.ActiveLayer = _previousActiveLayer;
    }
}

public sealed class SetLayerColorAction : IUndoableAction
{
    private readonly CadLayer _layer;
    private readonly string _oldColor;
    private readonly string _newColor;
    private readonly ICadEntity[] _byLayerEntities;

    public SetLayerColorAction(CadDocument document, CadLayer layer, string newColor)
    {
        _layer = layer;
        _oldColor = layer.ColorHex;
        _newColor = newColor;
        _byLayerEntities = []; // Existing entity colors are explicit, not inferred ByLayer.
    }

    public string Description => $"Color layer {_layer.Name} {_newColor}";

    public void Execute() => SetColor(_newColor);
    public void Undo() => SetColor(_oldColor);

    private void SetColor(string color)
    {
        _layer.ColorHex = color;
        foreach (var entity in _byLayerEntities) entity.ColorHex = color;
    }
}

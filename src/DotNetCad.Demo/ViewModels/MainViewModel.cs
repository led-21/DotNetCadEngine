using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Core.History;
using DotNetCad.Core.History.Actions;
using DotNetCad.Demo.Mvvm;
using DotNetCad.Infrastructure.Dxf;
using DotNetCad.Infrastructure.Native;
using DotNetCad.Infrastructure.Svg;
using Microsoft.Win32;

namespace DotNetCad.Demo.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private CadDocument _document = new();
    private CadToolType _activeTool = CadToolType.Select;
    private double _mouseWorldX;
    private double _mouseWorldY;
    private double _zoomLevel = 25.0;
    private int _selectedCount;
    private int _layerRevision;
    private int _drawingRevision;
    private string _consoleInput = string.Empty;
    private string? _nativePath;
    private bool _hasUnsavedChanges;
    private bool _suppressDirtyTracking;

    public Func<bool>? ZoomExtentsHandler { get; set; }
    public Action? ResetViewHandler { get; set; }

    public int DrawingRevision
    {
        get => _drawingRevision;
        private set => SetProperty(ref _drawingRevision, value);
    }

    public string ConsoleInput
    {
        get => _consoleInput;
        set => SetProperty(ref _consoleInput, value);
    }

    public ObservableCollection<string> ConsoleHistory { get; } = ["CAD console ready. Type HELP for commands."];

    public int LayerRevision
    {
        get => _layerRevision;
        private set => SetProperty(ref _layerRevision, value);
    }

    public CadDocument Document
    {
        get => _document;
        set
        {
            if (SetProperty(ref _document, value))
            {
                RefreshLayers();
                OnPropertyChanged(nameof(IsOrthoEnabled));
                OnPropertyChanged(nameof(IsPolarEnabled));
                OnPropertyChanged(nameof(IsOsnapEnabled));
            }
        }
    }

    public CadToolType ActiveTool
    {
        get => _activeTool;
        set => SetProperty(ref _activeTool, value);
    }

    public UndoRedoManager UndoManager { get; } = new();

    public double MouseWorldX
    {
        get => _mouseWorldX;
        set => SetProperty(ref _mouseWorldX, value);
    }

    public double MouseWorldY
    {
        get => _mouseWorldY;
        set => SetProperty(ref _mouseWorldY, value);
    }

    public double ZoomLevel
    {
        get => _zoomLevel;
        set => SetProperty(ref _zoomLevel, value);
    }

    public int SelectedCount
    {
        get => _selectedCount;
        set => SetProperty(ref _selectedCount, value);
    }

    public bool IsOrthoEnabled
    {
        get => Document.Preferences.IsOrthoModeEnabled;
        set
        {
            if (Document.Preferences.IsOrthoModeEnabled != value)
            {
                Document.Preferences.IsOrthoModeEnabled = value;
                _hasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public bool IsPolarEnabled
    {
        get => Document.Preferences.IsPolarTrackingEnabled;
        set
        {
            if (Document.Preferences.IsPolarTrackingEnabled != value)
            {
                Document.Preferences.IsPolarTrackingEnabled = value;
                _hasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public bool IsOsnapEnabled
    {
        get => Document.Preferences.IsOsnapEnabled;
        set
        {
            if (Document.Preferences.IsOsnapEnabled != value)
            {
                Document.Preferences.IsOsnapEnabled = value;
                _hasUnsavedChanges = true;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<LayerItemViewModel> Layers { get; } = [];

    public ICommand SetToolCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand ToggleOrthoCommand { get; }
    public ICommand TogglePolarCommand { get; }
    public ICommand ToggleOsnapCommand { get; }
    public ICommand ExportDxfCommand { get; }
    public ICommand ExportSvgCommand { get; }
    public ICommand ImportDxfCommand { get; }
    public ICommand SaveDrawingCommand { get; }
    public ICommand OpenDrawingCommand { get; }
    public ICommand NewDrawingCommand { get; }
    public ICommand LoadSampleDrawingCommand { get; }
    public ICommand ExecuteConsoleCommand { get; }
    public ICommand ZoomExtentsCommand { get; }
    public ICommand ResetViewCommand { get; }
    public ICommand DeleteSelectedCommand { get; }
    public ICommand ClearDrawingCommand { get; }
    public ICommand AddLayerCommand { get; }
    public ICommand ShowAllLayersCommand { get; }
    public ICommand AddTextPromptCommand { get; }
    public ICommand MoveSelectedCommand { get; }
    public ICommand CopySelectedCommand { get; }
    public ICommand RotateSelectedCommand { get; }
    public ICommand MirrorSelectedCommand { get; }
    public ICommand OffsetSelectedCommand { get; }

    private string _newLayerInput = string.Empty;
    public string NewLayerInput
    {
        get => _newLayerInput;
        set => SetProperty(ref _newLayerInput, value);
    }

    public MainViewModel()
    {
        UndoManager.PropertyChanged += (_, args) =>
        {
            if (!_suppressDirtyTracking && args.PropertyName == nameof(UndoRedoManager.UndoCount))
                _hasUnsavedChanges = true;
        };
        SetToolCommand = new RelayCommand(p =>
        {
            if (p is CadToolType tool) ActiveTool = tool;
            else if (p is string toolStr && Enum.TryParse<CadToolType>(toolStr, true, out var parsedTool)) ActiveTool = parsedTool;
        });

        UndoCommand = new RelayCommand(() => ApplyHistoryChange(undo: true), () => UndoManager.CanUndo);
        RedoCommand = new RelayCommand(() => ApplyHistoryChange(undo: false), () => UndoManager.CanRedo);

        ToggleOrthoCommand = new RelayCommand(() => IsOrthoEnabled = !IsOrthoEnabled);
        TogglePolarCommand = new RelayCommand(() => IsPolarEnabled = !IsPolarEnabled);
        ToggleOsnapCommand = new RelayCommand(() => IsOsnapEnabled = !IsOsnapEnabled);

        ExportDxfCommand = new RelayCommand(ExportDxf);
        ExportSvgCommand = new RelayCommand(ExportSvg);
        ImportDxfCommand = new RelayCommand(ImportDxf);
        SaveDrawingCommand = new RelayCommand(SaveDrawingFromToolbar);
        OpenDrawingCommand = new RelayCommand(OpenDrawingFromToolbar);
        NewDrawingCommand = new RelayCommand(NewDrawingFromToolbar);
        LoadSampleDrawingCommand = new RelayCommand(LoadSampleDrawing);
        ZoomExtentsCommand = new RelayCommand(() =>
        {
            if (ZoomExtentsHandler?.Invoke() == true)
                AppendConsole("CAD: Desenho ajustado ao viewport.");
            else
                AppendConsole("CAD: Nenhum elemento visível para enquadrar.");
        });
        ResetViewCommand = new RelayCommand(() =>
        {
            ResetViewHandler?.Invoke();
            AppendConsole("CAD: Posição e zoom da tela redefinidos.");
        });
        DeleteSelectedCommand = new RelayCommand(DeleteSelectedEntities);
        ClearDrawingCommand = new RelayCommand(ClearDrawing);
        AddLayerCommand = new RelayCommand(AddLayerFromInput);
        ShowAllLayersCommand = new RelayCommand(ShowAllLayers);
        AddTextPromptCommand = new RelayCommand(AddTextPrompt);
        MoveSelectedCommand = new RelayCommand(MoveSelectedEntities);
        CopySelectedCommand = new RelayCommand(CopySelectedEntities);
        RotateSelectedCommand = new RelayCommand(RotateSelectedEntities);
        MirrorSelectedCommand = new RelayCommand(MirrorSelectedEntities);
        OffsetSelectedCommand = new RelayCommand(OffsetSelectedEntities);
        ExecuteConsoleCommand = new RelayCommand(() =>
        {
            var input = ConsoleInput;
            ConsoleInput = string.Empty;
            RunCadCommand(input);
        });

        LoadSampleDrawing();
    }

    private void RefreshLayers()
    {
        Layers.Clear();
        foreach (var l in Document.Layers)
        {
            var item = new LayerItemViewModel(l, OnLayerVisibilityChanged, SetActiveLayer);
            item.SetActive(l.Name.Equals(Document.ActiveLayer, StringComparison.OrdinalIgnoreCase));
            Layers.Add(item);
        }
        LayerRevision++;
    }

    private void ApplyHistoryChange(bool undo)
    {
        if (undo) UndoManager.Undo();
        else UndoManager.Redo();
        DrawingRevision++;
        RefreshLayers();
    }

    private void OnLayerVisibilityChanged()
    {
        _hasUnsavedChanges = true;
        if (Document.GetLayer(Document.ActiveLayer)?.IsVisible == false)
        {
            var next = Document.Layers.FirstOrDefault(layer => layer.IsVisible);
            if (next != null) SetActiveLayer(next.Name);
        }
        LayerRevision++;
    }

    private void SetActiveLayer(string name)
    {
        var layer = Document.GetLayer(name);
        if (layer == null || !layer.IsVisible) return;
        if (!Document.ActiveLayer.Equals(layer.Name, StringComparison.OrdinalIgnoreCase)) _hasUnsavedChanges = true;
        Document.ActiveLayer = layer.Name;
        foreach (var item in Layers) item.SetActive(item.Name.Equals(layer.Name, StringComparison.OrdinalIgnoreCase));
    }

    public CadCommandResult RunCadCommand(string input, bool fromMcp = false)
    {
        if (string.IsNullOrWhiteSpace(input)) return new CadCommandResult(false, "Enter a command.");
        AppendConsole($"{(fromMcp ? "MCP" : "CAD")}> {input.Trim()}");
        var trimmed = input.Trim();
        var result = RunApplicationCommand(trimmed) ?? CadCommandProcessor.Execute(Document, UndoManager, trimmed);
        AppendConsole($"{(result.Success ? "OK" : "ERROR")}: {result.Message}");
        if (result.DrawingChanged) DrawingRevision++;
        if (result.LayersChanged)
        {
            RefreshLayers();
            _hasUnsavedChanges = true;
        }
        return result;
    }

    private CadCommandResult? RunApplicationCommand(string input)
    {
        if (input.Equals("HELP", StringComparison.OrdinalIgnoreCase))
            return new(true, CadCommandProcessor.Help + " | ZOOM EXTENTS | SAVE [path.cad] | OPEN [DISCARD] path.cad|path.dxf | NEW [DISCARD]");
        if (input.Equals("ZOOM EXTENTS", StringComparison.OrdinalIgnoreCase))
            return ZoomExtentsHandler?.Invoke() == true
                ? new(true, "Visible drawing fitted to the viewport.")
                : new(false, "No visible entities or viewport size available to fit.");
        if (input.Equals("NEW", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("NEW DISCARD", StringComparison.OrdinalIgnoreCase))
        {
            if (_hasUnsavedChanges && !input.EndsWith("DISCARD", StringComparison.OrdinalIgnoreCase))
                return new(false, "Unsaved changes. Use SAVE first, or NEW DISCARD to replace this drawing.");
            ReplaceDocument(new CadDocument(), null, false);
            ResetViewHandler?.Invoke();
            return new(true, "New empty drawing.", DrawingChanged: true);
        }
        if (input.Equals("SAVE", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("SAVE ", StringComparison.OrdinalIgnoreCase))
            return SaveNativeDrawing(ParsePath(input[4..]) ?? _nativePath);
        if (input.StartsWith("OPEN ", StringComparison.OrdinalIgnoreCase))
        {
            var argument = input[5..].Trim();
            var discard = argument.StartsWith("DISCARD ", StringComparison.OrdinalIgnoreCase);
            if (discard) argument = argument[8..].Trim();
            if (_hasUnsavedChanges && !discard)
                return new(false, "Unsaved changes. Use SAVE first, or OPEN DISCARD path to replace this drawing.");
            return OpenDrawing(ParsePath(argument));
        }
        return null;
    }

    private static string? ParsePath(string argument)
    {
        var path = argument.Trim();
        if (path.Length >= 2 && path[0] == '"' && path[^1] == '"') path = path[1..^1];
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    private CadCommandResult SaveNativeDrawing(string? path)
    {
        if (path == null) return new(false, "Use SAVE path.cad for a new drawing.");
        string? temporaryPath = null;
        var oldTitle = Document.Title;
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!Path.GetExtension(fullPath).Equals(".cad", StringComparison.OrdinalIgnoreCase))
                return new(false, "Native drawings must use the .cad extension.");
            Document.Title = Path.GetFileName(fullPath);
            var content = CadDrawingSerializer.Serialize(Document);
            temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporaryPath, content);
            File.Move(temporaryPath, fullPath, overwrite: true);
            _nativePath = fullPath;
            _hasUnsavedChanges = false;
            return new(true, $"Saved drawing to {fullPath}.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Text.Json.JsonException)
        {
            Document.Title = oldTitle;
            return new(false, $"Could not save drawing: {ex.Message}");
        }
        finally
        {
            if (temporaryPath != null)
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private CadCommandResult OpenDrawing(string? path)
    {
        if (path == null) return new(false, "Use OPEN path.cad or OPEN path.dxf.");
        try
        {
            var fullPath = Path.GetFullPath(path);
            var extension = Path.GetExtension(fullPath).ToLowerInvariant();
            if (extension is not (".cad" or ".dxf"))
                return new(false, "OPEN supports .cad and .dxf files.");
            var content = File.ReadAllText(fullPath);
            var opened = extension == ".cad"
                ? CadDrawingSerializer.Deserialize(content)
                : DxfCadImporter.ImportFromDxf(content, Path.GetFileName(fullPath));
            opened.Title = Path.GetFileName(fullPath);
            ReplaceDocument(opened, extension == ".cad" ? fullPath : null, extension == ".dxf");
            ZoomExtentsHandler?.Invoke();
            return new(true, $"Opened {opened.Entities.Count} entities from {fullPath}.", DrawingChanged: true);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Text.Json.JsonException)
        {
            return new(false, $"Could not open drawing: {ex.Message}");
        }
    }

    private void ReplaceDocument(CadDocument document, string? nativePath, bool hasUnsavedChanges)
    {
        _suppressDirtyTracking = true;
        try
        {
            Document = document;
            UndoManager.Clear();
            _nativePath = nativePath;
            _hasUnsavedChanges = hasUnsavedChanges;
        }
        finally { _suppressDirtyTracking = false; }
    }

    public void AppendConsole(string line)
    {
        foreach (var part in line.Split(["\r\n", "\n"], StringSplitOptions.None))
            ConsoleHistory.Add(part);
        while (ConsoleHistory.Count > 200) ConsoleHistory.RemoveAt(0);
    }

    private void DeleteSelectedEntities()
    {
        var toDelete = Document.Entities.Where(e => e.IsSelected).ToList();
        if (toDelete.Count > 0)
        {
            var actions = toDelete.Select(e => (IUndoableAction)new DeleteEntityAction(Document, e)).ToList();
            UndoManager.ExecuteAction(new CompositeUndoableAction(actions, $"Delete {toDelete.Count} entities"));
            SelectedCount = 0;
            DrawingRevision++;
            _hasUnsavedChanges = true;
            AppendConsole($"CAD: {toDelete.Count} entidade(s) excluída(s).");
        }
        else
        {
            AppendConsole("CAD: Nenhuma entidade selecionada para excluir.");
        }
    }

    private void ClearDrawing()
    {
        if (Document.Entities.Count == 0) return;
        if (MessageBox.Show("Deseja realmente limpar todas as entidades do desenho atual?", "Limpar Desenho", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            RunCadCommand("CLEAR");
        }
    }

    private void AddLayerFromInput()
    {
        var name = NewLayerInput?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            AppendConsole("CAD ERROR: Informe o nome da nova camada.");
            return;
        }
        RunCadCommand($"LAYER {name}");
        NewLayerInput = string.Empty;
    }

    private void ShowAllLayers()
    {
        foreach (var layer in Document.Layers)
        {
            layer.IsVisible = true;
        }
        RefreshLayers();
        _hasUnsavedChanges = true;
        DrawingRevision++;
        AppendConsole("CAD: Todas as camadas estão visíveis.");
    }

    private void AddTextPrompt()
    {
        var x = Math.Round(MouseWorldX, 1);
        var y = Math.Round(MouseWorldY, 1);
        RunCadCommand($"TEXT {x};{y} 1.0 Anotação");
    }

    private void MoveSelectedEntities()
    {
        var selected = Document.Entities.Where(e => e.IsSelected).ToList();
        if (selected.Count == 0)
        {
            AppendConsole("CAD: Selecione ao menos uma entidade para mover (ou use MOVE id dx;dy).");
            return;
        }

        var actions = selected.Select(e => (IUndoableAction)new MoveEntityAction(e, 1.0, 0.0)).ToList();
        UndoManager.ExecuteAction(new CompositeUndoableAction(actions, $"Move {selected.Count} entities"));
        _hasUnsavedChanges = true;
        DrawingRevision++;
        AppendConsole($"CAD: {selected.Count} entidade(s) movida(s) (+1.0m no eixo X).");
    }

    private void CopySelectedEntities()
    {
        var selected = Document.Entities.Where(e => e.IsSelected).ToList();
        if (selected.Count == 0)
        {
            AppendConsole("CAD: Selecione ao menos uma entidade para copiar (ou use COPY id dx;dy).");
            return;
        }

        var actions = new List<IUndoableAction>();
        foreach (var entity in selected)
        {
            var copy = entity.Clone();
            copy.Translate(1.0, 1.0);
            actions.Add(new AddEntityAction(Document, copy));
        }
        UndoManager.ExecuteAction(new CompositeUndoableAction(actions, $"Copy {selected.Count} entities"));
        _hasUnsavedChanges = true;
        DrawingRevision++;
        AppendConsole($"CAD: {selected.Count} cópia(s) gerada(s) (+1.0m, +1.0m).");
    }

    private void RotateSelectedEntities()
    {
        var selected = Document.Entities.Where(e => e.IsSelected).ToList();
        if (selected.Count == 0)
        {
            AppendConsole("CAD: Selecione ao menos uma entidade para rotacionar (ou use ROTATE id x;y angulo).");
            return;
        }

        var actions = new List<IUndoableAction>();
        foreach (var entity in selected)
        {
            var center = entity.BoundingBox.Center;
            actions.Add(new RotateEntityAction(entity, center, 90.0));
        }
        UndoManager.ExecuteAction(new CompositeUndoableAction(actions, $"Rotate {selected.Count} entities 90°"));
        _hasUnsavedChanges = true;
        DrawingRevision++;
        AppendConsole($"CAD: {selected.Count} entidade(s) rotacionada(s) em 90° em torno do centro.");
    }

    private void MirrorSelectedEntities()
    {
        var selected = Document.Entities.Where(e => e.IsSelected).ToList();
        if (selected.Count == 0)
        {
            AppendConsole("CAD: Selecione ao menos uma entidade para espelhar (ou use MIRROR id x1;y1 x2;y2).");
            return;
        }

        var actions = new List<IUndoableAction>();
        foreach (var entity in selected)
        {
            var center = entity.BoundingBox.Center;
            var axisP1 = new Point2D(center.X, center.Y - 5.0);
            var axisP2 = new Point2D(center.X, center.Y + 5.0);
            actions.Add(new MirrorEntityAction(entity, axisP1, axisP2));
        }
        UndoManager.ExecuteAction(new CompositeUndoableAction(actions, $"Mirror {selected.Count} entities"));
        _hasUnsavedChanges = true;
        DrawingRevision++;
        AppendConsole($"CAD: {selected.Count} entidade(s) espelhada(s) no eixo vertical central.");
    }

    private void OffsetSelectedEntities()
    {
        var selected = Document.Entities.Where(e => e.IsSelected).ToList();
        if (selected.Count == 0)
        {
            AppendConsole("CAD: Selecione ao menos uma entidade para criar offset (ou use OFFSET id distancia).");
            return;
        }

        var actions = new List<IUndoableAction>();
        foreach (var entity in selected)
        {
            actions.Add(new OffsetEntityAction(Document, entity, 0.5));
        }
        UndoManager.ExecuteAction(new CompositeUndoableAction(actions, $"Offset {selected.Count} entities"));
        _hasUnsavedChanges = true;
        DrawingRevision++;
        AppendConsole($"CAD: Offset de +0.5m aplicado em {selected.Count} entidade(s).");
    }

    private void LoadSampleDrawing()
    {
        var doc = new CadDocument { Title = "MechanicalBracket.cad" };

        // Sample synthetic technical CAD geometry
        doc.AddEntity(new CadRectangle(new Point2D(-10, -5), width: 20, height: 10, rotationDegrees: 0, layer: "Geometry", colorHex: "#38BDF8"));
        doc.AddEntity(new CadCircle(new Point2D(0, 0), radius: 2.5, layer: "Geometry", colorHex: "#38BDF8"));
        doc.AddEntity(new CadCircle(new Point2D(-6, 0), radius: 1.2, layer: "Geometry", colorHex: "#38BDF8"));
        doc.AddEntity(new CadCircle(new Point2D(6, 0), radius: 1.2, layer: "Geometry", colorHex: "#38BDF8"));
        doc.AddEntity(new CadLine(new Point2D(-12, 0), new Point2D(12, 0), layer: "Hidden", colorHex: "#94A3B8"));
        doc.AddEntity(new CadLine(new Point2D(0, -7), new Point2D(0, 7), layer: "Hidden", colorHex: "#94A3B8"));

        doc.AddEntity(new CadDimension
        {
            StartPoint = new Point2D(-10, 5),
            EndPoint = new Point2D(10, 5),
            OffsetPoint = new Point2D(0, 7),
            Prefix = "L = "
        });

        ReplaceDocument(doc, null, false);
    }

    private void SaveDrawingFromToolbar()
    {
        if (_nativePath != null)
        {
            ShowCommandError(RunCadCommand("SAVE"));
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "DotNetCad Drawing (*.cad)|*.cad",
            DefaultExt = ".cad",
            AddExtension = true,
            FileName = Path.GetExtension(Document.Title).Equals(".cad", StringComparison.OrdinalIgnoreCase)
                ? Document.Title : "Drawing.cad"
        };
        if (dialog.ShowDialog() == true)
            ShowCommandError(RunCadCommand($"SAVE \"{dialog.FileName}\""));
    }

    private void OpenDrawingFromToolbar()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CAD Drawings (*.cad;*.dxf)|*.cad;*.dxf|DotNetCad Drawing (*.cad)|*.cad|AutoCAD DXF (*.dxf)|*.dxf",
            DefaultExt = ".cad",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() != true) return;
        if (!ConfirmDiscardUnsaved()) return;
        var discard = _hasUnsavedChanges ? "DISCARD " : string.Empty;
        ShowCommandError(RunCadCommand($"OPEN {discard}\"{dialog.FileName}\""));
    }

    private void NewDrawingFromToolbar()
    {
        if (!ConfirmDiscardUnsaved()) return;
        ShowCommandError(RunCadCommand(_hasUnsavedChanges ? "NEW DISCARD" : "NEW"));
    }

    private bool ConfirmDiscardUnsaved() => !_hasUnsavedChanges ||
        MessageBox.Show("O desenho atual tem alterações não salvas. Deseja descartá-las?",
            "Alterações não salvas", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private static void ShowCommandError(CadCommandResult result)
    {
        if (!result.Success)
            MessageBox.Show(result.Message, "DotNetCad", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void ExportDxf()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "AutoCAD DXF File (*.dxf)|*.dxf",
            DefaultExt = "dxf",
            FileName = "DrawingExport.dxf"
        };

        if (dialog.ShowDialog() == true)
        {
            var content = DxfCadExporter.ExportToDxf(Document);
            File.WriteAllText(dialog.FileName, content);
            MessageBox.Show($"Drawing successfully exported to DXF:\n{dialog.FileName}", "DXF Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ExportSvg()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Scalable Vector Graphics (*.svg)|*.svg",
            DefaultExt = "svg",
            FileName = "DrawingPlot.svg"
        };

        if (dialog.ShowDialog() == true)
        {
            var content = SvgCadExporter.ExportToSvg(Document);
            File.WriteAllText(dialog.FileName, content);
            MessageBox.Show($"Drawing successfully exported to SVG:\n{dialog.FileName}", "SVG Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ImportDxf()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "AutoCAD DXF File (*.dxf)|*.dxf",
            DefaultExt = "dxf"
        };

        if (dialog.ShowDialog() == true)
        {
            var content = File.ReadAllText(dialog.FileName);
            var imported = DxfCadImporter.ImportFromDxf(content, Path.GetFileName(dialog.FileName));
            Document = imported;
            UndoManager.Clear();
            _nativePath = null;
            _hasUnsavedChanges = true;
            MessageBox.Show($"Successfully loaded {imported.Entities.Count} entities from DXF.", "DXF Import", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

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
    private string? _savedContent;
    public bool HasUnsavedChanges
    {
        get
        {
            try { return _savedContent == null || CaptureContent() != _savedContent; }
            catch (Exception ex) when (ex is ArgumentException or System.Text.Json.JsonException or InvalidOperationException)
            { return true; } // Invalid geometry must never bypass the pending-changes guard.
        }
    }
    // Injectable UI choices also allow cancellation/failure paths to be tested without dialogs.
    public Func<MessageBoxResult> UnsavedChangesPrompt { get; set; } = () => MessageBox.Show(
        "Salvar as alterações antes de continuar?\nSim: Salvar · Não: Descartar · Cancelar: permanecer no desenho",
        "Alterações não salvas", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
    public Func<string?>? SavePathPrompt { get; set; }
    private string CaptureContent()
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(CadDrawingSerializer.Serialize(Document))!;
        foreach (var entity in node["Entities"]!.AsArray()) entity!["Data"]!.AsObject().Remove("IsSelected");
        return node.ToJsonString();
    }
    public bool ConfirmPendingChanges()
    {
        if (!HasUnsavedChanges) return true;
        return UnsavedChangesPrompt() switch
        {
            MessageBoxResult.No => true,
            MessageBoxResult.Yes => SaveDrawingInteractively(),
            _ => false
        };
    }


    public Func<bool>? ZoomExtentsHandler { get; set; }
    public Action? ResetViewHandler { get; set; }

    public int DrawingRevision
    {
        get => _drawingRevision;
        private set
        {
            if (SetProperty(ref _drawingRevision, value)) CommandManager.InvalidateRequerySuggested();
        }
    }

    public string ConsoleInput
    {
        get => _consoleInput;
        set => SetProperty(ref _consoleInput, value);
    }

    public CadConsoleModel Console { get; } = new();
    public System.Windows.Threading.Dispatcher? UiDispatcher { get; set; }
    public Func<bool>? InteractiveOperationActive { get; set; }
    public Action? CancelInteractiveOperation { get; set; }
    private bool _executingCommand;
    private bool MutationBlocked => _executingCommand || InteractiveOperationActive?.Invoke()==true;
    public CadCommandResult ExecuteConsoleEntry()
    {
        var input=ConsoleInput;
        if(string.IsNullOrWhiteSpace(input))return new(true,"");
        Console.Remember(input);
        var result=RunCadCommand(input);
        if(result.Success)ConsoleInput="";
        return result;
    }

    public int LayerRevision
    {
        get => _layerRevision;
        private set
        {
            if (SetProperty(ref _layerRevision, value)) CommandManager.InvalidateRequerySuggested();
        }
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
                OnPropertyChanged(nameof(OrthoLabel)); OnPropertyChanged(nameof(PolarLabel)); OnPropertyChanged(nameof(OsnapLabel));
            }
        }
    }

    public CadToolType ActiveTool
    {
        get => _activeTool;
        set { if (SetProperty(ref _activeTool, value)) OnPropertyChanged(nameof(ActiveToolLabel)); }
    }

    public string ActiveToolLabel => ActiveTool switch {
        CadToolType.Select => "Selecionar", CadToolType.Line => "Linha", CadToolType.Polyline => "Polilinha",
        CadToolType.Rectangle => "Retângulo", CadToolType.Circle => "Círculo", CadToolType.Arc => "Arco",
        CadToolType.Dimension => "Cota", CadToolType.Move => "Mover", CadToolType.Copy => "Copiar",
        CadToolType.Rotate => "Rotacionar", CadToolType.Mirror => "Espelhar", CadToolType.Offset => "Offset",
        CadToolType.Text => "Texto", CadToolType.Pan => "Mover vista", _ => ActiveTool.ToString() };
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
        set
        {
            if (SetProperty(ref _selectedCount, value)) CommandManager.InvalidateRequerySuggested();
        }
    }

    public string OrthoLabel => IsOrthoEnabled ? "ativo" : "inativo";
    public string PolarLabel => IsPolarEnabled ? "ativo" : "inativo";
    public string OsnapLabel => IsOsnapEnabled ? "ativa" : "inativa";

    public bool IsOrthoEnabled
    {
        get => Document.Preferences.IsOrthoModeEnabled;
        set
        {
            if (Document.Preferences.IsOrthoModeEnabled != value)
            {
                Document.Preferences.IsOrthoModeEnabled = value;
                OnPropertyChanged(nameof(OrthoLabel));

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
                OnPropertyChanged(nameof(PolarLabel));

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
                OnPropertyChanged(nameof(OsnapLabel));

                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<LayerItemViewModel> Layers { get; } = [];
    public System.ComponentModel.ICollectionView FilteredLayers => System.Windows.Data.CollectionViewSource.GetDefaultView(Layers);
    private string _layerFilter = "";
    public string LayerFilter { get=>_layerFilter; set { if(SetProperty(ref _layerFilter,value)) ApplyLayerFilter(); } }
    private LayerItemViewModel? _selectedLayer;
    public LayerItemViewModel? SelectedLayer { get=>_selectedLayer; set=>SetProperty(ref _selectedLayer,value); }
    private void ApplyLayerFilter() { FilteredLayers.Filter=o=>o is LayerItemViewModel l && l.Name.Contains(_layerFilter,StringComparison.OrdinalIgnoreCase); }
    public CadCommandResult OperateLayer(string op,string name,string? argument=null,LayerDeleteMode mode=LayerDeleteMode.Cancel)
    {
        if(MutationBlocked) { var blocked=new CadCommandResult(false,"Conclua ou cancele a operação interativa antes de alterar camadas.");AppendConsole("ERRO: "+blocked.Message);return blocked; }
        var result=CadLayerOperations.Execute(Document,UndoManager,op,name,argument,mode);
        AppendConsole((result.Success?"CAD: ":"ERRO: ")+result.Message);
        if(result.Success && result.LayersChanged) { DrawingRevision++;RefreshLayers(); }
        return result;
    }
    private void RenameLayer(string name)
    {
        var next=LayerDialogs.Rename(name);if(next!=null)OperateLayer("RENAME",name,next);
    }
    private void DeleteLayer(string name)
    {
        var count=Document.Entities.Count(e=>e.Layer.Equals(name,StringComparison.OrdinalIgnoreCase));
        var choice=LayerDialogs.Delete(name,count,Document.Layers.Where(l=>!l.Name.Equals(name,StringComparison.OrdinalIgnoreCase) && l.IsVisible && !l.IsLocked).Select(l=>l.Name));
        if(choice.Mode!=LayerDeleteMode.Cancel)OperateLayer("DELETE",name,choice.Target,choice.Mode);
    }

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
        UndoManager.PropertyChanged += (_, e) => { if(e.PropertyName == nameof(UndoRedoManager.UndoCount)) foreach(var item in Layers)item.RefreshCounts(); };
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
        LoadSampleDrawingCommand = new RelayCommand(() => { if(MutationBlocked){AppendConsole("ERRO: conclua ou cancele a operação ativa.");return;} if (ConfirmPendingChanges()) LoadSampleDrawing(); });
        ZoomExtentsCommand = new RelayCommand(() =>
        {
            if (ZoomExtentsHandler?.Invoke() == true)
                AppendConsole("CAD: Desenho ajustado ao viewport.");
            else
                AppendConsole("CAD: Nenhum elemento visível para enquadrar.");
        }, () => Document.Entities.Any(e => Document.GetLayer(e.Layer)?.IsVisible != false));
        ResetViewCommand = new RelayCommand(() =>
        {
            ResetViewHandler?.Invoke();
            AppendConsole("CAD: Posição e zoom da tela redefinidos.");
        });
        DeleteSelectedCommand = new RelayCommand(DeleteSelectedEntities, () => Document.Entities.Any(e => e.IsSelected));
        ClearDrawingCommand = new RelayCommand(ClearDrawing, () => Document.Entities.Count > 0);
        AddLayerCommand = new RelayCommand(AddLayerFromInput);
        ShowAllLayersCommand = new RelayCommand(ShowAllLayers);
        AddTextPromptCommand = new RelayCommand(AddTextPrompt);
        MoveSelectedCommand = new RelayCommand(MoveSelectedEntities);
        CopySelectedCommand = new RelayCommand(CopySelectedEntities);
        RotateSelectedCommand = new RelayCommand(RotateSelectedEntities);
        MirrorSelectedCommand = new RelayCommand(MirrorSelectedEntities);
        OffsetSelectedCommand = new RelayCommand(OffsetSelectedEntities);
        ExecuteConsoleCommand = new RelayCommand(() => ExecuteConsoleEntry());

        LoadSampleDrawing();
    }

    private void RefreshLayers()
    {
        var selectedName=SelectedLayer?.Name;
        Layers.Clear();
        foreach (var l in Document.Layers)
        {
            var name=l.Name;
            var item = new LayerItemViewModel(l,()=>Document.Entities.Count(e=>e.Layer.Equals(name,StringComparison.OrdinalIgnoreCase)),
                (op,arg,mode)=>OperateLayer(op,name,arg,mode),()=>RenameLayer(name),()=>DeleteLayer(name));
            item.SetActive(l.Name.Equals(Document.ActiveLayer, StringComparison.OrdinalIgnoreCase));
            Layers.Add(item);
        }
        SelectedLayer=Layers.FirstOrDefault(l=>l.Name==selectedName);
        ApplyLayerFilter();
        LayerRevision++;
    }

    private void ApplyHistoryChange(bool undo)
    {
        if(MutationBlocked){AppendConsole("ERRO: conclua ou cancele a operação ativa antes de desfazer/refazer.");return;}
        if (undo) UndoManager.Undo();
        else UndoManager.Redo();
        DrawingRevision++;
        RefreshLayers();
    }

    public CadCommandResult RunCadCommand(string input, bool fromMcp = false)
    {
        if(UiDispatcher is { } dispatcher && !dispatcher.CheckAccess())return dispatcher.Invoke(()=>RunCadCommand(input,fromMcp));
        if(string.IsNullOrWhiteSpace(input))return new(false,"Informe um comando.");
        var origin=fromMcp?ConsoleOrigin.MCP:ConsoleOrigin.Usuario;
        if(_executingCommand || InteractiveOperationActive?.Invoke()==true && !CadCommandCatalog.IsReadOnly(input))
        {
            var blocked=new CadCommandResult(false,"Operação interativa em andamento. Conclua no viewport ou use Cancelar operação; a entrada do console inicia um novo comando.");
            Console.Add(blocked.Message,origin,ConsoleSeverity.Aviso);return blocked;
        }
        if(input.Length>CadConsoleModel.InputLimit)return new(false,"Comando excede o limite de entrada.");
        _executingCommand=true;
        try
        {
            Console.Add(input.Trim(),origin,command:true);
            var trimmed=input.Trim();
            var result=RunApplicationCommand(trimmed) ?? CadCommandProcessor.Execute(Document,UndoManager,trimmed);
            Console.Add(result.Message,origin,result.Success?ConsoleSeverity.Informacao:ConsoleSeverity.Erro);
            if(result.DrawingChanged)DrawingRevision++;
            if(result.LayersChanged)RefreshLayers();
            return result;
        }
        finally {_executingCommand=false;}
    }

    private CadCommandResult? RunApplicationCommand(string input)
    {
        if (input.Equals("HELP", StringComparison.OrdinalIgnoreCase))
            return new(true, CadCommandCatalog.Help);
        if (input.Equals("ZOOM EXTENTS", StringComparison.OrdinalIgnoreCase))
            return ZoomExtentsHandler?.Invoke() == true
                ? new(true, "Visible drawing fitted to the viewport.")
                : new(false, "No visible entities or viewport size available to fit.");
        if (input.Equals("NEW", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("NEW DISCARD", StringComparison.OrdinalIgnoreCase))
        {
            if (HasUnsavedChanges && !input.EndsWith("DISCARD", StringComparison.OrdinalIgnoreCase))
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
            if (HasUnsavedChanges && !discard)
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
            _savedContent = CaptureContent();
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

    private CadCommandResult OpenDrawing(string? path, bool protectPending = false)
    {
        if (path == null) return new(false, "Use OPEN path.cad or OPEN path.dxf.");
        try
        {
            var fullPath = Path.GetFullPath(path);
            var extension = Path.GetExtension(fullPath).ToLowerInvariant();
            if (extension is not (".cad" or ".dxf"))
                return new(false, "OPEN supports .cad and .dxf files.");
            var content = File.ReadAllText(fullPath);
            if (extension == ".dxf")
            {
                var lines = content.Replace("\r", "").Split('\n').Select(line => line.Trim()).ToList();
                while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count-1);
                if (lines.Count < 6 || lines.Count % 2 != 0 || lines[^2] != "0" || lines[^1] != "EOF" ||
                    !Enumerable.Range(0, lines.Count/2).All(i => int.TryParse(lines[2*i], out _)) ||
                    !lines.Contains("SECTION") || !lines.Contains("ENDSEC"))
                    return new(false, "DXF inválido ou incompleto: estrutura SECTION/ENDSEC/EOF ausente.");
            }
            var opened = extension == ".cad"
                ? CadDrawingSerializer.Deserialize(content)
                : DxfCadImporter.ImportFromDxf(content, Path.GetFileName(fullPath));
            // Reuse native validation for imported entities before committing replacement.
            opened = CadDrawingSerializer.Deserialize(CadDrawingSerializer.Serialize(opened));
            opened.Title = Path.GetFileName(fullPath);
            if (protectPending && !ConfirmPendingChanges()) return new(false, "Operação cancelada; desenho preservado.");
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
        Document = document;
        UndoManager.Clear();
        _nativePath = nativePath;
        _savedContent = hasUnsavedChanges ? null : CaptureContent();
        ActiveTool = CadToolType.Select;
        SelectedCount = 0;
    }

    public void AppendConsole(string line)
    {
        if(UiDispatcher is { } dispatcher && !dispatcher.CheckAccess()) {dispatcher.BeginInvoke(()=>AppendConsole(line));return;}
        Console.Add(line,line.StartsWith("MCP:")?ConsoleOrigin.MCP:ConsoleOrigin.Sistema,
            line.StartsWith("ERRO") || line.StartsWith("CAD ERROR")?ConsoleSeverity.Erro:ConsoleSeverity.Informacao);
    }

    private void DeleteSelectedEntities()
    {
        if(MutationBlocked){AppendConsole("ERRO: conclua ou cancele a operação ativa antes de excluir.");return;}
        var toDelete = Document.Entities.Where(e => e.IsSelected).ToList();
        if (toDelete.Any(e=>Document.GetLayer(e.Layer)?.IsLocked!=false)) { AppendConsole("ERRO: seleção contém camada bloqueada.");return; }
        if (toDelete.Count > 0)
        {
            var actions = toDelete.Select(e => (IUndoableAction)new DeleteEntityAction(Document, e)).ToList();
            UndoManager.ExecuteAction(new CompositeUndoableAction(actions, $"Delete {toDelete.Count} entities"));
            SelectedCount = 0;
            DrawingRevision++;

            AppendConsole($"CAD: {toDelete.Count} entidade(s) excluída(s).");
        }
        else
        {
            AppendConsole("CAD: Nenhuma entidade selecionada para excluir.");
        }
    }

    private void ClearDrawing()
    {
        if(MutationBlocked){AppendConsole("ERRO: conclua ou cancele a operação ativa.");return;}
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
        OperateLayer("CREATE",name);
        NewLayerInput = string.Empty;
    }

    private void ShowAllLayers() => RunCadCommand("LAYER SHOWALL");

    private void AddTextPrompt() => ActiveTool = CadToolType.Text;

    private void MoveSelectedEntities() => ActiveTool = CadToolType.Move;
    private void CopySelectedEntities() => ActiveTool = CadToolType.Copy;
    private void RotateSelectedEntities() => ActiveTool = CadToolType.Rotate;
    private void MirrorSelectedEntities() => ActiveTool = CadToolType.Mirror;
    private void OffsetSelectedEntities() => ActiveTool = CadToolType.Offset;

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

    private void SaveDrawingFromToolbar() => SaveDrawingInteractively();

    public bool SaveDrawingInteractively()
    {
        if (_nativePath != null)
        {
            var saved = RunCadCommand("SAVE");
            if (!saved.Success) AppendConsole(saved.Message);
            return saved.Success;
        }
        if (SavePathPrompt != null)
        {
            var path = SavePathPrompt();
            return path != null && RunCadCommand("SAVE " + path).Success;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "DotNetCad Drawing (*.cad)|*.cad",
            DefaultExt = ".cad",
            AddExtension = true,
            FileName = Path.GetExtension(Document.Title).Equals(".cad", StringComparison.OrdinalIgnoreCase)
                ? Document.Title : "Drawing.cad"
        };
        if (dialog.ShowDialog() != true) return false;
        var result = RunCadCommand("SAVE " + dialog.FileName);
        ShowCommandError(result);
        return result.Success;
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
        ShowCommandError(ReplaceFromFile(dialog.FileName));
    }

    private void NewDrawingFromToolbar()
    {
        if (!ConfirmDiscardUnsaved()) return;
        ShowCommandError(RunCadCommand(HasUnsavedChanges ? "NEW DISCARD" : "NEW"));
    }

    private bool ConfirmDiscardUnsaved() => ConfirmPendingChanges();

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

        if (dialog.ShowDialog() != true) return;
        ShowCommandError(ReplaceFromFile(dialog.FileName));
    }

    public CadCommandResult ReplaceFromFile(string path)
    {
        if(MutationBlocked)return new(false,"Conclua ou cancele a operação ativa antes de abrir um desenho.");
        // Stage and validate first; failed reads never prompt to discard the current drawing.
        return OpenDrawing(path, protectPending: true);
    }
}

using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Core.History;
using DotNetCad.Demo.Mvvm;
using DotNetCad.Infrastructure.Dxf;
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
    public ICommand LoadSampleDrawingCommand { get; }

    public MainViewModel()
    {
        SetToolCommand = new RelayCommand(p =>
        {
            if (p is CadToolType tool) ActiveTool = tool;
            else if (p is string toolStr && Enum.TryParse<CadToolType>(toolStr, true, out var parsedTool)) ActiveTool = parsedTool;
        });

        UndoCommand = new RelayCommand(() => UndoManager.Undo(), () => UndoManager.CanUndo);
        RedoCommand = new RelayCommand(() => UndoManager.Redo(), () => UndoManager.CanRedo);

        ToggleOrthoCommand = new RelayCommand(() => IsOrthoEnabled = !IsOrthoEnabled);
        TogglePolarCommand = new RelayCommand(() => IsPolarEnabled = !IsPolarEnabled);
        ToggleOsnapCommand = new RelayCommand(() => IsOsnapEnabled = !IsOsnapEnabled);

        ExportDxfCommand = new RelayCommand(ExportDxf);
        ExportSvgCommand = new RelayCommand(ExportSvg);
        ImportDxfCommand = new RelayCommand(ImportDxf);
        LoadSampleDrawingCommand = new RelayCommand(LoadSampleDrawing);

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

    private void OnLayerVisibilityChanged()
    {
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
        Document.ActiveLayer = layer.Name;
        foreach (var item in Layers) item.SetActive(item.Name.Equals(layer.Name, StringComparison.OrdinalIgnoreCase));
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

        Document = doc;
        UndoManager.Clear();
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
            MessageBox.Show($"Successfully loaded {imported.Entities.Count} entities from DXF.", "DXF Import", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

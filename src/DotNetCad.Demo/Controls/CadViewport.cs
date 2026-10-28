using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Core.Measurements;
using DotNetCad.Core.History;
using DotNetCad.Core.History.Actions;
using DotNetCad.Core.Selection;
using DotNetCad.Core.Snapping;

namespace DotNetCad.Demo.Controls;

public sealed class CadViewport : FrameworkElement
{
    #region Dependency Properties

    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(
        nameof(Document),
        typeof(CadDocument),
        typeof(CadViewport),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnDocumentChanged));

    public static readonly DependencyProperty LayerRevisionProperty = DependencyProperty.Register(
        nameof(LayerRevision), typeof(int), typeof(CadViewport),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender, OnLayerRevisionChanged));

    public static readonly DependencyProperty DrawingRevisionProperty = DependencyProperty.Register(
        nameof(DrawingRevision), typeof(int), typeof(CadViewport),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender, OnDrawingRevisionChanged));

    public static readonly DependencyProperty ActiveToolProperty = DependencyProperty.Register(
        nameof(ActiveTool),
        typeof(CadToolType),
        typeof(CadViewport),
        new FrameworkPropertyMetadata(CadToolType.Select, OnActiveToolChanged));

    public static readonly DependencyProperty UndoManagerProperty = DependencyProperty.Register(
        nameof(UndoManager),
        typeof(UndoRedoManager),
        typeof(CadViewport),
        new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty ToggleOrthoCommandProperty = DependencyProperty.Register(
        nameof(ToggleOrthoCommand), typeof(ICommand), typeof(CadViewport));
    public static readonly DependencyProperty TogglePolarCommandProperty = DependencyProperty.Register(
        nameof(TogglePolarCommand), typeof(ICommand), typeof(CadViewport));
    public static readonly DependencyProperty ToggleOsnapCommandProperty = DependencyProperty.Register(
        nameof(ToggleOsnapCommand), typeof(ICommand), typeof(CadViewport));

    public static readonly DependencyProperty MouseWorldXProperty = DependencyProperty.Register(
        nameof(MouseWorldX),
        typeof(double),
        typeof(CadViewport),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty MouseWorldYProperty = DependencyProperty.Register(
        nameof(MouseWorldY),
        typeof(double),
        typeof(CadViewport),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty ZoomLevelProperty = DependencyProperty.Register(
        nameof(ZoomLevel),
        typeof(double),
        typeof(CadViewport),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty SelectedTypeFilterProperty = DependencyProperty.Register(
        nameof(SelectedTypeFilter),
        typeof(CadEntityType?),
        typeof(CadViewport),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedLayerFilterProperty = DependencyProperty.Register(
        nameof(SelectedLayerFilter),
        typeof(string),
        typeof(CadViewport),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public CadEntityType? SelectedTypeFilter
    {
        get => (CadEntityType?)GetValue(SelectedTypeFilterProperty);
        set => SetValue(SelectedTypeFilterProperty, value);
    }

    public string? SelectedLayerFilter
    {
        get => (string?)GetValue(SelectedLayerFilterProperty);
        set => SetValue(SelectedLayerFilterProperty, value);
    }

    public static readonly DependencyProperty SelectedCountProperty = DependencyProperty.Register(
        nameof(SelectedCount),
        typeof(int),
        typeof(CadViewport),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public CadDocument? Document
    {
        get => (CadDocument?)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public int LayerRevision
    {
        get => (int)GetValue(LayerRevisionProperty);
        set => SetValue(LayerRevisionProperty, value);
    }

    public int DrawingRevision
    {
        get => (int)GetValue(DrawingRevisionProperty);
        set => SetValue(DrawingRevisionProperty, value);
    }

    public CadToolType ActiveTool
    {
        get => (CadToolType)GetValue(ActiveToolProperty);
        set => SetValue(ActiveToolProperty, value);
    }

    public UndoRedoManager? UndoManager
    {
        get => (UndoRedoManager?)GetValue(UndoManagerProperty);
        set => SetValue(UndoManagerProperty, value);
    }

    public ICommand? ToggleOrthoCommand
    {
        get => (ICommand?)GetValue(ToggleOrthoCommandProperty);
        set => SetValue(ToggleOrthoCommandProperty, value);
    }

    public ICommand? TogglePolarCommand
    {
        get => (ICommand?)GetValue(TogglePolarCommandProperty);
        set => SetValue(TogglePolarCommandProperty, value);
    }

    public ICommand? ToggleOsnapCommand
    {
        get => (ICommand?)GetValue(ToggleOsnapCommandProperty);
        set => SetValue(ToggleOsnapCommandProperty, value);
    }

    public double MouseWorldX
    {
        get => (double)GetValue(MouseWorldXProperty);
        set => SetValue(MouseWorldXProperty, value);
    }

    public double MouseWorldY
    {
        get => (double)GetValue(MouseWorldYProperty);
        set => SetValue(MouseWorldYProperty, value);
    }

    public double ZoomLevel
    {
        get => (double)GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    public int SelectedCount
    {
        get => (int)GetValue(SelectedCountProperty);
        set => SetValue(SelectedCountProperty, value);
    }

    #endregion

    public bool HasInteractiveOperation => _modification != null || _trimExtendSession != null || ActiveTool == CadToolType.Text || _draftStartPoint != null || _polyDraftPoints.Count > 0 || _dimensionStep > 0 || _isDraggingGrip;
    public void CancelOperation()
    {
        ResetDraftState(); _modification=null; _parameterText=""; _textPreview=null; _trimExtendSession=null;
        ReleaseMouseCapture(); ActiveTool=CadToolType.Select; InvalidateVisual();
    }
    public static readonly DependencyProperty CommandStatusProperty = DependencyProperty.Register(nameof(CommandStatus),typeof(string),typeof(CadViewport),new PropertyMetadata("Selecione uma ferramenta."));
    public string CommandStatus { get => (string)GetValue(CommandStatusProperty); private set => SetValue(CommandStatusProperty,value); }
    private int _textStep;
    private string _textContent = "";
    private double _textHeight;
    private CadText? _textPreview;
    private string TextPrompt => _textStep switch { 0 => "Texto: digite o conteúdo e Enter.", 1 => "Texto: altura em metros (ex.: 0,25) e Enter.", 2 => "Texto: clique no ponto de inserção para concluir. Esc cancela.", _ => "Texto: clique no ponto de inserção para concluir; Esc cancela." };
    private CadModificationSession? _modification;
    private CadTrimExtendSession? _trimExtendSession;
    private string _parameterText = "";
    private readonly ViewportTransform _transform = new();
    private bool _initialViewportCentered;

    // Pan state
    private bool _isPanning;
    private Point _panStartScreen;
    private double _panOriginX;
    private double _panOriginY;

    // Selection box state
    private bool _isBoxSelecting;
    private Point2D _boxSelectStart;
    private Point2D _boxSelectCurrent;

    // Grips state
    private readonly List<GripHandle> _currentGrips = [];
    private bool _isDraggingGrip;
    private GripHandle? _draggingGrip;
    private ICadEntity? _gripOwnerEntity;

    // Drafting state
    private Point2D? _draftStartPoint;
    private Point2D? _arcRadiusPoint;
    private Point2D _draftCurrentPoint;
    private readonly List<Point2D> _polyDraftPoints = [];
    private int _dimensionStep;
    private Point2D _dimP1;
    private Point2D _dimP2;

    // Snapping state
    private PrecisionSnapResult _currentSnap;
    private Point2D _rawMouseWorld;
    private Point2D? _lastSelectClickPoint;
    private List<ICadEntity>? _cycleCandidates;
    private int _cycleCandidateIndex;

    public CadViewport()
    {
        Focusable = true;
        ClipToBounds = true;
        Cursor = Cursors.Cross;

        // The initial pan is set after layout, when the canvas size is known.
        _transform.Zoom = 25.0; // 25 px/meter
    }

    public bool ZoomExtents()
    {
        var bounds = BoundingBox2D.Empty;
        foreach (var entity in VisibleEntities()) bounds = bounds.Expand(entity.BoundingBox);
        if (!_transform.FitBounds(bounds, ActualWidth, ActualHeight)) return false;
        _initialViewportCentered = true;
        ZoomLevel = _transform.Zoom;
        InvalidateVisual();
        return true;
    }

    public void ResetView()
    {
        _transform.Zoom = 25.0;
        _transform.PanX = ActualWidth / 2.0;
        _transform.PanY = ActualHeight / 2.0;
        _initialViewportCentered = ActualWidth > 0 && ActualHeight > 0;
        ZoomLevel = _transform.Zoom;
        InvalidateVisual();
    }

    private static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadViewport vp)
        {
            vp.ClearSelection();
            vp.InvalidateVisual();
        }
    }

    private static void OnLayerRevisionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CadViewport vp || vp.Document == null) return;
        foreach (var entity in vp.Document.Entities)
        {
            if (vp.Document.GetLayer(entity.Layer)?.IsVisible == false) entity.IsSelected = false;
        }
        vp._modification = null;
        vp.UpdateGrips();
        vp.InvalidateVisual();
    }

    private static void OnDrawingRevisionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CadViewport vp) return;
        vp._modification = null;
        vp.UpdateGrips();
        vp.InvalidateVisual();
    }

    public IEnumerable<ICadEntity> SelectableEntities()
    {
        var list = VisibleEntities();
        if (SelectedTypeFilter.HasValue)
            list = list.Where(e => e.EntityType == SelectedTypeFilter.Value);
        if (!string.IsNullOrWhiteSpace(SelectedLayerFilter))
            list = list.Where(e => string.Equals(e.Layer, SelectedLayerFilter, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    private IEnumerable<ICadEntity> VisibleEntities() =>
        Document?.Entities.Where(entity => Document.GetLayer(entity.Layer)?.IsVisible != false)
        ?? Enumerable.Empty<ICadEntity>();

    private static void OnActiveToolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadViewport vp)
        {
            vp.ResetDraftState();
            vp._parameterText = "";
            vp._textStep = 0; vp._textPreview = null;
            vp._modification = vp.ActiveTool is CadToolType.Move or CadToolType.Copy or CadToolType.Rotate or CadToolType.Mirror or CadToolType.Offset
                ? new CadModificationSession(vp.ActiveTool) : null;
            if (vp._modification != null && vp.Document?.Entities.Any(e => e.IsSelected) == true)
                vp._modification.Select(vp.Document.Entities);
            vp._trimExtendSession = vp.ActiveTool is CadToolType.Trim or CadToolType.Extend ? new CadTrimExtendSession(vp.ActiveTool) : null;
            if (vp._trimExtendSession != null && vp.Document?.Entities.Any(e => e.IsSelected) == true) vp._trimExtendSession.InitializePreselected(vp.Document.Entities.Where(e => e.IsSelected));
            vp.UpdateCommandStatus();
            vp.Cursor = vp.ActiveTool == CadToolType.Pan ? Cursors.Hand : Cursors.Cross;
            vp.InvalidateVisual();
        }
    }

    public void ResetDraftState()
    {
        _draftStartPoint = null;
        _arcRadiusPoint = null;
        _polyDraftPoints.Clear();
        _dimensionStep = 0;
        _isBoxSelecting = false;
        _isDraggingGrip = false;
        _draggingGrip = null;
        _cycleCandidates = null;
        _cycleCandidateIndex = 0;
        _lastSelectClickPoint = null;
        _trimExtendSession = null;
    }

    public void ClearSelection()
    {
        if (Document != null)
        {
            foreach (var e in Document.Entities) e.IsSelected = false;
        }
        _currentGrips.Clear();
        SelectedCount = 0;
    }

    public void SelectAll()
    {
        if (Document == null) return;
        int count = 0;
        foreach (var e in Document.Entities)
        {
            var layer = Document.GetLayer(e.Layer);
            if (layer is { IsVisible: true, IsLocked: false })
            {
                if (SelectedTypeFilter.HasValue && e.EntityType != SelectedTypeFilter.Value)
                {
                    e.IsSelected = false;
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(SelectedLayerFilter) && !string.Equals(e.Layer, SelectedLayerFilter, StringComparison.OrdinalIgnoreCase))
                {
                    e.IsSelected = false;
                    continue;
                }
                e.IsSelected = true;
                count++;
            }
            else
            {
                e.IsSelected = false;
            }
        }
        UpdateGrips();
        SelectedCount = count;
        CommandStatus = count > 0 ? $"{count} elemento(s) selecionado(s)." : "Nenhum elemento correspondeu à seleção.";
        InvalidateVisual();
    }

    public void InvertSelection()
    {
        if (Document == null) return;
        int count = 0;
        foreach (var e in Document.Entities)
        {
            var layer = Document.GetLayer(e.Layer);
            if (layer is { IsVisible: true, IsLocked: false })
            {
                if (SelectedTypeFilter.HasValue && e.EntityType != SelectedTypeFilter.Value)
                {
                    e.IsSelected = false;
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(SelectedLayerFilter) && !string.Equals(e.Layer, SelectedLayerFilter, StringComparison.OrdinalIgnoreCase))
                {
                    e.IsSelected = false;
                    continue;
                }
                e.IsSelected = !e.IsSelected;
                if (e.IsSelected) count++;
            }
            else
            {
                e.IsSelected = false;
            }
        }
        UpdateGrips();
        SelectedCount = count;
        CommandStatus = $"Seleção invertida: {count} elemento(s) selecionado(s).";
        InvalidateVisual();
    }

    public void SelectActiveLayer()
    {
        if (Document == null) return;
        var activeLayerName = Document.ActiveLayer;
        var layer = Document.GetLayer(activeLayerName);
        if (layer is not { IsVisible: true, IsLocked: false })
        {
            CommandStatus = $"Camada ativa '{activeLayerName}' está invisível ou bloqueada.";
            return;
        }
        int count = 0;
        foreach (var e in Document.Entities)
        {
            if (string.Equals(e.Layer, activeLayerName, StringComparison.OrdinalIgnoreCase))
            {
                if (SelectedTypeFilter.HasValue && e.EntityType != SelectedTypeFilter.Value)
                {
                    e.IsSelected = false;
                    continue;
                }
                e.IsSelected = true;
                count++;
            }
            else
            {
                e.IsSelected = false;
            }
        }
        UpdateGrips();
        SelectedCount = count;
        CommandStatus = $"{count} elemento(s) da camada '{activeLayerName}' selecionado(s).";
        InvalidateVisual();
    }

    public void UpdateGrips()
    {
        _currentGrips.Clear();
        if (Document == null) return;

        var selected = Document.Entities.Where(e => e.IsSelected).ToList();
        SelectedCount = selected.Count;

        if (selected.Count == 1 && Document.GetLayer(selected[0].Layer)?.IsLocked == false)
        {
            _gripOwnerEntity = selected[0];
            _currentGrips.AddRange(CadGripEngine.GetGripsForEntity(_gripOwnerEntity));
        }
        else
        {
            _gripOwnerEntity = null;
        }
    }

    #region Rendering

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (!_initialViewportCentered && ActualWidth > 0 && ActualHeight > 0)
        {
            _transform.PanX = ActualWidth / 2.0;
            _transform.PanY = ActualHeight / 2.0;
            _initialViewportCentered = true;
        }

        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(15, 23, 42)), null, bounds);

        DrawGrid(dc);
        DrawAxes(dc);

        if (Document != null)
        {
            DrawEntities(dc);
            if (Document.StagedProposal is { } staged)
            {
                DrawStagedProposalPreview(dc, staged);
            }
        }

        if (_modification != null)
        {
            dc.PushOpacity(0.8);
            DrawEntities(dc, _modification.Preview);
            dc.Pop();
            DrawModificationGuides(dc,_modification);
            var message = _modification.Prompt + "\n" + _parameterText + "\n" + _modification.Error;
            var text = new FormattedText(message, CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 14, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawRectangle(Brushes.Black, null, new Rect(8, 8, Math.Min(ActualWidth, text.Width + 16), text.Height + 12));
            dc.DrawText(text, new Point(16, 14));
        }
        if (ActiveTool == CadToolType.Text && _textPreview != null)
        {
            dc.PushOpacity(.6); DrawEntities(dc, [_textPreview]); dc.Pop();
        }
        if (_trimExtendSession != null)
        {
            DrawTrimExtendGuides(dc, _trimExtendSession);
            var message = _trimExtendSession.Prompt + (!string.IsNullOrEmpty(_trimExtendSession.Feedback) ? "\n" + _trimExtendSession.Feedback : "");
            var text = new FormattedText(message, CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 14, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(200, 15, 23, 42)), null, new Rect(8, 8, Math.Min(ActualWidth, text.Width + 16), text.Height + 12));
            dc.DrawText(text, new Point(16, 14));
        }
        UpdateCommandStatus();
        DrawActiveDraft(dc);
        DrawSelectionBox(dc);
        DrawGrips(dc);
        DrawSnapGuides(dc);
    }

    public void HandleTrimExtendClick(Point2D worldPt)
    {
        if (_trimExtendSession == null) return;
        var hitTol = CadSnapEngine.ConvertScreenPixelsToMeters(Document?.Preferences.SelectionHitSizePixels ?? 10, _transform.Zoom);
        if (_trimExtendSession.Step == 0)
        {
            var hit = CadSelectionEngine.SelectByPoint(VisibleEntities(), worldPt, hitTol);
            if (hit != null)
            {
                _trimExtendSession.ToggleBoundary(hit);
                UpdateCommandStatus();
                InvalidateVisual();
            }
        }
        else
        {
            var res = _trimExtendSession.ExecuteClick(worldPt, VisibleEntities(), hitTol, Document!, UndoManager!);
            if (res.DrawingChanged)
            {
                CommandManager.InvalidateRequerySuggested();
            }
            UpdateCommandStatus();
            InvalidateVisual();
        }
    }

    public void HandleTrimExtendHover(Point2D worldPt)
    {
        if (_trimExtendSession == null || Document == null) return;
        var prefs = Document.Preferences;
        var hitTol = CadSnapEngine.ConvertScreenPixelsToMeters(prefs.SelectionHitSizePixels, _transform.Zoom);
        _trimExtendSession.Hover(worldPt, VisibleEntities(), hitTol, Document);
        UpdateCommandStatus();
    }

    internal void UpdateCommandStatus()
    {
        CommandStatus = (_trimExtendSession != null ? _trimExtendSession.Prompt + (!string.IsNullOrEmpty(_trimExtendSession.Feedback) ? " | " + _trimExtendSession.Feedback : "") : ActiveTool == CadToolType.Text ? TextPrompt + " " + _parameterText : _modification?.Prompt ??
            (ActiveTool == CadToolType.Select ? "Clique para selecionar; Shift adiciona; arraste uma janela de seleção." : "Clique no viewport para definir os pontos; Esc cancela."))
            + " | Captura: " + (Document?.Preferences.IsOsnapEnabled == true ? "objetos" : "livre")
            + (Document?.Preferences.IsOrthoModeEnabled == true ? " / ortogonal" : "")
            + (Document?.Preferences.IsPolarTrackingEnabled == true ? " / polar" : "");
    }

    private void DrawGrid(DrawingContext dc)
    {
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)), 1.0);
        gridPen.Freeze();

        double meterStep = _transform.Zoom >= 40 ? 0.5 : (_transform.Zoom >= 15 ? 1.0 : (_transform.Zoom >= 4 ? 5.0 : 20.0));
        double pixelStep = meterStep * _transform.Zoom;
        if (pixelStep < 10) return;

        var origin = ToWpfPoint(Point2D.Zero);

        double startX = origin.X % pixelStep;
        if (startX > 0) startX -= pixelStep;
        for (double x = startX; x < ActualWidth; x += pixelStep)
        {
            dc.DrawLine(gridPen, new Point(x, 0), new Point(x, ActualHeight));
        }

        double startY = origin.Y % pixelStep;
        if (startY > 0) startY -= pixelStep;
        for (double y = startY; y < ActualHeight; y += pixelStep)
        {
            dc.DrawLine(gridPen, new Point(0, y), new Point(ActualWidth, y));
        }
    }

    private void DrawAxes(DrawingContext dc)
    {
        var origin = ToWpfPoint(Point2D.Zero);

        // X Axis (Muted Red)
        var xPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 239, 68, 68)), 1.2);
        xPen.Freeze();
        dc.DrawLine(xPen, new Point(0, origin.Y), new Point(ActualWidth, origin.Y));

        // Y Axis (Muted Green - note CAD Y grows upwards, so positive Y is up)
        var yPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 34, 197, 94)), 1.2);
        yPen.Freeze();
        dc.DrawLine(yPen, new Point(origin.X, 0), new Point(origin.X, ActualHeight));
    }

    private void DrawEntities(DrawingContext dc, IEnumerable<ICadEntity>? entities = null)
    {
        var doc = Document!;
        var visible=(entities ?? doc.Entities).Where(e=>doc.GetLayer(e.Layer)?.IsVisible!=false).ToArray();
        foreach(var entity in visible)
        {
            var color=entities==null?ColorFromHex(entity.ColorHex):Color.FromRgb(226,232,240);
            var pen=new Pen(new SolidColorBrush(color),Math.Max(entities==null?1.0:2.0,entity.LineThickness));
            if(entities!=null)pen.DashStyle=DashStyles.Dash;
            pen.Freeze();
            DrawEntity(dc,entity,pen,color);
        }
        // Render selection last so overlapping unselected objects cannot cover its outline.
        // This is viewport styling only; persisted entity colors remain unchanged.
        if(entities!=null)return;
        foreach(var entity in visible.Where(e=>e.IsSelected))
        {
            var color=CadSelectionAppearance.ColorFor(ColorFromHex(entity.ColorHex));
            var width=Math.Max(1.0,entity.LineThickness);
            DrawEntity(dc,entity,new Pen(Brushes.Black,width+5),Colors.Black,outlineText:true);
            DrawEntity(dc,entity,new Pen(new SolidColorBrush(color),width+2),color,outlineText:true);
        }
    }

    private void DrawEntity(DrawingContext dc,ICadEntity entity,Pen pen,Color color,bool outlineText=false)
    {
            if (entity is CadLine line)
            {
                var s1 = ToWpfPoint(line.StartPoint);
                var s2 = ToWpfPoint(line.EndPoint);
                dc.DrawLine(pen, s1, s2);
            }
            else if (entity is CadCircle circle)
            {
                var sc = ToWpfPoint(circle.Center);
                var rPx = circle.Radius * _transform.Zoom;
                dc.DrawEllipse(null, pen, sc, rPx, rPx);
            }
            else if (entity is CadArc arc)
            {
                var sc = ToWpfPoint(arc.Center);
                var rPx = arc.Radius * _transform.Zoom;
                var sp = ToWpfPoint(arc.StartPoint);
                var ep = ToWpfPoint(arc.EndPoint);

                var geom = new StreamGeometry();
                using (var ctx = geom.Open())
                {
                    ctx.BeginFigure(sp, false, false);
                    ctx.ArcTo(ep, new Size(rPx, rPx), 0, arc.SweepAngleDegrees > 180, SweepDirection.Counterclockwise, true, false);
                }
                geom.Freeze();
                dc.DrawGeometry(null, pen, geom);
            }
            else if (entity is CadRectangle rect)
            {
                var v = rect.GetVertices();
                DrawPolygonLines(dc, pen, v, isClosed: true);
            }
            else if (entity is CadPolyline poly)
            {
                DrawPolygonLines(dc, pen, poly.Vertices, poly.IsClosed);
            }
            else if (entity is CadDimension dim)
            {
                var (d1World, d2World) = dim.GetDimensionLinePoints();
                var sp = ToWpfPoint(dim.StartPoint);
                var ep = ToWpfPoint(dim.EndPoint);
                var d1 = ToWpfPoint(d1World);
                var d2 = ToWpfPoint(d2World);

                DrawDimensionGeometry(dc, pen, color, sp, ep, d1, d2, dim.FormattedText);
            }
            else if (entity is CadText label)
            {
                var anchor = ToWpfPoint(label.Position);
                var formatted = new FormattedText(label.Text, CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface("Segoe UI"),
                    label.Height * _transform.Zoom, new SolidColorBrush(color),
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.PushTransform(new RotateTransform(-label.RotationDegrees, anchor.X, anchor.Y));
                var position=new Point(anchor.X,anchor.Y-formatted.Height);
                if(outlineText)dc.DrawGeometry(new SolidColorBrush(color),color==Colors.Black?new Pen(Brushes.Black,2.5):null,formatted.BuildGeometry(position));
                else dc.DrawText(formatted,position);
                dc.Pop();
            }
    }

    private void DrawDimensionGeometry(DrawingContext dc, Pen pen, Color color, Point p1, Point p2, Point d1, Point d2, string text, bool isDraft = false)
    {
        // 1. Extension lines from measured points to the dimension line
        var extPen = new Pen(new SolidColorBrush(Color.FromArgb(isDraft ? (byte)180 : (byte)160, color.R, color.G, color.B)), isDraft ? 1.0 : Math.Max(1.0, pen.Thickness * 0.8));
        if (isDraft) extPen.DashStyle = DashStyles.Dash;
        extPen.Freeze();

        var ext1Dir = new Vector(d1.X - p1.X, d1.Y - p1.Y);
        var ext1Len = ext1Dir.Length;
        Point d1Ext = d1;
        if (ext1Len > 1e-4)
        {
            var u1 = ext1Dir / ext1Len;
            d1Ext = new Point(d1.X + (u1.X * 4.0), d1.Y + (u1.Y * 4.0));
        }

        var ext2Dir = new Vector(d2.X - p2.X, d2.Y - p2.Y);
        var ext2Len = ext2Dir.Length;
        Point d2Ext = d2;
        if (ext2Len > 1e-4)
        {
            var u2 = ext2Dir / ext2Len;
            d2Ext = new Point(d2.X + (u2.X * 4.0), d2.Y + (u2.Y * 4.0));
        }

        dc.DrawLine(extPen, p1, d1Ext);
        dc.DrawLine(extPen, p2, d2Ext);

        // 2. Main dimension line between d1 and d2
        dc.DrawLine(pen, d1, d2);

        // 3. Architectural ticks at d1 and d2 (45-degree slash)
        var tickPen = new Pen(new SolidColorBrush(color), Math.Max(1.2, pen.Thickness * 1.2));
        tickPen.Freeze();
        const double tSize = 4.0;
        dc.DrawLine(tickPen, new Point(d1.X - tSize, d1.Y + tSize), new Point(d1.X + tSize, d1.Y - tSize));
        dc.DrawLine(tickPen, new Point(d2.X - tSize, d2.Y + tSize), new Point(d2.X + tSize, d2.Y - tSize));

        // 4. Dimension text centered on the dimension line
        var mid = new Point((d1.X + d2.X) / 2.0, (d1.Y + d2.Y) / 2.0);
        var dx = d2.X - d1.X;
        var dy = d2.Y - d1.Y;
        var angleDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        if (angleDeg > 90.0) angleDeg -= 180.0;
        else if (angleDeg < -90.0) angleDeg += 180.0;

        var ft = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            11.0,
            new SolidColorBrush(color),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        dc.PushTransform(new RotateTransform(angleDeg, mid.X, mid.Y));

        // Dark background pill behind text so it's always readable
        var bgRect = new Rect(mid.X - (ft.Width / 2.0) - 3.0, mid.Y - ft.Height - 3.0, ft.Width + 6.0, ft.Height + 2.0);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(210, 15, 23, 42)), null, bgRect, 2.0, 2.0);

        dc.DrawText(ft, new Point(mid.X - (ft.Width / 2.0), mid.Y - ft.Height - 2.0));
        dc.Pop();
    }

    private void DrawPolygonLines(DrawingContext dc, Pen pen, IReadOnlyList<Point2D> vertices, bool isClosed)
    {
        if (vertices.Count < 2) return;
        for (int i = 0; i < vertices.Count - 1; i++)
        {
            dc.DrawLine(pen, ToWpfPoint(vertices[i]), ToWpfPoint(vertices[i + 1]));
        }
        if (isClosed && vertices.Count >= 3)
        {
            dc.DrawLine(pen, ToWpfPoint(vertices[^1]), ToWpfPoint(vertices[0]));
        }
    }

    private void DrawModificationGuides(DrawingContext dc,CadModificationSession session)
    {
        var guide=new Pen(Brushes.Gold,1.2){DashStyle=DashStyles.Dash};
        var solid=new Pen(Brushes.Gold,1.5);
        void Mark(Point p){dc.DrawEllipse(Brushes.Black,solid,p,4,4);dc.DrawLine(solid,new(p.X-8,p.Y),new(p.X+8,p.Y));dc.DrawLine(solid,new(p.X,p.Y-8),new(p.X,p.Y+8));}
        void Label(Point p,string value)
        {
            var text=new FormattedText(value,CultureInfo.GetCultureInfo("pt-BR"),FlowDirection.LeftToRight,new Typeface("Segoe UI"),12,Brushes.White,VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(230,15,23,42)),null,new Rect(p.X+10,p.Y+10,text.Width+10,text.Height+6),3,3);
            dc.DrawText(text,new Point(p.X+15,p.Y+13));
        }
        string Length(double meters)=>CadUnitConverter.FormatLength(meters,Document!.Preferences.DisplayUnit,Document.Preferences.DecimalPrecision,true,CultureInfo.GetCultureInfo("pt-BR"));
        void Arrow(Point a,Point b)
        {
            dc.DrawLine(guide,a,b);var v=b-a;if(v.Length<1)return;v.Normalize();var n=new Vector(-v.Y,v.X);
            dc.DrawLine(solid,b,b-v*9+n*4);dc.DrawLine(solid,b,b-v*9-n*4);
        }
        var target=ToWpfPoint(session.Target);
        if(session.Tool==CadToolType.Offset && session.Step is 1 or 2)
        {
            for(int i=0;i<session.Preview.Count;i++)
            {
                if(session.Sources[i] is CadLine line && session.Preview[i] is CadLine offset)
                {
                    var dx=line.EndPoint.X-line.StartPoint.X;var dy=line.EndPoint.Y-line.StartPoint.Y;
                    var t=Math.Clamp(((session.Target.X-line.StartPoint.X)*dx+(session.Target.Y-line.StartPoint.Y)*dy)/(dx*dx+dy*dy),0,1);
                    var a=new Point2D(line.StartPoint.X+t*dx,line.StartPoint.Y+t*dy);
                    var b=new Point2D(a.X+offset.StartPoint.X-line.StartPoint.X,a.Y+offset.StartPoint.Y-line.StartPoint.Y);
                    Arrow(ToWpfPoint(a),ToWpfPoint(b));Label(ToWpfPoint(b),Length(session.Distance));
                }
                else if(session.Sources[i] is CadCircle circle && session.Preview[i] is CadCircle offsetCircle)
                {
                    var dx=session.Target.X-circle.Center.X;var dy=session.Target.Y-circle.Center.Y;var len=Math.Sqrt(dx*dx+dy*dy);
                    if(len<1e-9){dx=1;dy=0;len=1;}
                    var a=new Point2D(circle.Center.X+dx/len*circle.Radius,circle.Center.Y+dy/len*circle.Radius);
                    var b=new Point2D(circle.Center.X+dx/len*offsetCircle.Radius,circle.Center.Y+dy/len*offsetCircle.Radius);
                    Arrow(ToWpfPoint(a),ToWpfPoint(b));Label(ToWpfPoint(b),Length(session.Distance));
                }
            }
            return;
        }
        if(session.Origin is not {} origin)return;
        var pivot=ToWpfPoint(origin);Mark(pivot);
        if(session.Tool is CadToolType.Move or CadToolType.Copy)
        {
            Arrow(pivot,target);Mark(target);
            Label(target,$"ΔX {Length(session.Target.X-origin.X)} · ΔY {Length(session.Target.Y-origin.Y)} · {Length(origin.DistanceTo(session.Target))}");
        }
        else if(session.Tool==CadToolType.Rotate)
        {
            const double radius=55;
            var reference=session.ReferenceDegrees*Math.PI/180;
            var angle=(session.Step==5 ? Math.Atan2(session.Target.Y-origin.Y,session.Target.X-origin.X)*180/Math.PI-session.ReferenceDegrees:session.AngleDegrees)%360;
            var end=reference+angle*Math.PI/180;
            Point Ray(double radians,double length)=>new(pivot.X+length*Math.Cos(radians),pivot.Y-length*Math.Sin(radians));
            dc.DrawLine(guide,pivot,Ray(reference,90));dc.DrawLine(guide,pivot,Ray(end,Math.Max(90,(target-pivot).Length)));
            if(Math.Abs(angle)>1e-5)
            {
                var arc=new StreamGeometry();using(var ctx=arc.Open())
                {ctx.BeginFigure(Ray(reference,radius),false,false);ctx.ArcTo(Ray(end,radius),new Size(radius,radius),0,Math.Abs(angle)>180,angle>0?SweepDirection.Counterclockwise:SweepDirection.Clockwise,true,false);}
                dc.DrawGeometry(null,solid,arc);
                var tip=Ray(end,radius);var tangent=new Vector(-Math.Sin(end),-Math.Cos(end))*(angle>0?1:-1);var n=new Vector(-tangent.Y,tangent.X);
                dc.DrawLine(solid,tip,tip-tangent*8+n*3);dc.DrawLine(solid,tip,tip-tangent*8-n*3);
            }
            Label(pivot,$"{angle.ToString("0.##",CultureInfo.GetCultureInfo("pt-BR"))}° · pivô comum");
        }
        else if(session.Tool==CadToolType.Mirror)
        {
            var v=target-pivot;if(v.Length<1)return;v.Normalize();var extent=Math.Max(ActualWidth,ActualHeight)*2;
            dc.DrawLine(guide,pivot-v*extent,pivot+v*extent);Mark(target);Label(target,"Eixo de espelhamento");
        }
        else if(session.Tool==CadToolType.Offset && session.Step==6)
        {Arrow(pivot,target);Label(target,Length(session.Distance));}
    }

    private void DrawTrimExtendGuides(DrawingContext dc, CadTrimExtendSession session)
    {
        var boundaryPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 245, 158, 11)), 2.0)
        {
            DashStyle = DashStyles.Dash
        };
        boundaryPen.Freeze();

        foreach (var b in session.Boundaries)
        {
            DrawEntity(dc, b, boundaryPen, Color.FromRgb(245, 158, 11));
        }

        if (session.Tool == CadToolType.Trim && session.HoverTrimResult is { } trimRes)
        {
            var discardPen = new Pen(new SolidColorBrush(Color.FromArgb(240, 239, 68, 68)), Math.Max(2.5, trimRes.DiscardedSegment.LineThickness + 2.0))
            {
                DashStyle = DashStyles.Dash
            };
            discardPen.Freeze();
            DrawEntity(dc, trimRes.DiscardedSegment, discardPen, Color.FromRgb(239, 68, 68));

            var pt = ToWpfPoint(trimRes.IntersectionPoint);
            const double s = 6.0;
            dc.DrawLine(discardPen, new Point(pt.X - s, pt.Y - s), new Point(pt.X + s, pt.Y + s));
            dc.DrawLine(discardPen, new Point(pt.X - s, pt.Y + s), new Point(pt.X + s, pt.Y - s));
        }

        if (session.Tool == CadToolType.Extend && session.HoverExtendResult is { } extRes)
        {
            var extPen = new Pen(new SolidColorBrush(Color.FromArgb(240, 34, 197, 94)), Math.Max(2.5, extRes.ExtendedEntity.LineThickness + 2.0))
            {
                DashStyle = DashStyles.Dash
            };
            extPen.Freeze();
            DrawEntity(dc, extRes.ExtendedEntity, extPen, Color.FromRgb(34, 197, 94));

            var pt = ToWpfPoint(extRes.ExtensionPoint);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(180, 34, 197, 94)), extPen, pt, 4.0, 4.0);
        }
    }

    private void DrawStagedProposalPreview(DrawingContext dc, CadStagedProposal staged)
    {
        var doc = Document;
        if (doc == null) return;

        var addModColor = Color.FromRgb(16, 185, 129);
        var addModPen = new Pen(new SolidColorBrush(Color.FromArgb(230, 16, 185, 129)), 2.2)
        {
            DashStyle = DashStyles.Dash
        };
        addModPen.Freeze();

        var removeColor = Color.FromRgb(239, 68, 68);
        var removePen = new Pen(new SolidColorBrush(Color.FromArgb(230, 239, 68, 68)), 2.2)
        {
            DashStyle = DashStyles.Dash
        };
        removePen.Freeze();

        dc.PushOpacity(0.85);
        foreach (var entity in staged.AddedEntities.Concat(staged.ModifiedEntities))
        {
            DrawEntity(dc, entity, addModPen, addModColor);
        }

        if (staged.RemovedEntityIds.Count > 0)
        {
            var removedSet = staged.RemovedEntityIds.ToHashSet();
            foreach (var entity in doc.Entities.Where(e => removedSet.Contains(e.Id)))
            {
                DrawEntity(dc, entity, removePen, removeColor);
            }
        }
        dc.Pop();

        var bannerMsg = $"[PREVIEW IA] {staged.Summary} (+{staged.AddedEntities.Count} / ~{staged.ModifiedEntities.Count} / -{staged.RemovedEntityIds.Count}) — Digite COMMIT para aplicar ou DISCARD para descartar";
        var bannerText = new FormattedText(
            bannerMsg,
            CultureInfo.GetCultureInfo("pt-BR"),
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            13,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        var rectWidth = Math.Min(ActualWidth - 16, bannerText.Width + 20);
        dc.DrawRoundedRectangle(
            new SolidColorBrush(Color.FromArgb(225, 6, 78, 59)),
            new Pen(new SolidColorBrush(addModColor), 1.2),
            new Rect(8, 8, Math.Max(40, rectWidth), bannerText.Height + 10),
            4, 4);
        dc.DrawText(bannerText, new Point(18, 13));
    }

    private void DrawActiveDraft(DrawingContext dc)
    {
        var draftPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 245, 158, 11)), 1.5)
        {
            DashStyle = DashStyles.Dash
        };
        draftPen.Freeze();

        if (ActiveTool == CadToolType.Dimension)
        {
            var dimColor = Color.FromRgb(245, 158, 11);
            if (_dimensionStep == 1)
            {
                var p1 = ToWpfPoint(_dimP1);
                var p2 = ToWpfPoint(_draftCurrentPoint);

                dc.DrawLine(draftPen, p1, p2);

                const double mSize = 4.0;
                dc.DrawLine(draftPen, new Point(p1.X - mSize, p1.Y - mSize), new Point(p1.X + mSize, p1.Y + mSize));
                dc.DrawLine(draftPen, new Point(p1.X - mSize, p1.Y + mSize), new Point(p1.X + mSize, p1.Y - mSize));

                var dist = _dimP1.DistanceTo(_draftCurrentPoint);
                var ft = new FormattedText(
                    $"{dist:F2} m",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    11.0,
                    new SolidColorBrush(dimColor),
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                var mid = new Point((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0);
                var bgRect = new Rect(mid.X - (ft.Width / 2.0) - 3.0, mid.Y - ft.Height - 3.0, ft.Width + 6.0, ft.Height + 2.0);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(210, 15, 23, 42)), null, bgRect, 2.0, 2.0);
                dc.DrawText(ft, new Point(mid.X - (ft.Width / 2.0), mid.Y - ft.Height - 2.0));
            }
            else if (_dimensionStep == 2)
            {
                var (d1World, d2World) = CadDimension.CalculateDimensionLinePoints(_dimP1, _dimP2, _draftCurrentPoint);
                var p1 = ToWpfPoint(_dimP1);
                var p2 = ToWpfPoint(_dimP2);
                var d1 = ToWpfPoint(d1World);
                var d2 = ToWpfPoint(d2World);

                var dist = _dimP1.DistanceTo(_dimP2);
                var text = $"{dist:F2} m";

                DrawDimensionGeometry(dc, draftPen, dimColor, p1, p2, d1, d2, text, isDraft: true);

                var curPt = ToWpfPoint(_draftCurrentPoint);
                var dimMid = new Point((d1.X + d2.X) / 2.0, (d1.Y + d2.Y) / 2.0);
                var guidePen = new Pen(new SolidColorBrush(Color.FromArgb(120, 245, 158, 11)), 1.0) { DashStyle = DashStyles.Dot };
                guidePen.Freeze();
                dc.DrawLine(guidePen, curPt, dimMid);
            }
        }
        else if (_draftStartPoint != null)
        {
            var p1 = _draftStartPoint.Value;
            var p2 = _draftCurrentPoint;

            if (ActiveTool == CadToolType.Line)
            {
                dc.DrawLine(draftPen, ToWpfPoint(p1), ToWpfPoint(p2));
            }
            else if (ActiveTool == CadToolType.Rectangle)
            {
                var minX = Math.Min(p1.X, p2.X);
                var minY = Math.Min(p1.Y, p2.Y);
                var w = Math.Abs(p2.X - p1.X);
                var h = Math.Abs(p2.Y - p1.Y);
                var rectCorners = PolygonMath.CreateRectangle(new Point2D(minX, minY), w, h);
                DrawPolygonLines(dc, draftPen, rectCorners, isClosed: true);
            }
            else if (ActiveTool == CadToolType.Circle)
            {
                var r = p1.DistanceTo(p2);
                var rPx = r * _transform.Zoom;
                dc.DrawEllipse(null, draftPen, ToWpfPoint(p1), rPx, rPx);
            }
            else if (ActiveTool == CadToolType.Arc)
            {
                if (_arcRadiusPoint == null)
                {
                    var r = p1.DistanceTo(p2);
                    var rPx = r * _transform.Zoom;
                    dc.DrawEllipse(null, draftPen, ToWpfPoint(p1), rPx, rPx);
                    dc.DrawLine(draftPen, ToWpfPoint(p1), ToWpfPoint(p2));
                }
                else
                {
                    dc.DrawLine(draftPen, ToWpfPoint(p1), ToWpfPoint(_arcRadiusPoint.Value));
                    dc.DrawLine(draftPen, ToWpfPoint(p1), ToWpfPoint(p2));
                }
            }
        }
        else if (_polyDraftPoints.Count > 0)
        {
            DrawPolygonLines(dc, draftPen, _polyDraftPoints, isClosed: false);
            dc.DrawLine(draftPen, ToWpfPoint(_polyDraftPoints[^1]), ToWpfPoint(_draftCurrentPoint));
        }
    }

    private void DrawSelectionBox(DrawingContext dc)
    {
        if (!_isBoxSelecting) return;

        var s1 = ToWpfPoint(_boxSelectStart);
        var s2 = ToWpfPoint(_boxSelectCurrent);

        var rect = new Rect(
            Math.Min(s1.X, s2.X),
            Math.Min(s1.Y, s2.Y),
            Math.Abs(s2.X - s1.X),
            Math.Abs(s2.Y - s1.Y));

        bool isWindow = _boxSelectCurrent.X >= _boxSelectStart.X;

        // Window = Blue Solid, Crossing = Green Dashed
        var fillColor = isWindow ? Color.FromArgb(40, 56, 189, 248) : Color.FromArgb(40, 34, 197, 94);
        var borderColor = isWindow ? Color.FromRgb(56, 189, 248) : Color.FromRgb(34, 197, 94);

        var borderPen = new Pen(new SolidColorBrush(borderColor), 1.2);
        if (!isWindow) borderPen.DashStyle = DashStyles.Dash;
        borderPen.Freeze();

        dc.DrawRectangle(new SolidColorBrush(fillColor), borderPen, rect);
    }

    private void DrawGrips(DrawingContext dc)
    {
        var gripColor=CadSelectionAppearance.ColorFor(ColorFromHex(_gripOwnerEntity?.ColorHex));
        var gripBrush = new SolidColorBrush(gripColor);
        var borderPen = new Pen(Brushes.Black, 2.0);
        borderPen.Freeze();

        const double size = 7.0;

        foreach (var grip in _currentGrips)
        {
            var sp = ToWpfPoint(grip.Position);
            var rect = new Rect(sp.X - (size / 2.0), sp.Y - (size / 2.0), size, size);
            dc.DrawRectangle(gripBrush, borderPen, rect);
        }
    }

    private void DrawSnapGuides(DrawingContext dc)
    {
        // 1. Polar guide line
        if (_currentSnap.TrackingSnap?.IsPolarSnapped == true || _currentSnap.TrackingSnap?.IsOrthoSnapped == true)
        {
            if (_draftStartPoint != null)
            {
                var s1 = ToWpfPoint(_draftStartPoint.Value);
                var s2 = ToWpfPoint(_currentSnap.Point);
                var polarPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 6, 182, 212)), 1.0)
                {
                    DashStyle = DashStyles.Dot
                };
                polarPen.Freeze();
                dc.DrawLine(polarPen, s1, s2);
            }
        }

        // 2. Osnap glyph
        if (_currentSnap.ObjectSnap != null)
        {
            var sp = ToWpfPoint(_currentSnap.ObjectSnap.Point);
            var glyphColor = Color.FromRgb(234, 179, 8); // Gold Osnap Glyph
            var glyphPen = new Pen(new SolidColorBrush(glyphColor), 1.5);
            glyphPen.Freeze();

            double gSize = 8.0;

            switch (_currentSnap.ObjectSnap.Type)
            {
                case OsnapType.Endpoint:
                    dc.DrawRectangle(null, glyphPen, new Rect(sp.X - gSize, sp.Y - gSize, gSize * 2, gSize * 2));
                    break;
                case OsnapType.Midpoint:
                    var triangle = new StreamGeometry();
                    using (var ctx = triangle.Open())
                    {
                        ctx.BeginFigure(new Point(sp.X, sp.Y - gSize), true, true);
                        ctx.LineTo(new Point(sp.X - gSize, sp.Y + gSize), true, false);
                        ctx.LineTo(new Point(sp.X + gSize, sp.Y + gSize), true, false);
                    }
                    triangle.Freeze();
                    dc.DrawGeometry(null, glyphPen, triangle);
                    break;
                case OsnapType.Center:
                    dc.DrawEllipse(null, glyphPen, sp, gSize, gSize);
                    break;
                default:
                    dc.DrawLine(glyphPen, new Point(sp.X - gSize, sp.Y - gSize), new Point(sp.X + gSize, sp.Y + gSize));
                    dc.DrawLine(glyphPen, new Point(sp.X - gSize, sp.Y + gSize), new Point(sp.X + gSize, sp.Y - gSize));
                    break;
            }
        }
    }

    #endregion

    #region Coordinate & Color Helpers

    private Point ToWpfPoint(Point2D worldPoint)
    {
        // CAD coordinates: Y grows upwards. Screen coordinates: Y grows downwards.
        var scr = _transform.WorldToScreen(worldPoint);
        return new Point(scr.X, ActualHeight - scr.Y);
    }

    private Point2D ToWorldPoint(Point screenPoint)
    {
        var invertedScreenY = ActualHeight - screenPoint.Y;
        return _transform.ScreenToWorld(new ScreenPoint(screenPoint.X, invertedScreenY));
    }

    private static Color ColorFromHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Colors.White;
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Colors.White;
        }
    }

    #endregion

    #region Mouse & Keyboard Interaction

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        var screenPt = e.GetPosition(this);
        var worldPt = ToWorldPoint(screenPt);

        if (e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Left && ActiveTool == CadToolType.Pan))
        {
            _isPanning = true;
            _panStartScreen = screenPt;
            _panOriginX = _transform.PanX;
            _panOriginY = _transform.PanY;
            CaptureMouse();
            Cursor = Cursors.SizeAll;
            e.Handled = true;
            return;
        }

        if (e.ChangedButton == MouseButton.Right)
        {
            if (_trimExtendSession != null)
            {
                if (_trimExtendSession.Step == 0)
                {
                    _trimExtendSession.ConfirmBoundaries(VisibleEntities());
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
                else
                {
                    _trimExtendSession = null;
                    ActiveTool = CadToolType.Select;
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
            }
            if(_modification?.Step == 0 && Document!=null)
            {
                _modification.Select(Document.Entities);InvalidateVisual();e.Handled=true;return;
            }
            if (_polyDraftPoints.Count >= 2 && ActiveTool == CadToolType.Polyline)
            {
                CommitPolyline(isClosed: false);
            }
            else
            {
                ResetDraftState();
                ActiveTool = CadToolType.Select;
            }
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton == MouseButton.Left)
        {
            if (_trimExtendSession != null)
            {
                HandleTrimExtendClick(worldPt);
                if (e.RoutedEvent != null) e.Handled = true;
                return;
            }
            var precisionPt = CadSnapEngine.ResolvePrecisionPoint(worldPt, VisibleEntities(),
                Document?.Preferences ?? new CadPreferences(),
                CadSnapEngine.ConvertScreenPixelsToMeters(Document?.Preferences.SnapHitSizePixels ?? 10, _transform.Zoom),
                _modification != null ? _modification.PrecisionOrigin : _draftStartPoint).Point;
            if(_modification is {Tool:CadToolType.Offset,Step:2})precisionPt=worldPt;
            if (ActiveTool == CadToolType.Text)
            {
                if (_textStep == 2) { UpdateTextPreview(precisionPt); CommitText(); }
                InvalidateVisual();
                if (e.RoutedEvent != null) e.Handled = true;
                return;
            }
            if (_modification != null)
            {
                if (_modification.Step == 0)
                {
                    var hit = CadSelectionEngine.SelectByPoint(VisibleEntities(), worldPt,
                        CadSnapEngine.ConvertScreenPixelsToMeters(Document!.Preferences.SelectionHitSizePixels, _transform.Zoom));
                    if (hit != null) hit.IsSelected = !hit.IsSelected;
                    UpdateGrips();
                }
                else if(_modification is {Tool:CadToolType.Offset,Step:1} && !string.IsNullOrWhiteSpace(_parameterText))
                {
                    // A side click submits the typed distance and applies the offset in one gesture.
                    // Invalid text stays available for correction and never starts a measurement silently.
                    if(_modification.Input(_parameterText)) { _parameterText="";AcceptModificationPoint(worldPt); }
                }
                else AcceptModificationPoint(precisionPt);
                InvalidateVisual();
                if (e.RoutedEvent != null) e.Handled = true;
                return;
            }

            if (ActiveTool == CadToolType.Select)
            {
                HandleSelectMouseDown(worldPt);
            }
            else
            {
                HandleToolMouseDown(precisionPt);
            }

            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var screenPt = e.GetPosition(this);
        _rawMouseWorld = ToWorldPoint(screenPt);

        MouseWorldX = _rawMouseWorld.X;
        MouseWorldY = _rawMouseWorld.Y;

        if (_isPanning)
        {
            var dx = screenPt.X - _panStartScreen.X;
            var dy = _panStartScreen.Y - screenPt.Y;
            _transform.PanX = _panOriginX + dx;
            _transform.PanY = _panOriginY + dy;
            InvalidateVisual();
            return;
        }

        var prefs = Document?.Preferences ?? new CadPreferences();
        Point2D? origin = _modification != null ? _modification.PrecisionOrigin : _draftStartPoint;
        if (origin == null && _polyDraftPoints.Count > 0)
        {
            origin = _polyDraftPoints[^1];
        }
        else if (origin == null && ActiveTool == CadToolType.Dimension)
        {
            if (_dimensionStep == 1) origin = _dimP1;
            else if (_dimensionStep == 2) origin = _dimP2;
        }
        var snapTol = CadSnapEngine.ConvertScreenPixelsToMeters(prefs.SnapHitSizePixels, _transform.Zoom);

        _currentSnap = CadSnapEngine.ResolvePrecisionPoint(
            _rawMouseWorld,
            VisibleEntities(),
            prefs,
            snapTol,
            origin);

        if(_modification is {Tool:CadToolType.Offset} offsetSession && (offsetSession.Step==2 || offsetSession.Step==1 && !string.IsNullOrWhiteSpace(_parameterText)))_currentSnap=new PrecisionSnapResult(_rawMouseWorld,null,null,false);
        _draftCurrentPoint = _currentSnap.Point;
        _modification?.Hover(_currentSnap.Point,_parameterText);
        if (ActiveTool == CadToolType.Text && _textStep == 2) UpdateTextPreview(_currentSnap.Point);
        if (_trimExtendSession != null && Document != null) { HandleTrimExtendHover(_rawMouseWorld); }

        if (_isBoxSelecting)
        {
            _boxSelectCurrent = _rawMouseWorld;
            InvalidateVisual();
            return;
        }

        if (_isDraggingGrip && _draggingGrip != null && _gripOwnerEntity != null)
        {
            if (Document?.GetLayer(_gripOwnerEntity.Layer)?.IsLocked != false) { ResetDraftState(); ReleaseMouseCapture(); return; }
            CadGripEngine.ApplyGripMove(_gripOwnerEntity, _draggingGrip, _currentSnap.Point);
            InvalidateVisual();
            return;
        }

        InvalidateVisual();
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if ((e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Left && ActiveTool == CadToolType.Pan)) && _isPanning)
        {
            _isPanning = false;
            ReleaseMouseCapture();
            Cursor = ActiveTool == CadToolType.Pan ? Cursors.Hand : Cursors.Cross;
            e.Handled = true;
            return;
        }

        if (e.ChangedButton == MouseButton.Left)
        {
            if (_isBoxSelecting)
            {
                _isBoxSelecting = false;
                ReleaseMouseCapture();

                if (Document != null)
                {
                    bool isShift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
                    bool isCtrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

                    var selectable = SelectableEntities().Where(e => Document.GetLayer(e.Layer)?.IsLocked == false);
                    var selected = CadSelectionEngine.SelectByBox(selectable, _boxSelectStart, _boxSelectCurrent);

                    if (!isShift && !isCtrl)
                    {
                        foreach (var ent in Document.Entities) ent.IsSelected = false;
                    }

                    foreach (var ent in selected)
                    {
                        if (isCtrl) ent.IsSelected = false;
                        else ent.IsSelected = true;
                    }

                    UpdateGrips();
                }

                InvalidateVisual();
            }
            else if (_isDraggingGrip)
            {
                _isDraggingGrip = false;
                _draggingGrip = null;
                UpdateGrips();
                ReleaseMouseCapture();
            }
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        var screenPt = e.GetPosition(this);
        var invertedScreenY = ActualHeight - screenPt.Y;
        var factor = e.Delta > 0 ? 1.2 : 0.833333;
        var newZoom = _transform.Zoom * factor;

        _transform.ZoomAtScreenPoint(newZoom, new ScreenPoint(screenPt.X, invertedScreenY));
        ZoomLevel = _transform.Zoom;

        InvalidateVisual();
        e.Handled = true;
    }

    private void AcceptModificationPoint(Point2D point)
    {
        if(_modification?.Point(point)==true)CompleteModificationStep();
    }
    private void CompleteModificationStep()
    {
        var session=_modification;
        if(session==null || Document==null || UndoManager==null)return;
        if(session.Step==3 && session.Tool==CadToolType.Mirror)
        {
            var menu=new ContextMenu();bool chosen=false;
            foreach(var keep in new[]{true,false})
            {
                var option=new MenuItem {Header=keep?"Manter original (criar espelhado)":"Substituir original"};
                option.Click+=(_,_)=>{chosen=true;if(session.Input(keep?"sim":"não"))CompleteModificationStep();};
                menu.Items.Add(option);
            }
            menu.Closed+=(_,_)=>{if(ContextMenu==menu)ContextMenu=null;if(!chosen && _modification==session)CancelOperation();};
            ContextMenu=menu;menu.IsOpen=true;return;
        }
        if(session.IsReady && session.Confirm(Document,UndoManager))
        {
            _modification=null;_parameterText="";ActiveTool=CadToolType.Select;UpdateGrips();
            CommandManager.InvalidateRequerySuggested();
        }
        InvalidateVisual();
    }
    private void CommitText()
    {
        if(Document==null || UndoManager==null || _textPreview==null || Document.GetLayer(_textPreview.Layer) is not {IsVisible:true,IsLocked:false})return;
        UndoManager.ExecuteAction(new AddEntityAction(Document,_textPreview));ActiveTool=CadToolType.Select;_textPreview=null;CommandManager.InvalidateRequerySuggested();
    }
    private void UpdateTextPreview(Point2D p)
    {
        if (!double.IsFinite(p.X) || !double.IsFinite(p.Y) || Document == null) return;
        _textPreview = new CadText { Text=_textContent, Height=_textHeight, Position=p, Layer=Document.ActiveLayer,
            ColorHex=Document.GetLayer(Document.ActiveLayer)?.ColorHex ?? "#38BDF8" };
    }
    protected override void OnTextInput(TextCompositionEventArgs e)
    {
        if (_modification != null || ActiveTool == CadToolType.Text)
        {
            _parameterText += e.Text;
            if(_modification is {Tool:CadToolType.Offset,Step:1})_modification.Hover(_rawMouseWorld,_parameterText);
            e.Handled = true;
            InvalidateVisual();
            return;
        }
        base.OnTextInput(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase ||
            e.OriginalSource is System.Windows.Controls.Primitives.TextBoxBase)
        {
            return;
        }

        base.OnKeyDown(e);

        if (Document == null) return;

        if (e.Key == Key.Tab && ActiveTool == CadToolType.Select && _cycleCandidates != null && _cycleCandidates.Count > 1)
        {
            bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            var prev = _cycleCandidates[_cycleCandidateIndex];
            if (!isShift) prev.IsSelected = false;

            _cycleCandidateIndex = (_cycleCandidateIndex + 1) % _cycleCandidates.Count;
            var next = _cycleCandidates[_cycleCandidateIndex];
            var layerObj = Document.GetLayer(next.Layer);
            if (layerObj?.IsLocked != true)
            {
                next.IsSelected = true;
            }
            UpdateGrips();
            CommandStatus = $"Entidade {_cycleCandidateIndex + 1}/{_cycleCandidates.Count} ({next.EntityType}, Camada: {next.Layer}). [Tab/Clique: alternar]";
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.A && ActiveTool == CadToolType.Select && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            SelectAll();
            e.Handled = true;
            return;
        }

        if (ActiveTool == CadToolType.Text)
        {
            if (e.Key == Key.Escape) { ActiveTool = CadToolType.Select; _textPreview=null; _parameterText=""; }
            else if (e.Key == Key.Back && _parameterText.Length > 0) _parameterText = _parameterText[..^1];
            else if (e.Key == Key.Enter)
            {
                if (_textStep == 0 && !string.IsNullOrWhiteSpace(_parameterText) && _parameterText.Length <= 500 && !_parameterText.Any(char.IsControl))
                { _textContent = _parameterText; _parameterText=""; _textStep=1; }
                else if (_textStep == 1 && double.TryParse(_parameterText.Replace(',', '.'),NumberStyles.Float,CultureInfo.InvariantCulture,out var height) && double.IsFinite(height) && height >= .05)
                { _textHeight=height; _parameterText=""; _textStep=2; UpdateTextPreview(_currentSnap.Point); }
                else if (_textStep == 2) CommitText();

            }
            e.Handled=true; InvalidateVisual(); return;
        }
        if (_trimExtendSession != null && e.Key is not (Key.F8 or Key.F10 or Key.F3))
        {
            if (e.Key == Key.Escape)
            {
                if (_trimExtendSession.Step == 1) _trimExtendSession.Cancel();
                else { _trimExtendSession = null; ActiveTool = CadToolType.Select; }
                UpdateCommandStatus();
                e.Handled = true; InvalidateVisual(); return;
            }
            if (e.Key == Key.Enter)
            {
                if (_trimExtendSession.Step == 0) _trimExtendSession.ConfirmBoundaries(VisibleEntities());
                else { _trimExtendSession = null; ActiveTool = CadToolType.Select; }
                UpdateCommandStatus();
                e.Handled = true; InvalidateVisual(); return;
            }
        }
        if (_modification != null && e.Key is not (Key.F8 or Key.F10 or Key.F3))
        {
            if (e.Key == Key.Escape) { _modification = null; _parameterText = ""; ActiveTool = CadToolType.Select; }
            else if (e.Key == Key.Back && _parameterText.Length > 0) _parameterText = _parameterText[..^1];
            else if (e.Key == Key.Enter)
            {
                if (_modification.Step == 0) _modification.Select(Document.Entities);
                else if (_parameterText.Length > 0)
                {
                    if (_modification.Input(_parameterText)) { _parameterText = "";CompleteModificationStep(); }
                }
                else if (_modification.Step is 2 or 5 or 6)
                {
                    AcceptModificationPoint(_currentSnap.Point);
                }
            }
            e.Handled = true; InvalidateVisual(); return;
        }

        if (e.Key == Key.F8)
        {
            ToggleOrthoCommand?.Execute(null);
            InvalidateVisual();
            e.Handled = true;
        }
        else if (e.Key == Key.F10)
        {
            TogglePolarCommand?.Execute(null);
            InvalidateVisual();
            e.Handled = true;
        }
        else if (e.Key == Key.F3)
        {
            ToggleOsnapCommand?.Execute(null);
            InvalidateVisual();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ResetDraftState();
            ClearSelection();
            ActiveTool = CadToolType.Select;
            InvalidateVisual();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            var toDelete = Document.Entities.Where(ent => ent.IsSelected).ToList();
            if (toDelete.Any(ent => Document.GetLayer(ent.Layer)?.IsLocked != false))
            {
                CommandStatus = "A seleção contém entidades em camadas bloqueadas e não pode ser excluída.";
                e.Handled = true;
                return;
            }
            if (toDelete.Count > 0 && UndoManager != null)
            {
                var actions = toDelete.Select(ent => new DeleteEntityAction(Document, ent)).ToList();
                UndoManager.ExecuteAction(new CompositeUndoableAction(actions, $"Delete {toDelete.Count} entities"));
                UpdateGrips();
                InvalidateVisual();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            UndoManager?.Undo();
            UpdateGrips();
            InvalidateVisual();
            e.Handled = true;
        }
        else if (e.Key == Key.Y && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            UndoManager?.Redo();
            UpdateGrips();
            InvalidateVisual();
            e.Handled = true;
        }
    }

    #endregion

    #region Interaction Handlers

    private void HandleSelectMouseDown(Point2D worldPt)
    {
        if (Document == null) return;

        var gripTol = CadSnapEngine.ConvertScreenPixelsToMeters(Document.Preferences.GripHitSizePixels, _transform.Zoom);
        var hitGrip = CadGripEngine.HitTestGrips(_currentGrips, worldPt, gripTol);

        if (hitGrip != null && _gripOwnerEntity != null && Document.GetLayer(_gripOwnerEntity.Layer)?.IsLocked == false)
        {
            _isDraggingGrip = true;
            _draggingGrip = hitGrip;
            CaptureMouse();
            return;
        }

        var hitTol = CadSnapEngine.ConvertScreenPixelsToMeters(Document.Preferences.SelectionHitSizePixels, _transform.Zoom);
        bool isShift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        bool isSameLocation = _lastSelectClickPoint.HasValue &&
                              _lastSelectClickPoint.Value.DistanceTo(worldPt) <= hitTol &&
                              _cycleCandidates != null && _cycleCandidates.Count > 1;

        if (isSameLocation)
        {
            _cycleCandidateIndex = (_cycleCandidateIndex + 1) % _cycleCandidates!.Count;
        }
        else
        {
            _lastSelectClickPoint = worldPt;
            _cycleCandidates = CadSelectionEngine.HitTestAllEntities(SelectableEntities(), worldPt, hitTol);
            _cycleCandidateIndex = 0;
        }

        if (_cycleCandidates != null && _cycleCandidates.Count > 0)
        {
            var hitEntity = _cycleCandidates[_cycleCandidateIndex];
            var layerObj = Document.GetLayer(hitEntity.Layer);

            if (layerObj?.IsLocked == true)
            {
                CommandStatus = $"A entidade na camada '{hitEntity.Layer}' está bloqueada e não pode ser editada.";
                InvalidateVisual();
                return;
            }

            if (!isShift && !isSameLocation)
            {
                foreach (var e in Document.Entities) e.IsSelected = false;
            }
            else if (isSameLocation && !isShift)
            {
                foreach (var e in Document.Entities) e.IsSelected = false;
            }

            hitEntity.IsSelected = isSameLocation ? true : !hitEntity.IsSelected;
            UpdateGrips();

            if (_cycleCandidates.Count > 1)
            {
                CommandStatus = $"Entidade {_cycleCandidateIndex + 1}/{_cycleCandidates.Count} ({hitEntity.EntityType}, Camada: {hitEntity.Layer}). [Clique novamente ou Tab para alternar]";
            }
            else
            {
                CommandStatus = $"Entidade ({hitEntity.EntityType}, Camada: {hitEntity.Layer}) selecionada.";
            }
        }
        else
        {
            _cycleCandidates = null;
            _cycleCandidateIndex = 0;
            if (!isShift) ClearSelection();

            // Start box selection
            _isBoxSelecting = true;
            _boxSelectStart = worldPt;
            _boxSelectCurrent = worldPt;
            CaptureMouse();
        }
    }

    private void HandleToolMouseDown(Point2D pt)
    {
        if (Document == null || Document.GetLayer(Document.ActiveLayer) is not { IsVisible: true, IsLocked: false }) return;

        var activeLayer = Document.ActiveLayer;
        var layerObj = Document.GetLayer(activeLayer);
        var color = layerObj?.ColorHex ?? "#38BDF8";

        if (ActiveTool == CadToolType.Line)
        {
            if (_draftStartPoint == null)
            {
                _draftStartPoint = pt;
            }
            else
            {
                var line = new CadLine(_draftStartPoint.Value, pt, activeLayer, color);
                UndoManager?.ExecuteAction(new AddEntityAction(Document, line));
                _draftStartPoint = null;
            }
        }
        else if (ActiveTool == CadToolType.Rectangle)
        {
            if (_draftStartPoint == null)
            {
                _draftStartPoint = pt;
            }
            else
            {
                var p1 = _draftStartPoint.Value;
                var minX = Math.Min(p1.X, pt.X);
                var minY = Math.Min(p1.Y, pt.Y);
                var w = Math.Abs(pt.X - p1.X);
                var h = Math.Abs(pt.Y - p1.Y);

                if (w > 0.01 && h > 0.01)
                {
                    var rect = new CadRectangle(new Point2D(minX, minY), w, h, 0.0, activeLayer, color);
                    UndoManager?.ExecuteAction(new AddEntityAction(Document, rect));
                }
                _draftStartPoint = null;
            }
        }
        else if (ActiveTool == CadToolType.Circle)
        {
            if (_draftStartPoint == null)
            {
                _draftStartPoint = pt;
            }
            else
            {
                var r = _draftStartPoint.Value.DistanceTo(pt);
                if (r > 0.01)
                {
                    var circle = new CadCircle(_draftStartPoint.Value, r, activeLayer, color);
                    UndoManager?.ExecuteAction(new AddEntityAction(Document, circle));
                }
                _draftStartPoint = null;
            }
        }
        else if (ActiveTool == CadToolType.Arc)
        {
            if (_draftStartPoint == null)
            {
                _draftStartPoint = pt;
            }
            else if (_arcRadiusPoint == null)
            {
                _arcRadiusPoint = pt;
            }
            else
            {
                var center = _draftStartPoint.Value;
                var r = center.DistanceTo(_arcRadiusPoint.Value);
                if (r > 0.01)
                {
                    var startAngle = Math.Atan2(_arcRadiusPoint.Value.Y - center.Y, _arcRadiusPoint.Value.X - center.X) * 180.0 / Math.PI;
                    var endAngle = Math.Atan2(pt.Y - center.Y, pt.X - center.X) * 180.0 / Math.PI;
                    if (startAngle < 0) startAngle += 360;
                    if (endAngle < 0) endAngle += 360;
                    if (Math.Abs(endAngle - startAngle) > 0.1)
                    {
                        var arc = new CadArc(center, r, startAngle, endAngle, activeLayer, color);
                        UndoManager?.ExecuteAction(new AddEntityAction(Document, arc));
                    }
                }
                _draftStartPoint = null;
                _arcRadiusPoint = null;
            }
        }
        else if (ActiveTool == CadToolType.Polyline)
        {
            _polyDraftPoints.Add(pt);
        }
        else if (ActiveTool == CadToolType.Dimension)
        {
            if (_dimensionStep == 0)
            {
                _dimP1 = pt;
                _dimensionStep = 1;
            }
            else if (_dimensionStep == 1)
            {
                _dimP2 = pt;
                _dimensionStep = 2;
            }
            else if (_dimensionStep == 2)
            {
                var dim = new CadDimension
                {
                    StartPoint = _dimP1,
                    EndPoint = _dimP2,
                    OffsetPoint = pt,
                    Layer = activeLayer,
                    ColorHex = "#EF4444"
                };
                UndoManager?.ExecuteAction(new AddEntityAction(Document, dim));
                _dimensionStep = 0;
            }
        }
    }

    private void CommitPolyline(bool isClosed)
    {
        if (Document == null || _polyDraftPoints.Count < 2) return;

        var activeLayer = Document.ActiveLayer;
        var color = Document.GetLayer(activeLayer)?.ColorHex ?? "#38BDF8";
        if (Document.GetLayer(activeLayer) is not { IsVisible: true, IsLocked: false }) return;
        var poly = new CadPolyline(_polyDraftPoints, isClosed, activeLayer, color);

        UndoManager?.ExecuteAction(new AddEntityAction(Document, poly));
        _polyDraftPoints.Clear();
    }

    #endregion
}

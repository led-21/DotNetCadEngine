using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
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

    private readonly ViewportTransform _transform = new();

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
    private Point2D _draftCurrentPoint;
    private readonly List<Point2D> _polyDraftPoints = [];
    private int _dimensionStep;
    private Point2D _dimP1;
    private Point2D _dimP2;

    // Snapping state
    private PrecisionSnapResult _currentSnap;
    private Point2D _rawMouseWorld;

    public CadViewport()
    {
        Focusable = true;
        ClipToBounds = true;
        Cursor = Cursors.Cross;

        // Default zoom centered
        _transform.Zoom = 25.0; // 25 px/meter
        _transform.PanX = 400.0;
        _transform.PanY = 300.0;
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
        vp.UpdateGrips();
        vp.InvalidateVisual();
    }

    private IEnumerable<ICadEntity> VisibleEntities() =>
        Document?.Entities.Where(entity => Document.GetLayer(entity.Layer)?.IsVisible != false)
        ?? Enumerable.Empty<ICadEntity>();

    private static void OnActiveToolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadViewport vp)
        {
            vp.ResetDraftState();
            vp.Cursor = vp.ActiveTool == CadToolType.Pan ? Cursors.Hand : Cursors.Cross;
            vp.InvalidateVisual();
        }
    }

    public void ResetDraftState()
    {
        _draftStartPoint = null;
        _polyDraftPoints.Clear();
        _dimensionStep = 0;
        _isBoxSelecting = false;
        _isDraggingGrip = false;
        _draggingGrip = null;
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

    public void UpdateGrips()
    {
        _currentGrips.Clear();
        if (Document == null) return;

        var selected = Document.Entities.Where(e => e.IsSelected).ToList();
        SelectedCount = selected.Count;

        if (selected.Count == 1)
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

        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(15, 23, 42)), null, bounds);

        DrawGrid(dc);
        DrawAxes(dc);

        if (Document != null)
        {
            DrawEntities(dc);
        }

        DrawActiveDraft(dc);
        DrawSelectionBox(dc);
        DrawGrips(dc);
        DrawSnapGuides(dc);
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

    private void DrawEntities(DrawingContext dc)
    {
        var doc = Document!;
        foreach (var entity in doc.Entities)
        {
            var layer = doc.GetLayer(entity.Layer);
            if (layer != null && !layer.IsVisible) continue;

            var color = ColorFromHex(entity.ColorHex);
            if (entity.IsSelected) color = Color.FromRgb(56, 189, 248); // Highlight Cyan

            var pen = new Pen(new SolidColorBrush(color), Math.Max(1.0, entity.LineThickness));
            pen.Freeze();

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
        var gripBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
        var borderPen = new Pen(Brushes.White, 1.0);
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
            var precisionPt = _currentSnap.Point;

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
        Point2D? origin = _draftStartPoint;
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

        _draftCurrentPoint = _currentSnap.Point;

        if (_isBoxSelecting)
        {
            _boxSelectCurrent = _rawMouseWorld;
            InvalidateVisual();
            return;
        }

        if (_isDraggingGrip && _draggingGrip != null && _gripOwnerEntity != null)
        {
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

                    var selected = CadSelectionEngine.SelectByBox(VisibleEntities(), _boxSelectStart, _boxSelectCurrent);

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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (Document == null) return;

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
            ActiveTool = CadToolType.Select;
            InvalidateVisual();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            var toDelete = Document.Entities.Where(ent => ent.IsSelected).ToList();
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

        if (hitGrip != null)
        {
            _isDraggingGrip = true;
            _draggingGrip = hitGrip;
            CaptureMouse();
            return;
        }

        var hitTol = CadSnapEngine.ConvertScreenPixelsToMeters(Document.Preferences.SelectionHitSizePixels, _transform.Zoom);
        var hitEntity = CadSelectionEngine.SelectByPoint(VisibleEntities(), worldPt, hitTol);

        bool isShift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        if (hitEntity != null)
        {
            if (!isShift)
            {
                foreach (var e in Document.Entities) e.IsSelected = false;
            }
            hitEntity.IsSelected = !hitEntity.IsSelected;
            UpdateGrips();
        }
        else
        {
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
        if (Document == null) return;

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
                    Layer = "Dimensions",
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
        var poly = new CadPolyline(_polyDraftPoints, isClosed, activeLayer, color);

        UndoManager?.ExecuteAction(new AddEntityAction(Document, poly));
        _polyDraftPoints.Clear();
    }

    #endregion
}

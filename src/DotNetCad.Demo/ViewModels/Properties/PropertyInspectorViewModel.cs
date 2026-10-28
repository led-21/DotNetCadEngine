using System.Globalization;
using System.Windows.Input;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Core.History;
using DotNetCad.Core.History.Actions;
using DotNetCad.Core.Measurements;
using DotNetCad.Demo.Mvvm;

namespace DotNetCad.Demo.ViewModels.Properties;

public enum PropertyPanelMode
{
    DocumentSettings,
    SingleEntity,
    MultipleEntities
}

public sealed record CadColorOption(string Name, string Hex);

public sealed class PropertyInspectorViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private PropertyPanelMode _mode = PropertyPanelMode.DocumentSettings;
    private string _validationError = string.Empty;

    public PropertyInspectorViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
        CommitPropertyCommand = new RelayCommand(CommitCurrentEdit);
        CancelPropertyCommand = new RelayCommand(CancelCurrentEdit);
        SelectColorCommand = new RelayCommand(p =>
        {
            if (p is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                if (IsSingleEntityMode) SingleEntityColor = hex;
                else if (IsMultipleEntitiesMode) ApplyBatchColor(hex);
            }
        });
    }

    public static readonly IReadOnlyList<CadColorOption> StandardColors =
    [
        new("Branco", "#FFFFFF"),
        new("Ciano", "#38BDF8"),
        new("Verde", "#22C55E"),
        new("Vermelho", "#EF4444"),
        new("Amarelo", "#EAB308"),
        new("Azul", "#3B82F6"),
        new("Magenta", "#EC4899"),
        new("Laranja", "#F97316"),
        new("Roxo", "#A855F7"),
        new("Cinza Claro", "#94A3B8"),
        new("Cinza Escuro", "#64748B"),
    ];

    public IReadOnlyList<CadColorOption> ColorPalette => StandardColors;
    public ICommand SelectColorCommand { get; }

    public CadDocument Document => _mainViewModel.Document;
    public UndoRedoManager UndoManager => _mainViewModel.UndoManager;

    public PropertyPanelMode Mode
    {
        get => _mode;
        private set => SetProperty(ref _mode, value);
    }

    public bool IsDocumentMode => Mode == PropertyPanelMode.DocumentSettings;
    public bool IsSingleEntityMode => Mode == PropertyPanelMode.SingleEntity;
    public bool IsMultipleEntitiesMode => Mode == PropertyPanelMode.MultipleEntities;

    public string ValidationError
    {
        get => _validationError;
        set => SetProperty(ref _validationError, value);
    }

    public ICommand CommitPropertyCommand { get; }
    public ICommand CancelPropertyCommand { get; }

    #region Document Settings Properties

    public string DocumentTitle => Document.Title;

    public IReadOnlyList<string> SupportedUnits => CadUnitConverter.SupportedUnits;

    public string DisplayUnit
    {
        get => Document.Preferences.DisplayUnit;
        set
        {
            if (Document.Preferences.DisplayUnit != value)
            {
                Document.Preferences.DisplayUnit = value;
                OnPropertyChanged();
                Refresh();
                _mainViewModel.DrawingRevision++;
            }
        }
    }

    public IReadOnlyList<int> SupportedPrecisions { get; } = [0, 1, 2, 3, 4, 5];

    public int DecimalPrecision
    {
        get => Document.Preferences.DecimalPrecision;
        set
        {
            if (Document.Preferences.DecimalPrecision != value)
            {
                Document.Preferences.DecimalPrecision = value;
                OnPropertyChanged();
                Refresh();
                _mainViewModel.DrawingRevision++;
            }
        }
    }

    public int TotalEntitiesCount => Document.Entities.Count;
    public int TotalLayersCount => Document.Layers.Count;

    public string BoundingBoxSummary
    {
        get
        {
            var bbox = Document.BoundingBox;
            if (bbox.IsEmpty) return "Vazio";
            var w = CadUnitConverter.FormatLength(bbox.Width, DisplayUnit, DecimalPrecision);
            var h = CadUnitConverter.FormatLength(bbox.Height, DisplayUnit, DecimalPrecision);
            return $"{w} × {h}";
        }
    }

    #endregion

    #region Multiple Selection Properties

    public int SelectedCount => SelectedEntities.Count;

    public string MultiSelectionSummary
    {
        get
        {
            if (SelectedCount <= 1) return string.Empty;
            var groups = SelectedEntities
                .GroupBy(e => e.EntityType)
                .Select(g => $"{g.Count()} {GetEntityTypeName(g.Key)}");
            return $"{SelectedCount} itens selecionados: {string.Join(", ", groups)}";
        }
    }

    public string CommonLayer
    {
        get
        {
            if (SelectedEntities.Count == 0) return string.Empty;
            var first = SelectedEntities[0].Layer;
            return SelectedEntities.All(e => e.Layer == first) ? first : "(Múltiplos valores)";
        }
    }

    public string CommonColor
    {
        get
        {
            if (SelectedEntities.Count == 0) return string.Empty;
            var first = SelectedEntities[0].ColorHex;
            return SelectedEntities.All(e => e.ColorHex == first) ? first : "(Múltiplos valores)";
        }
    }

    public string CommonColorBrush => CommonColor == "(Múltiplos valores)" ? "Transparent" : CommonColor;

    public void ApplyBatchLayer(string newLayer)
    {
        if (string.IsNullOrWhiteSpace(newLayer) || newLayer == "(Múltiplos valores)") return;
        var toChange = SelectedEntities.Where(e => e.Layer != newLayer).ToList();
        if (toChange.Count == 0) return;

        var actions = toChange.Select(e => new ChangeEntityLayerAction(e, newLayer)).Cast<IUndoableAction>().ToList();
        UndoManager.ExecuteAction(new CompositeUndoableAction(actions, $"Alterar camada de {toChange.Count} entidades para {newLayer}"));
        _mainViewModel.DrawingRevision++;
        Refresh();
    }

    public static string NormalizeColorHex(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "#FFFFFF";
        var trimmed = input.Trim().ToUpperInvariant();
        if (!trimmed.StartsWith('#'))
        {
            if (trimmed.Length == 6 || trimmed.Length == 8) trimmed = "#" + trimmed;
            else if (trimmed.Length == 3) trimmed = $"#{trimmed[0]}{trimmed[0]}{trimmed[1]}{trimmed[1]}{trimmed[2]}{trimmed[2]}";
        }
        return trimmed;
    }

    public void ApplyBatchColor(string newColor)
    {
        if (string.IsNullOrWhiteSpace(newColor) || newColor == "(Múltiplos valores)") return;
        var normalized = NormalizeColorHex(newColor);
        var toChange = SelectedEntities.Where(e => !string.Equals(e.ColorHex, normalized, StringComparison.OrdinalIgnoreCase)).ToList();
        if (toChange.Count == 0) return;

        var actions = toChange.Select(e => new ChangeEntityColorAction(e, normalized)).Cast<IUndoableAction>().ToList();
        UndoManager.ExecuteAction(new CompositeUndoableAction(actions, $"Alterar cor de {toChange.Count} entidades para {normalized}"));
        _mainViewModel.DrawingRevision++;
        Refresh();
    }

    #endregion

    #region Single Entity Properties

    public ICadEntity? SingleEntity => SelectedEntities.Count == 1 ? SelectedEntities[0] : null;

    public string EntityTypeLabel => SingleEntity != null ? GetEntityTypeName(SingleEntity.EntityType) : string.Empty;
    public string EntityIdText => SingleEntity != null ? SingleEntity.Id.ToString()[..8] : string.Empty;

    public IReadOnlyList<string> AvailableLayers => Document.Layers.Select(l => l.Name).ToList();

    public string SingleEntityLayer
    {
        get => SingleEntity?.Layer ?? string.Empty;
        set
        {
            if (SingleEntity != null && !string.IsNullOrWhiteSpace(value) && SingleEntity.Layer != value)
            {
                if (Document.GetLayer(SingleEntity.Layer)?.IsLocked == true)
                {
                    ValidationError = "Entidade pertence a uma camada bloqueada.";
                    return;
                }
                UndoManager.ExecuteAction(new ChangeEntityLayerAction(SingleEntity, value));
                _mainViewModel.DrawingRevision++;
                OnPropertyChanged();
            }
        }
    }

    public string SingleEntityColor
    {
        get => SingleEntity?.ColorHex?.ToUpperInvariant() ?? string.Empty;
        set
        {
            if (SingleEntity != null && !string.IsNullOrWhiteSpace(value))
            {
                var normalized = NormalizeColorHex(value);
                if (!string.Equals(SingleEntity.ColorHex, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    if (Document.GetLayer(SingleEntity.Layer)?.IsLocked == true)
                    {
                        ValidationError = "Entidade pertence a uma camada bloqueada.";
                        return;
                    }
                    ValidationError = string.Empty;
                    UndoManager.ExecuteAction(new ChangeEntityColorAction(SingleEntity, normalized));
                    _mainViewModel.DrawingRevision++;
                    OnPropertyChanged();
                }
            }
        }
    }

    // Type Booleans
    public bool IsLine => SingleEntity is CadLine;
    public bool IsCircle => SingleEntity is CadCircle;
    public bool IsRectangle => SingleEntity is CadRectangle;
    public bool IsArc => SingleEntity is CadArc;
    public bool IsPolyline => SingleEntity is CadPolyline;
    public bool IsText => SingleEntity is CadText;
    public bool IsDimension => SingleEntity is CadDimension;

    // Line Properties
    public string LineX1
    {
        get => SingleEntity is CadLine l ? FormatCoord(l.StartPoint.X) : "";
        set => TryUpdateLinePoint(p1X: ParseCoord(value));
    }
    public string LineY1
    {
        get => SingleEntity is CadLine l ? FormatCoord(l.StartPoint.Y) : "";
        set => TryUpdateLinePoint(p1Y: ParseCoord(value));
    }
    public string LineX2
    {
        get => SingleEntity is CadLine l ? FormatCoord(l.EndPoint.X) : "";
        set => TryUpdateLinePoint(p2X: ParseCoord(value));
    }
    public string LineY2
    {
        get => SingleEntity is CadLine l ? FormatCoord(l.EndPoint.Y) : "";
        set => TryUpdateLinePoint(p2Y: ParseCoord(value));
    }
    public string LineLength
    {
        get => SingleEntity is CadLine l ? FormatLengthVal(l.Length) : "";
        set => TryUpdateLineLength(value);
    }
    public string LineAngle
    {
        get => SingleEntity is CadLine l ? CadUnitConverter.FormatAngle(l.AngleDegrees, 1, false) : "";
        set => TryUpdateLineAngle(value);
    }

    // Circle Properties
    public string CircleCenterX
    {
        get => SingleEntity is CadCircle c ? FormatCoord(c.Center.X) : "";
        set => TryUpdateCircleCenter(x: ParseCoord(value));
    }
    public string CircleCenterY
    {
        get => SingleEntity is CadCircle c ? FormatCoord(c.Center.Y) : "";
        set => TryUpdateCircleCenter(y: ParseCoord(value));
    }
    public string CircleRadius
    {
        get => SingleEntity is CadCircle c ? FormatLengthVal(c.Radius) : "";
        set => TryUpdateCircleRadius(value);
    }
    public string CircleDiameter => SingleEntity is CadCircle c ? FormatLengthVal(c.Radius * 2.0) : "";
    public string CircleCircumference => SingleEntity is CadCircle c ? FormatLengthVal(2.0 * Math.PI * c.Radius) : "";
    public string CircleArea => SingleEntity is CadCircle c ? CadUnitConverter.FormatArea(Math.PI * c.Radius * c.Radius, DisplayUnit, DecimalPrecision) : "";

    // Rectangle Properties
    public string RectX
    {
        get => SingleEntity is CadRectangle r ? FormatCoord(r.Origin.X) : "";
        set => TryUpdateRect(x: ParseCoord(value));
    }
    public string RectY
    {
        get => SingleEntity is CadRectangle r ? FormatCoord(r.Origin.Y) : "";
        set => TryUpdateRect(y: ParseCoord(value));
    }
    public string RectWidth
    {
        get => SingleEntity is CadRectangle r ? FormatLengthVal(r.Width) : "";
        set => TryUpdateRect(widthText: value);
    }
    public string RectHeight
    {
        get => SingleEntity is CadRectangle r ? FormatLengthVal(r.Height) : "";
        set => TryUpdateRect(heightText: value);
    }
    public string RectArea => SingleEntity is CadRectangle r ? CadUnitConverter.FormatArea(r.Width * r.Height, DisplayUnit, DecimalPrecision) : "";
    public string RectPerimeter => SingleEntity is CadRectangle r ? FormatLengthVal(2.0 * (r.Width + r.Height)) : "";

    // Arc Properties
    public string ArcCenterX
    {
        get => SingleEntity is CadArc a ? FormatCoord(a.Center.X) : "";
        set => TryUpdateArcCenter(x: ParseCoord(value));
    }
    public string ArcCenterY
    {
        get => SingleEntity is CadArc a ? FormatCoord(a.Center.Y) : "";
        set => TryUpdateArcCenter(y: ParseCoord(value));
    }
    public string ArcRadius
    {
        get => SingleEntity is CadArc a ? FormatLengthVal(a.Radius) : "";
        set => TryUpdateArcRadius(value);
    }
    public string ArcStartAngle
    {
        get => SingleEntity is CadArc a ? CadUnitConverter.FormatAngle(a.StartAngleDegrees, 1, false) : "";
        set => TryUpdateArcAngle(startAngleText: value);
    }
    public string ArcEndAngle
    {
        get => SingleEntity is CadArc a ? CadUnitConverter.FormatAngle(a.EndAngleDegrees, 1, false) : "";
        set => TryUpdateArcAngle(endAngleText: value);
    }
    public string ArcLength
    {
        get
        {
            if (SingleEntity is not CadArc a) return "";
            var sweep = (a.EndAngleDegrees - a.StartAngleDegrees + 360.0) % 360.0;
            if (sweep < 1e-4) sweep = 360.0;
            var arcLen = a.Radius * (sweep * Math.PI / 180.0);
            return FormatLengthVal(arcLen);
        }
    }

    // Polyline Properties
    public int PolylineVertexCount => SingleEntity is CadPolyline p ? p.Vertices.Count : 0;
    public bool PolylineIsClosed
    {
        get => SingleEntity is CadPolyline p && p.IsClosed;
        set
        {
            if (SingleEntity is CadPolyline p && p.IsClosed != value)
            {
                var copy = new CadPolyline(p.Vertices, value, p.Layer, p.ColorHex) { LineThickness = p.LineThickness, IsSelected = true };
                UndoManager.ExecuteAction(new ReplaceEntityAction(Document, p, copy, "Alterar fechamento de polilinha"));
                _mainViewModel.DrawingRevision++;
                Refresh();
            }
        }
    }
    public string PolylineLength
    {
        get
        {
            if (SingleEntity is not CadPolyline p || p.Vertices.Count < 2) return "";
            var len = 0.0;
            for (int i = 0; i < p.Vertices.Count - 1; i++) len += p.Vertices[i].DistanceTo(p.Vertices[i + 1]);
            if (p.IsClosed && p.Vertices.Count >= 3) len += p.Vertices[^1].DistanceTo(p.Vertices[0]);
            return FormatLengthVal(len);
        }
    }

    // Text Properties
    public string TextContent
    {
        get => SingleEntity is CadText t ? t.Text : "";
        set
        {
            if (SingleEntity is CadText t && !string.IsNullOrWhiteSpace(value) && t.Text != value)
            {
                var copy = new CadText(t.Position, value, t.Height, t.RotationDegrees, t.Layer, t.ColorHex) { LineThickness = t.LineThickness, IsSelected = true };
                UndoManager.ExecuteAction(new ReplaceEntityAction(Document, t, copy, "Alterar texto"));
                _mainViewModel.DrawingRevision++;
                Refresh();
            }
        }
    }
    public string TextHeight
    {
        get => SingleEntity is CadText t ? FormatLengthVal(t.Height) : "";
        set
        {
            if (SingleEntity is not CadText t) return;
            if (CadUnitConverter.TryParseLength(value, DisplayUnit, out var h, out var err))
            {
                if (h < 0.01) { ValidationError = "Altura do texto deve ser de pelo menos 0,01."; return; }
                var copy = new CadText(t.Position, t.Text, h, t.RotationDegrees, t.Layer, t.ColorHex) { LineThickness = t.LineThickness, IsSelected = true };
                UndoManager.ExecuteAction(new ReplaceEntityAction(Document, t, copy, "Alterar altura do texto"));
                _mainViewModel.DrawingRevision++;
                Refresh();
            }
            else
            {
                ValidationError = err;
            }
        }
    }
    public string TextRotation
    {
        get => SingleEntity is CadText t ? CadUnitConverter.FormatAngle(t.RotationDegrees, 1, false) : "";
        set
        {
            if (SingleEntity is CadText t && TryParseDouble(value, out var rot))
            {
                var copy = new CadText(t.Position, t.Text, t.Height, rot, t.Layer, t.ColorHex) { LineThickness = t.LineThickness, IsSelected = true };
                UndoManager.ExecuteAction(new ReplaceEntityAction(Document, t, copy, "Alterar rotação do texto"));
                _mainViewModel.DrawingRevision++;
                Refresh();
            }
        }
    }

    // Dimension Properties
    public string DimMeasuredLength => SingleEntity is CadDimension d ? FormatLengthVal(d.StartPoint.DistanceTo(d.EndPoint)) : "";
    public string DimPrefix
    {
        get => SingleEntity is CadDimension d ? d.Prefix : "";
        set
        {
            if (SingleEntity is CadDimension d && d.Prefix != value)
            {
                var copy = new CadDimension(d.StartPoint, d.EndPoint, d.OffsetPoint, d.Layer, d.ColorHex)
                {
                    Prefix = value, Suffix = d.Suffix, CustomTextOverride = d.CustomTextOverride, LineThickness = d.LineThickness, IsSelected = true
                };
                UndoManager.ExecuteAction(new ReplaceEntityAction(Document, d, copy, "Alterar prefixo de cota"));
                _mainViewModel.DrawingRevision++;
                Refresh();
            }
        }
    }
    public string DimSuffix
    {
        get => SingleEntity is CadDimension d ? d.Suffix : "";
        set
        {
            if (SingleEntity is CadDimension d && d.Suffix != value)
            {
                var copy = new CadDimension(d.StartPoint, d.EndPoint, d.OffsetPoint, d.Layer, d.ColorHex)
                {
                    Prefix = d.Prefix, Suffix = value, CustomTextOverride = d.CustomTextOverride, LineThickness = d.LineThickness, IsSelected = true
                };
                UndoManager.ExecuteAction(new ReplaceEntityAction(Document, d, copy, "Alterar sufixo de cota"));
                _mainViewModel.DrawingRevision++;
                Refresh();
            }
        }
    }

    #endregion

    #region Mutation & Validation Helpers

    private void TryUpdateLinePoint(double? p1X = null, double? p1Y = null, double? p2X = null, double? p2Y = null)
    {
        if (SingleEntity is not CadLine line) return;
        if (CheckLocked(line)) return;

        var start = new Point2D(p1X ?? line.StartPoint.X, p1Y ?? line.StartPoint.Y);
        var end = new Point2D(p2X ?? line.EndPoint.X, p2Y ?? line.EndPoint.Y);

        if (start.DistanceTo(end) < 1e-4)
        {
            ValidationError = "Comprimento da linha não pode ser nulo ou degenerado.";
            return;
        }

        var copy = new CadLine(start, end, line.Layer, line.ColorHex) { LineThickness = line.LineThickness, IsSelected = true };
        UndoManager.ExecuteAction(new ReplaceEntityAction(Document, line, copy, "Alterar pontos da linha"));
        ValidationError = string.Empty;
        _mainViewModel.DrawingRevision++;
        Refresh();
    }

    private void TryUpdateLineLength(string lengthText)
    {
        if (SingleEntity is not CadLine line) return;
        if (CheckLocked(line)) return;

        if (!CadUnitConverter.TryParseLength(lengthText, DisplayUnit, out var newLength, out var err))
        {
            ValidationError = err;
            return;
        }
        if (newLength <= 1e-4)
        {
            ValidationError = "Comprimento deve ser maior que zero.";
            return;
        }

        // Preserve StartPoint by default
        var angleRad = Math.Atan2(line.EndPoint.Y - line.StartPoint.Y, line.EndPoint.X - line.StartPoint.X);
        var newEnd = new Point2D(line.StartPoint.X + (newLength * Math.Cos(angleRad)), line.StartPoint.Y + (newLength * Math.Sin(angleRad)));

        var copy = new CadLine(line.StartPoint, newEnd, line.Layer, line.ColorHex) { LineThickness = line.LineThickness, IsSelected = true };
        UndoManager.ExecuteAction(new ReplaceEntityAction(Document, line, copy, "Alterar comprimento da linha"));
        ValidationError = string.Empty;
        _mainViewModel.DrawingRevision++;
        Refresh();
    }

    private void TryUpdateLineAngle(string angleText)
    {
        if (SingleEntity is not CadLine line) return;
        if (CheckLocked(line)) return;

        if (!TryParseDouble(angleText, out var angleDeg))
        {
            ValidationError = "Ângulo inválido.";
            return;
        }

        var length = line.StartPoint.DistanceTo(line.EndPoint);
        var angleRad = angleDeg * Math.PI / 180.0;
        var newEnd = new Point2D(line.StartPoint.X + (length * Math.Cos(angleRad)), line.StartPoint.Y + (length * Math.Sin(angleRad)));

        var copy = new CadLine(line.StartPoint, newEnd, line.Layer, line.ColorHex) { LineThickness = line.LineThickness, IsSelected = true };
        UndoManager.ExecuteAction(new ReplaceEntityAction(Document, line, copy, "Alterar ângulo da linha"));
        ValidationError = string.Empty;
        _mainViewModel.DrawingRevision++;
        Refresh();
    }

    private void TryUpdateCircleCenter(double? x = null, double? y = null)
    {
        if (SingleEntity is not CadCircle circle) return;
        if (CheckLocked(circle)) return;

        var center = new Point2D(x ?? circle.Center.X, y ?? circle.Center.Y);
        var copy = new CadCircle(center, circle.Radius, circle.Layer, circle.ColorHex) { LineThickness = circle.LineThickness, IsSelected = true };
        UndoManager.ExecuteAction(new ReplaceEntityAction(Document, circle, copy, "Alterar centro do círculo"));
        ValidationError = string.Empty;
        _mainViewModel.DrawingRevision++;
        Refresh();
    }

    private void TryUpdateCircleRadius(string radiusText)
    {
        if (SingleEntity is not CadCircle circle) return;
        if (CheckLocked(circle)) return;

        if (!CadUnitConverter.TryParseLength(radiusText, DisplayUnit, out var newRadius, out var err))
        {
            ValidationError = err;
            return;
        }
        if (newRadius <= 1e-4)
        {
            ValidationError = "Raio deve ser maior que zero.";
            return;
        }

        var copy = new CadCircle(circle.Center, newRadius, circle.Layer, circle.ColorHex) { LineThickness = circle.LineThickness, IsSelected = true };
        UndoManager.ExecuteAction(new ReplaceEntityAction(Document, circle, copy, "Alterar raio do círculo"));
        ValidationError = string.Empty;
        _mainViewModel.DrawingRevision++;
        Refresh();
    }

    private void TryUpdateRect(double? x = null, double? y = null, string? widthText = null, string? heightText = null)
    {
        if (SingleEntity is not CadRectangle rect) return;
        if (CheckLocked(rect)) return;

        var origin = new Point2D(x ?? rect.Origin.X, y ?? rect.Origin.Y);
        var w = rect.Width;
        var h = rect.Height;

        if (widthText != null)
        {
            if (!CadUnitConverter.TryParseLength(widthText, DisplayUnit, out w, out var err)) { ValidationError = err; return; }
            if (w <= 1e-4) { ValidationError = "Largura deve ser maior que zero."; return; }
        }
        if (heightText != null)
        {
            if (!CadUnitConverter.TryParseLength(heightText, DisplayUnit, out h, out var err)) { ValidationError = err; return; }
            if (h <= 1e-4) { ValidationError = "Altura deve ser maior que zero."; return; }
        }

        var copy = new CadRectangle(origin, w, h, rect.RotationDegrees, rect.Layer, rect.ColorHex) { LineThickness = rect.LineThickness, IsSelected = true };
        UndoManager.ExecuteAction(new ReplaceEntityAction(Document, rect, copy, "Alterar dimensões do retângulo"));
        ValidationError = string.Empty;
        _mainViewModel.DrawingRevision++;
        Refresh();
    }

    private void TryUpdateArcCenter(double? x = null, double? y = null)
    {
        if (SingleEntity is not CadArc arc) return;
        if (CheckLocked(arc)) return;

        var center = new Point2D(x ?? arc.Center.X, y ?? arc.Center.Y);
        var copy = new CadArc(center, arc.Radius, arc.StartAngleDegrees, arc.EndAngleDegrees, arc.Layer, arc.ColorHex) { LineThickness = arc.LineThickness, IsSelected = true };
        UndoManager.ExecuteAction(new ReplaceEntityAction(Document, arc, copy, "Alterar centro do arco"));
        ValidationError = string.Empty;
        _mainViewModel.DrawingRevision++;
        Refresh();
    }

    private void TryUpdateArcRadius(string radiusText)
    {
        if (SingleEntity is not CadArc arc) return;
        if (CheckLocked(arc)) return;

        if (!CadUnitConverter.TryParseLength(radiusText, DisplayUnit, out var r, out var err)) { ValidationError = err; return; }
        if (r <= 1e-4) { ValidationError = "Raio deve ser maior que zero."; return; }

        var copy = new CadArc(arc.Center, r, arc.StartAngleDegrees, arc.EndAngleDegrees, arc.Layer, arc.ColorHex) { LineThickness = arc.LineThickness, IsSelected = true };
        UndoManager.ExecuteAction(new ReplaceEntityAction(Document, arc, copy, "Alterar raio do arco"));
        ValidationError = string.Empty;
        _mainViewModel.DrawingRevision++;
        Refresh();
    }

    private void TryUpdateArcAngle(string? startAngleText = null, string? endAngleText = null)
    {
        if (SingleEntity is not CadArc arc) return;
        if (CheckLocked(arc)) return;

        var start = arc.StartAngleDegrees;
        var end = arc.EndAngleDegrees;

        if (startAngleText != null && TryParseDouble(startAngleText, out var s)) start = (s % 360 + 360) % 360;
        if (endAngleText != null && TryParseDouble(endAngleText, out var e)) end = (e % 360 + 360) % 360;

        var copy = new CadArc(arc.Center, arc.Radius, start, end, arc.Layer, arc.ColorHex) { LineThickness = arc.LineThickness, IsSelected = true };
        UndoManager.ExecuteAction(new ReplaceEntityAction(Document, arc, copy, "Alterar ângulos do arco"));
        ValidationError = string.Empty;
        _mainViewModel.DrawingRevision++;
        Refresh();
    }

    private bool CheckLocked(ICadEntity entity)
    {
        if (Document.GetLayer(entity.Layer)?.IsLocked == true)
        {
            ValidationError = "Não é permitido alterar entidades em camadas bloqueadas.";
            return true;
        }
        return false;
    }

    private void CommitCurrentEdit()
    {
        ValidationError = string.Empty;
    }

    private void CancelCurrentEdit()
    {
        ValidationError = string.Empty;
        Refresh();
    }

    #endregion

    #region Formatting Helpers

    private List<ICadEntity> SelectedEntities => Document.Entities.Where(e => e.IsSelected).ToList();

    public void Refresh()
    {
        var count = SelectedEntities.Count;
        Mode = count switch
        {
            0 => PropertyPanelMode.DocumentSettings,
            1 => PropertyPanelMode.SingleEntity,
            _ => PropertyPanelMode.MultipleEntities
        };

        ValidationError = string.Empty;

        OnPropertyChanged(nameof(Mode));
        OnPropertyChanged(nameof(IsDocumentMode));
        OnPropertyChanged(nameof(IsSingleEntityMode));
        OnPropertyChanged(nameof(IsMultipleEntitiesMode));

        OnPropertyChanged(nameof(DocumentTitle));
        OnPropertyChanged(nameof(DisplayUnit));
        OnPropertyChanged(nameof(DecimalPrecision));
        OnPropertyChanged(nameof(TotalEntitiesCount));
        OnPropertyChanged(nameof(TotalLayersCount));
        OnPropertyChanged(nameof(BoundingBoxSummary));

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(MultiSelectionSummary));
        OnPropertyChanged(nameof(CommonLayer));
        OnPropertyChanged(nameof(CommonColor));

        OnPropertyChanged(nameof(SingleEntity));
        OnPropertyChanged(nameof(EntityTypeLabel));
        OnPropertyChanged(nameof(EntityIdText));
        OnPropertyChanged(nameof(AvailableLayers));
        OnPropertyChanged(nameof(SingleEntityLayer));
        OnPropertyChanged(nameof(SingleEntityColor));

        OnPropertyChanged(nameof(IsLine));
        OnPropertyChanged(nameof(IsCircle));
        OnPropertyChanged(nameof(IsRectangle));
        OnPropertyChanged(nameof(IsArc));
        OnPropertyChanged(nameof(IsPolyline));
        OnPropertyChanged(nameof(IsText));
        OnPropertyChanged(nameof(IsDimension));

        // Line
        OnPropertyChanged(nameof(LineX1));
        OnPropertyChanged(nameof(LineY1));
        OnPropertyChanged(nameof(LineX2));
        OnPropertyChanged(nameof(LineY2));
        OnPropertyChanged(nameof(LineLength));
        OnPropertyChanged(nameof(LineAngle));

        // Circle
        OnPropertyChanged(nameof(CircleCenterX));
        OnPropertyChanged(nameof(CircleCenterY));
        OnPropertyChanged(nameof(CircleRadius));
        OnPropertyChanged(nameof(CircleDiameter));
        OnPropertyChanged(nameof(CircleCircumference));
        OnPropertyChanged(nameof(CircleArea));

        // Rect
        OnPropertyChanged(nameof(RectX));
        OnPropertyChanged(nameof(RectY));
        OnPropertyChanged(nameof(RectWidth));
        OnPropertyChanged(nameof(RectHeight));
        OnPropertyChanged(nameof(RectArea));
        OnPropertyChanged(nameof(RectPerimeter));

        // Arc
        OnPropertyChanged(nameof(ArcCenterX));
        OnPropertyChanged(nameof(ArcCenterY));
        OnPropertyChanged(nameof(ArcRadius));
        OnPropertyChanged(nameof(ArcStartAngle));
        OnPropertyChanged(nameof(ArcEndAngle));
        OnPropertyChanged(nameof(ArcLength));

        // Polyline
        OnPropertyChanged(nameof(PolylineVertexCount));
        OnPropertyChanged(nameof(PolylineIsClosed));
        OnPropertyChanged(nameof(PolylineLength));

        // Text
        OnPropertyChanged(nameof(TextContent));
        OnPropertyChanged(nameof(TextHeight));
        OnPropertyChanged(nameof(TextRotation));

        // Dimension
        OnPropertyChanged(nameof(DimMeasuredLength));
        OnPropertyChanged(nameof(DimPrefix));
        OnPropertyChanged(nameof(DimSuffix));
    }

    private string FormatCoord(double meters)
    {
        var display = CadUnitConverter.FromMeters(meters, DisplayUnit);
        return display.ToString($"F{DecimalPrecision}", CultureInfo.CurrentCulture);
    }

    private double? ParseCoord(string text)
    {
        if (CadUnitConverter.TryParseLength(text, DisplayUnit, out var meters, out var err))
        {
            return meters;
        }
        if (!string.IsNullOrEmpty(err)) ValidationError = err;
        return null;
    }

    private string FormatLengthVal(double meters)
    {
        return CadUnitConverter.FormatLength(meters, DisplayUnit, DecimalPrecision, includeUnit: true);
    }

    private static bool TryParseDouble(string text, out double value)
    {
        var clean = text.Trim().Replace(',', '.');
        return double.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    }

    private static string GetEntityTypeName(CadEntityType type) => type switch
    {
        CadEntityType.Line => "Linha",
        CadEntityType.Circle => "Círculo",
        CadEntityType.Rectangle => "Retângulo",
        CadEntityType.Arc => "Arco",
        CadEntityType.Polyline => "Polilinha",
        CadEntityType.Text => "Texto",
        CadEntityType.Dimension => "Cota",
        _ => type.ToString()
    };

    #endregion
}

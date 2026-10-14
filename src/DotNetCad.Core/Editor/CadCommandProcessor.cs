using System.Globalization;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Core.History;
using DotNetCad.Core.History.Actions;

namespace DotNetCad.Core.Editor;

public sealed record CadCommandResult(bool Success, string Message, bool DrawingChanged = false, bool LayersChanged = false);

/// <summary>Executes one CAD command against the same document and history used by the viewport.</summary>
public static class CadCommandProcessor
{
    public const string Help = "HELP | LINE x1;y1 x2;y2 | CIRCLE x;y radius | RECT x1;y1 x2;y2 | ARC x;y radius start end | POLYLINE x1;y1 x2;y2 [...] | TEXT x;y height words... | DIM x1;y1 x2;y2 offsetX;offsetY | LAYER name | LAYER SHOW|HIDE name | LAYER COLOR name #RRGGBB | PROPERTIES id | PROPERTIES LAYER name | LIST | BREAK lineId x1;y1 x2;y2 | TRIM lineId boundaryId pickX;pickY | EXTEND lineId boundaryId START|END | OFFSET id distance [LEFT|RIGHT] | MOVE id dx;dy | COPY id dx;dy | ROTATE id centerX;centerY angleDegrees | MIRROR id axisX1;axisY1 axisX2;axisY2 | DELETE id | CLEAR | UNDO | REDO";

    public static CadCommandResult Execute(CadDocument document, UndoRedoManager history, string input)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(history);
        if (string.IsNullOrWhiteSpace(input)) return new(false, "Enter a command. Type HELP for syntax.");

        var parts = input.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToUpperInvariant();

        try
        {
            switch (command)
            {
                case "HELP":
                    return new(true, Help);
                case "LINE" when parts.Length == 3:
                {
                    if (!Point(parts[1], out var start) || !Point(parts[2], out var end)) return InvalidPoint();
                    var validation = GeometryValidator.ValidateSegment(start, end);
                    if (!validation.IsValid) return new(false, validation.ErrorMessage!);
                    return Add(new CadLine(start, end, document.ActiveLayer, ActiveColor(document)), document, history);
                }
                case "CIRCLE" when parts.Length == 3:
                {
                    if (!Point(parts[1], out var center) || !Number(parts[2], out var radius)) return new(false, "Use CIRCLE x;y radius.");
                    var validation = GeometryValidator.ValidateCircle(center, radius);
                    if (!validation.IsValid) return new(false, validation.ErrorMessage!);
                    return Add(new CadCircle(center, radius, document.ActiveLayer, ActiveColor(document)), document, history);
                }
                case "RECT" when parts.Length == 3:
                {
                    if (!Point(parts[1], out var a) || !Point(parts[2], out var b)) return InvalidPoint();
                    var width = Math.Abs(b.X - a.X);
                    var height = Math.Abs(b.Y - a.Y);
                    var validation = GeometryValidator.ValidateDimensions(width, height);
                    if (!validation.IsValid) return new(false, validation.ErrorMessage!);
                    var origin = new Point2D(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y));
                    return Add(new CadRectangle(origin, width, height, 0, document.ActiveLayer, ActiveColor(document)), document, history);
                }
                case "ARC" when parts.Length == 5:
                {
                    if (!Point(parts[1], out var center) || !Number(parts[2], out var radius) ||
                        !Number(parts[3], out var start) || !Number(parts[4], out var end))
                        return new(false, "Use ARC x;y radius startAngle endAngle.");
                    var validation = GeometryValidator.ValidateCircle(center, radius);
                    if (!validation.IsValid) return new(false, validation.ErrorMessage!);
                    if ((end - start) % 360 == 0) return new(false, "Arc sweep must be between 0 and 360 degrees.");
                    return Add(new CadArc(center, radius, start, end, document.ActiveLayer, ActiveColor(document)), document, history);
                }
                case "POLYLINE" when parts.Length >= 3:
                {
                    var vertices = new List<Point2D>();
                    foreach (var token in parts.Skip(1))
                    {
                        if (!Point(token, out var vertex)) return InvalidPoint();
                        vertices.Add(vertex);
                    }
                    if (vertices.Zip(vertices.Skip(1)).Any(pair => pair.First.DistanceTo(pair.Second) < 0.01))
                        return new(false, "Adjacent polyline points must be distinct.");
                    return Add(new CadPolyline(vertices, false, document.ActiveLayer, ActiveColor(document)), document, history);
                }
                case "TEXT" when parts.Length >= 4:
                {
                    if (!Point(parts[1], out var position) || !Number(parts[2], out var height))
                        return new(false, "Use TEXT x;y height words...");
                    if (height < 0.05) return new(false, "Text height must be at least 0.05 units.");
                    var content = string.Join(" ", parts.Skip(3));
                    if (content.Length > 500 || content.Any(char.IsControl))
                        return new(false, "Text must be one line of at most 500 characters.");
                    return Add(new CadText
                    {
                        Position = position,
                        Height = height,
                        Text = content,
                        Layer = document.ActiveLayer,
                        ColorHex = ActiveColor(document)
                    }, document, history);
                }
                case "DIM" when parts.Length == 4:
                {
                    if (!Point(parts[1], out var start) || !Point(parts[2], out var end) ||
                        !Point(parts[3], out var offset)) return InvalidPoint();
                    var validation = GeometryValidator.ValidateSegment(start, end);
                    if (!validation.IsValid) return new(false, validation.ErrorMessage!);
                    var (dimStart, _) = CadDimension.CalculateDimensionLinePoints(start, end, offset);
                    if (start.DistanceTo(dimStart) < 0.01)
                        return new(false, "Dimension offset must be away from the measured line.");
                    return Add(new CadDimension
                    {
                        StartPoint = start,
                        EndPoint = end,
                        OffsetPoint = offset,
                        Layer = document.ActiveLayer,
                        ColorHex = ActiveColor(document)
                    }, document, history);
                }
                case "LAYER" when parts.Length == 2:
                {
                    var layer = document.GetLayer(parts[1]);
                    if (layer == null)
                    {
                        layer = new CadLayer { Name = parts[1], ColorHex = "#38BDF8" };
                        document.Layers.Add(layer);
                    }
                    if (!layer.IsVisible) return new(false, "Make the layer visible before activating it.");
                    document.ActiveLayer = layer.Name;
                    return new(true, $"Active layer: {layer.Name}", LayersChanged: true);
                }
                case "LAYER" when parts.Length == 3 &&
                    (parts[1].Equals("SHOW", StringComparison.OrdinalIgnoreCase) ||
                     parts[1].Equals("HIDE", StringComparison.OrdinalIgnoreCase)):
                {
                    var layer = document.GetLayer(parts[2]);
                    if (layer == null) return new(false, "Layer not found.");
                    var show = parts[1].Equals("SHOW", StringComparison.OrdinalIgnoreCase);
                    if (layer.IsVisible == show)
                        return new(true, $"Layer {layer.Name} is already {(show ? "visible" : "hidden")}.");
                    var nextActive = document.ActiveLayer;
                    if (!show && document.ActiveLayer.Equals(layer.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        nextActive = document.Layers.FirstOrDefault(item => item != layer && item.IsVisible)?.Name ?? string.Empty;
                        if (nextActive.Length == 0) return new(false, "At least one visible layer must remain active.");
                    }
                    history.ExecuteAction(new SetLayerVisibilityAction(document, layer, show, nextActive));
                    return new(true, $"Layer {layer.Name} is now {(show ? "visible" : "hidden")}. Active layer: {document.ActiveLayer}.",
                        DrawingChanged: true, LayersChanged: true);
                }
                case "LAYER" when parts.Length == 4 && parts[1].Equals("COLOR", StringComparison.OrdinalIgnoreCase):
                {
                    var layer = document.GetLayer(parts[2]);
                    if (layer == null) return new(false, "Layer not found.");
                    var color = parts[3];
                    if (color.Length != 7 || color[0] != '#' || !color.Skip(1).All(Uri.IsHexDigit))
                        return new(false, "Use a color in #RRGGBB form.");
                    color = color.ToUpperInvariant();
                    if (layer.ColorHex.Equals(color, StringComparison.OrdinalIgnoreCase))
                        return new(true, $"Layer {layer.Name} already uses {color}.");
                    history.ExecuteAction(new SetLayerColorAction(document, layer, color));
                    return new(true, $"Layer {layer.Name} color changed to {color}.", DrawingChanged: true, LayersChanged: true);
                }
                case "PROPERTIES" when parts.Length == 2:
                {
                    if (!TryEntity(document, parts[1], out var entity, out var error)) return new(false, error);
                    return new(true, DescribeEntity(entity!));
                }
                case "PROPERTIES" when parts.Length == 3 && parts[1].Equals("LAYER", StringComparison.OrdinalIgnoreCase):
                {
                    var layer = document.GetLayer(parts[2]);
                    return layer == null ? new(false, "Layer not found.") : new(true, DescribeLayer(document, layer));
                }
                case "LIST" when parts.Length == 1:
                {
                    if (document.Entities.Count == 0) return new(true, "Drawing is empty.");
                    return new(true, string.Join(Environment.NewLine,
                        document.Entities.Select(e => $"{e.Id}  {e.EntityType}  [{e.Layer}]")));
                }
                case "BREAK" when parts.Length == 4:
                {
                    if (!TryLine(document, parts[1], out var line, out var error)) return new(false, error);
                    if (!Point(parts[2], out var first) || !Point(parts[3], out var second)) return InvalidPoint();
                    if (!TryPositionOnLine(line!, first, out var firstT) ||
                        !TryPositionOnLine(line!, second, out var secondT))
                        return new(false, "Both break points must lie on the line segment.");
                    var from = Math.Min(firstT, secondT);
                    var to = Math.Max(firstT, secondT);
                    if ((to - from) * line!.Length < 0.01) return new(false, "Break points must be at least 0.01 units apart.");
                    var pieces = new List<CadLine>();
                    if (from * line.Length >= 0.01) pieces.Add(CopyLine(line, line.StartPoint, Along(line, from)));
                    if ((1 - to) * line.Length >= 0.01) pieces.Add(CopyLine(line, Along(line, to), line.EndPoint));
                    return ReplaceLine(document, history, line, pieces, "Break line");
                }
                case "TRIM" when parts.Length == 4:
                {
                    if (!TryLine(document, parts[1], out var line, out var error)) return new(false, error);
                    if (!TryLine(document, parts[2], out var boundary, out error)) return new(false, error);
                    if (line!.Id == boundary!.Id) return new(false, "Choose a different boundary line.");
                    if (!Point(parts[3], out var pick)) return InvalidPoint();
                    if (!TryPositionOnLine(line, pick, out _)) return new(false, "Pick point must lie on the line to trim.");
                    var trimmed = CadConstructionMath.TrimSegment(line.StartPoint, line.EndPoint,
                        boundary.StartPoint, boundary.EndPoint, pick);
                    if (trimmed == null) return new(false, "The line and boundary do not intersect as segments.");
                    if (trimmed.Value.Start.DistanceTo(line.StartPoint) < 0.01 &&
                        trimmed.Value.End.DistanceTo(line.EndPoint) < 0.01)
                        return new(false, "The boundary does not shorten this line.");
                    var validation = GeometryValidator.ValidateSegment(trimmed.Value.Start, trimmed.Value.End);
                    if (!validation.IsValid) return new(false, validation.ErrorMessage!);
                    return ReplaceLine(document, history, line,
                        [CopyLine(line, trimmed.Value.Start, trimmed.Value.End)], "Trim line");
                }
                case "EXTEND" when parts.Length == 4:
                {
                    if (!TryLine(document, parts[1], out var line, out var error)) return new(false, error);
                    if (!TryLine(document, parts[2], out var boundary, out error)) return new(false, error);
                    if (line!.Id == boundary!.Id) return new(false, "Choose a different boundary line.");
                    var extendStart = parts[3].Equals("START", StringComparison.OrdinalIgnoreCase);
                    if (!extendStart && !parts[3].Equals("END", StringComparison.OrdinalIgnoreCase))
                        return new(false, "Use START or END to select the endpoint to extend.");
                    var extended = extendStart
                        ? CadConstructionMath.ExtendSegment(line.EndPoint, line.StartPoint, boundary.StartPoint, boundary.EndPoint)
                        : CadConstructionMath.ExtendSegment(line.StartPoint, line.EndPoint, boundary.StartPoint, boundary.EndPoint);
                    if (extended == null) return new(false, "The selected endpoint cannot reach the boundary segment.");
                    var newStart = extendStart ? extended.Value.End : line.StartPoint;
                    var newEnd = extendStart ? line.EndPoint : extended.Value.End;
                    if (line.StartPoint.DistanceTo(newStart) < 0.01 && line.EndPoint.DistanceTo(newEnd) < 0.01)
                        return new(false, "The line already touches the boundary.");
                    var validation = GeometryValidator.ValidateSegment(newStart, newEnd);
                    if (!validation.IsValid) return new(false, validation.ErrorMessage!);
                    return ReplaceLine(document, history, line, [CopyLine(line, newStart, newEnd)], "Extend line");
                }
                case "OFFSET" when parts.Length is 3 or 4:
                {
                    if (!Guid.TryParse(parts[1], out var id)) return new(false, "Use an entity ID from LIST.");
                    var entity = document.Entities.FirstOrDefault(e => e.Id == id);
                    if (entity == null) return new(false, "Entity not found.");
                    if (!Number(parts[2], out var distance) || Math.Abs(distance) < 0.01)
                        return new(false, "Offset distance must be at least 0.01 units.");
                    ICadEntity offset;
                    if (entity is CadLine source)
                    {
                        if (distance < 0) return new(false, "Use a positive distance and LEFT or RIGHT for a line offset.");
                        var left = parts.Length == 3 || parts[3].Equals("LEFT", StringComparison.OrdinalIgnoreCase);
                        if (!left && !parts[3].Equals("RIGHT", StringComparison.OrdinalIgnoreCase))
                            return new(false, "Use LEFT or RIGHT for a line offset.");
                        var (a, b) = CadConstructionMath.OffsetSegment(source.StartPoint, source.EndPoint,
                            distance, toLeft: left);
                        var validation = GeometryValidator.ValidateSegment(a, b);
                        if (!validation.IsValid) return new(false, validation.ErrorMessage!);
                        offset = CopyLine(source, a, b);
                    }
                    else if (entity is CadCircle circle && parts.Length == 3)
                    {
                        var radius = circle.Radius + distance;
                        var validation = GeometryValidator.ValidateCircle(circle.Center, radius);
                        if (!validation.IsValid) return new(false, validation.ErrorMessage!);
                        offset = new CadCircle(circle.Center, radius, circle.Layer, circle.ColorHex)
                            { LineThickness = circle.LineThickness };
                    }
                    else return new(false, "OFFSET supports lines and circles; LEFT/RIGHT applies only to lines.");
                    return Add(offset, document, history);
                }
                case "MOVE" when parts.Length == 3:
                {
                    if (!TryEntity(document, parts[1], out var entity, out var error)) return new(false, error);
                    if (!Point(parts[2], out var delta)) return InvalidPoint();
                    if (delta.X == 0 && delta.Y == 0)
                        return new(false, "Move displacement must be nonzero.");
                    var preview = entity!.Clone();
                    preview.Translate(delta.X, delta.Y);
                    if (!HasFiniteBounds(preview)) return new(false, "Move produces invalid geometry.");
                    history.ExecuteAction(new MoveEntityAction(entity, delta.X, delta.Y));
                    return new(true, $"Moved {entity.Id} by {delta.X};{delta.Y}.", DrawingChanged: true);
                }
                case "COPY" when parts.Length == 3:
                {
                    if (!TryEntity(document, parts[1], out var entity, out var error)) return new(false, error);
                    if (!Point(parts[2], out var delta)) return InvalidPoint();
                    if (delta.X == 0 && delta.Y == 0)
                        return new(false, "Copy displacement must be nonzero.");
                    var copy = entity!.Clone();
                    copy.Translate(delta.X, delta.Y);
                    if (!HasFiniteBounds(copy)) return new(false, "Copy produces invalid geometry.");
                    return Add(copy, document, history);
                }
                case "ROTATE" when parts.Length == 4:
                {
                    if (!TryEntity(document, parts[1], out var entity, out var error)) return new(false, error);
                    if (!Point(parts[2], out var center) || !Number(parts[3], out var angle))
                        return new(false, "Use ROTATE id centerX;centerY angleDegrees.");
                    if (angle % 360 == 0)
                        return new(false, "Rotation angle must change the entity.");
                    var preview = entity!.Clone();
                    preview.Rotate(center, angle);
                    if (!HasFiniteBounds(preview)) return new(false, "Rotation produces invalid geometry.");
                    history.ExecuteAction(new RotateEntityAction(entity, center, angle));
                    return new(true, $"Rotated {entity.Id} by {angle} degrees.", DrawingChanged: true);
                }
                case "MIRROR" when parts.Length == 4:
                {
                    if (!TryEntity(document, parts[1], out var entity, out var error)) return new(false, error);
                    if (!Point(parts[2], out var axisStart) || !Point(parts[3], out var axisEnd))
                        return InvalidPoint();
                    var validation = GeometryValidator.ValidateSegment(axisStart, axisEnd);
                    if (!validation.IsValid) return new(false, "Mirror axis must have two distinct points.");
                    var preview = entity!.Clone();
                    preview.Mirror(axisStart, axisEnd);
                    if (!HasFiniteBounds(preview)) return new(false, "Mirror produces invalid geometry.");
                    history.ExecuteAction(new MirrorEntityAction(entity, axisStart, axisEnd));
                    return new(true, $"Mirrored {entity.Id} across the specified axis.", DrawingChanged: true);
                }
                case "DELETE" when parts.Length == 2:
                {
                    if (!Guid.TryParse(parts[1], out var id)) return new(false, "Use DELETE followed by an entity ID from LIST.");
                    var entity = document.Entities.FirstOrDefault(e => e.Id == id);
                    if (entity == null) return new(false, "Entity not found.");
                    history.ExecuteAction(new DeleteEntityAction(document, entity));
                    return new(true, $"Deleted {id}.", DrawingChanged: true);
                }
                case "CLEAR" when parts.Length == 1:
                {
                    if (document.Entities.Count == 0) return new(true, "Drawing is already empty.");
                    var actions = document.Entities.Select(e => (IUndoableAction)new DeleteEntityAction(document, e)).ToList();
                    history.ExecuteAction(new CompositeUndoableAction(actions, "Clear drawing"));
                    return new(true, $"Removed {actions.Count} entities.", DrawingChanged: true);
                }
                case "UNDO" when parts.Length == 1:
                    if (!history.CanUndo) return new(false, "Nothing to undo.");
                    history.Undo();
                    return new(true, "Undone.", DrawingChanged: true, LayersChanged: true);
                case "REDO" when parts.Length == 1:
                    if (!history.CanRedo) return new(false, "Nothing to redo.");
                    history.Redo();
                    return new(true, "Redone.", DrawingChanged: true, LayersChanged: true);
                default:
                    return new(false, $"Unknown command or argument count. {Help}");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return new(false, ex.Message);
        }
    }

    private static CadCommandResult Add(ICadEntity entity, CadDocument document, UndoRedoManager history)
    {
        history.ExecuteAction(new AddEntityAction(document, entity));
        return new(true, $"Added {entity.EntityType} {entity.Id} on {entity.Layer}.", DrawingChanged: true);
    }

    private static CadCommandResult ReplaceLine(CadDocument document, UndoRedoManager history,
        CadLine original, IReadOnlyList<CadLine> replacements, string description)
    {
        var actions = new List<IUndoableAction> { new DeleteEntityAction(document, original) };
        actions.AddRange(replacements.Select(line => (IUndoableAction)new AddEntityAction(document, line)));
        history.ExecuteAction(new CompositeUndoableAction(actions, description));
        return new(true, $"{description}: replaced {original.Id} with {replacements.Count} line(s): " +
            string.Join(", ", replacements.Select(line => line.Id)), DrawingChanged: true);
    }

    private static CadLine CopyLine(CadLine source, Point2D start, Point2D end) =>
        new(start, end, source.Layer, source.ColorHex) { LineThickness = source.LineThickness };

    private static bool TryLine(CadDocument document, string text, out CadLine? line, out string error)
    {
        line = null;
        error = "Use a line ID from LIST or describe_drawing.";
        if (!Guid.TryParse(text, out var id)) return false;
        var entity = document.Entities.FirstOrDefault(e => e.Id == id);
        if (entity is CadLine found) { line = found; return true; }
        error = entity == null ? "Entity not found." : "This command requires a line entity.";
        return false;
    }

    private static bool TryEntity(CadDocument document, string text, out ICadEntity? entity, out string error)
    {
        entity = null;
        error = "Use an entity ID from LIST or describe_drawing.";
        if (!Guid.TryParse(text, out var id)) return false;
        entity = document.Entities.FirstOrDefault(item => item.Id == id);
        error = entity == null ? "Entity not found." : string.Empty;
        return entity != null;
    }

    private static bool HasFiniteBounds(ICadEntity entity)
    {
        var bounds = entity.BoundingBox;
        return !bounds.IsEmpty && double.IsFinite(bounds.MinX) && double.IsFinite(bounds.MinY) &&
            double.IsFinite(bounds.MaxX) && double.IsFinite(bounds.MaxY);
    }

    private static string DescribeEntity(ICadEntity entity)
    {
        var bounds = entity.BoundingBox;
        var fields = new List<string>
        {
            $"ID: {entity.Id}", $"Type: {entity.EntityType}", $"Layer: {entity.Layer}",
            $"Color: {entity.ColorHex}", $"Line thickness: {Format(entity.LineThickness)}",
            $"Bounds: {FormatPoint(new(bounds.MinX, bounds.MinY))} to {FormatPoint(new(bounds.MaxX, bounds.MaxY))}"
        };
        switch (entity)
        {
            case CadLine line:
                fields.Add($"Start: {FormatPoint(line.StartPoint)}");
                fields.Add($"End: {FormatPoint(line.EndPoint)}");
                fields.Add($"Length: {Format(line.Length)}");
                break;
            case CadCircle circle:
                fields.Add($"Center: {FormatPoint(circle.Center)}");
                fields.Add($"Radius: {Format(circle.Radius)}");
                fields.Add($"Diameter: {Format(circle.Diameter)}");
                break;
            case CadArc arc:
                fields.Add($"Center: {FormatPoint(arc.Center)}");
                fields.Add($"Radius: {Format(arc.Radius)}");
                fields.Add($"Start angle: {Format(arc.StartAngleDegrees)} degrees");
                fields.Add($"End angle: {Format(arc.EndAngleDegrees)} degrees");
                fields.Add($"Arc length: {Format(arc.ArcLength)}");
                break;
            case CadRectangle rectangle:
                fields.Add($"Origin: {FormatPoint(rectangle.Origin)}");
                fields.Add($"Width: {Format(rectangle.Width)}");
                fields.Add($"Height: {Format(rectangle.Height)}");
                fields.Add($"Rotation: {Format(rectangle.RotationDegrees)} degrees");
                break;
            case CadPolyline polyline:
                fields.Add($"Closed: {polyline.IsClosed}");
                fields.Add($"Vertices: {string.Join(" ", polyline.Vertices.Select(FormatPoint))}");
                fields.Add($"Length: {Format(polyline.Length)}");
                if (polyline.IsClosed) fields.Add($"Area: {Format(polyline.Area)}");
                break;
            case CadText label:
                fields.Add($"Position: {FormatPoint(label.Position)}");
                fields.Add($"Height: {Format(label.Height)}");
                fields.Add($"Rotation: {Format(label.RotationDegrees)} degrees");
                fields.Add($"Text: {label.Text}");
                break;
            case CadDimension dimension:
                fields.Add($"Dimension type: {dimension.DimensionType}");
                fields.Add($"Start: {FormatPoint(dimension.StartPoint)}");
                fields.Add($"End: {FormatPoint(dimension.EndPoint)}");
                fields.Add($"Offset: {FormatPoint(dimension.OffsetPoint)}");
                fields.Add($"Measurement: {dimension.FormattedText}");
                break;
        }
        return string.Join(Environment.NewLine, fields);
    }

    private static string DescribeLayer(CadDocument document, CadLayer layer) =>
        string.Join(Environment.NewLine,
            $"Layer: {layer.Name}", $"Active: {document.ActiveLayer.Equals(layer.Name, StringComparison.OrdinalIgnoreCase)}",
            $"Visible: {layer.IsVisible}", $"Color: {layer.ColorHex}",
            $"Line thickness: {Format(layer.LineThickness)}", $"Locked: {layer.IsLocked}",
            $"Plottable: {layer.IsPlottable}", $"Protected: {layer.IsProtected}",
            $"Entities: {document.Entities.Count(entity => entity.Layer.Equals(layer.Name, StringComparison.OrdinalIgnoreCase))}");

    private static string Format(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);
    private static string FormatPoint(Point2D point) => $"{Format(point.X)};{Format(point.Y)}";

    private static bool TryPositionOnLine(CadLine line, Point2D point, out double position)
    {
        var dx = line.EndPoint.X - line.StartPoint.X;
        var dy = line.EndPoint.Y - line.StartPoint.Y;
        var lengthSquared = dx * dx + dy * dy;
        position = lengthSquared > 0
            ? ((point.X - line.StartPoint.X) * dx + (point.Y - line.StartPoint.Y) * dy) / lengthSquared
            : 0;
        var projected = Along(line, position);
        return position >= -1e-8 && position <= 1 + 1e-8 && point.DistanceTo(projected) <= 0.01;
    }

    private static Point2D Along(CadLine line, double position) =>
        new(line.StartPoint.X + (line.EndPoint.X - line.StartPoint.X) * position,
            line.StartPoint.Y + (line.EndPoint.Y - line.StartPoint.Y) * position);

    private static CadCommandResult InvalidPoint() => new(false, "Use coordinates in x;y form, for example 10;5.");

    private static bool Point(string text, out Point2D point) =>
        CadCoordinateParser.TryParse(text, null, out point, out _);

    private static bool Number(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    private static string ActiveColor(CadDocument document) => document.GetLayer(document.ActiveLayer)?.ColorHex ?? "#38BDF8";
}

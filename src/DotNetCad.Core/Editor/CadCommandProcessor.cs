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
    public const string Help = "HELP | LINE x1;y1 x2;y2 | CIRCLE x;y radius | RECT x1;y1 x2;y2 | ARC x;y radius start end | POLYLINE x1;y1 x2;y2 [...] | LAYER name | LIST | DELETE id | CLEAR | UNDO | REDO";

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
                case "LIST" when parts.Length == 1:
                {
                    if (document.Entities.Count == 0) return new(true, "Drawing is empty.");
                    return new(true, string.Join(Environment.NewLine,
                        document.Entities.Select(e => $"{e.Id}  {e.EntityType}  [{e.Layer}]")));
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
                    return new(true, "Undone.", DrawingChanged: true);
                case "REDO" when parts.Length == 1:
                    if (!history.CanRedo) return new(false, "Nothing to redo.");
                    history.Redo();
                    return new(true, "Redone.", DrawingChanged: true);
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

    private static CadCommandResult InvalidPoint() => new(false, "Use coordinates in x;y form, for example 10;5.");

    private static bool Point(string text, out Point2D point) =>
        CadCoordinateParser.TryParse(text, null, out point, out _);

    private static bool Number(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    private static string ActiveColor(CadDocument document) => document.GetLayer(document.ActiveLayer)?.ColorHex ?? "#38BDF8";
}

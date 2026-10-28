namespace DotNetCad.Core.Editor;

/// <summary>Single syntax source for console help, completion and argument hints.</summary>
public static class CadCommandCatalog
{
    public static IReadOnlyList<string> Syntax { get; } = Array.AsReadOnly(new[]
    {
        "HELP",
        "LINE x1;y1 x2;y2",
        "CIRCLE x;y radius",
        "RECT x1;y1 x2;y2",
        "ARC x;y radius start end",
        "POLYLINE x1;y1 x2;y2 [...]",
        "TEXT x;y height words...",
        "DIM x1;y1 x2;y2 offsetX;offsetY",
        "LAYER name",
        "LAYER SHOW|HIDE name",
        "LAYER COLOR name #RRGGBB",
        "LAYER CREATE|ACTIVE|LOCK|UNLOCK name",
        "LAYER RENAME name newName",
        "LAYER DELETE name TRANSFER destination|ENTITIES|CANCEL",
        "LAYER TRANSFER destination [entityId ...]",
        "LAYER SHOWALL",
        "PROPERTIES id",
        "PROPERTIES LAYER name",
        "LIST",
        "BREAK lineId x1;y1 x2;y2",
        "TRIM lineId boundaryId pickX;pickY",
        "EXTEND lineId boundaryId START|END",
        "OFFSET id distance [LEFT|RIGHT]",
        "MOVE id dx;dy",
        "COPY id dx;dy",
        "ROTATE id centerX;centerY angleDegrees",
        "MIRROR id axisX1;axisY1 axisX2;axisY2",
        "DELETE id",
        "CLEAR",
        "UNDO",
        "REDO",
        "ZOOM EXTENTS",
        "SAVE [path.cad]",
        "OPEN [DISCARD] path.cad|path.dxf",
        "NEW [DISCARD]",
        "BATCH cmd1 | cmd2 | [...]",
        "STAGE [\"summary\"] cmd1 | cmd2 | [...]",
        "COMMIT",
        "DISCARD",
        "AUDIT [CLEAR]",
        "QUERY [LAYER name] [TYPE type] [TEXT substr]",
        "CONFLICTS [layerA] [layerB] [clearance]",
        "TOPOLOGY",
    });
    public static string Help => string.Join(" | ", Syntax);
    public static IReadOnlyList<string> Complete(string prefix) => Syntax.Select(s => s.Split(' ')[0]).Distinct()
        .Where(s => s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).OrderBy(s => s).ToArray();
    public static string Hint(string input)
    {
        var name = input.TrimStart().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return string.Join("  •  ", Syntax.Where(s => s.Split(' ')[0].Equals(name, StringComparison.OrdinalIgnoreCase)));
    }
    public static bool IsReadOnly(string input) => input.TrimStart().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToUpperInvariant() is "HELP" or "LIST" or "PROPERTIES" or "ZOOM" or "AUDIT" or "QUERY" or "CONFLICTS" or "TOPOLOGY";
}

using System.ComponentModel;
using System.Globalization;
using DotNetCad.Infrastructure.Mcp;
using ModelContextProtocol.Server;

namespace DotNetCad.Mcp;

[McpServerToolType]
public sealed class CadTools(CadMcpBridgeClient bridge)
{
    [McpServerTool, Description("Describe the drawing currently open in DotNetCad.Demo, including entity IDs, types, layers, staged AI proposals, and bounds.")]
    public async Task<string> DescribeDrawing() => await Content("describe");

    [McpServerTool, Description("Execute a list of CAD console commands as an atomic All-or-Nothing transaction. If any command fails, all earlier commands in the batch are rolled back automatically. Returns created, modified, and deleted entity IDs and groups the entire batch into a single Undo step.")]
    public async Task<string> ExecuteBatch(string[] commands, string summary = "Operação em Lote MCP", string agentIdentifier = "DotNetCad.Mcp") =>
        await SendStructured(new CadMcpRequest("execute_batch", Commands: commands, Summary: summary, AgentIdentifier: agentIdentifier));

    [McpServerTool, Description("Stage a batch of CAD console commands in sandboxed preview mode without modifying the live drawing. Renders a dashed ghost preview in the CAD viewport so the engineer can inspect and confirm (commit_staged_proposal) or reject (discard_staged_proposal) the AI changes.")]
    public async Task<string> StageBatch(string[] commands, string summary = "Proposta IA", string agentIdentifier = "DotNetCad.Mcp") =>
        await SendStructured(new CadMcpRequest("stage_batch", Commands: commands, Summary: summary, AgentIdentifier: agentIdentifier));

    [McpServerTool, Description("Commit the currently staged AI proposal in DotNetCad.Demo, applying all staged commands atomically and recording an AI audit trail entry.")]
    public async Task<string> CommitStagedProposal(string? proposalId = null) =>
        await SendStructured(new CadMcpRequest("commit_staged", ProposalId: Guid.TryParse(proposalId, out var id) ? id : null));

    [McpServerTool, Description("Discard the currently staged AI proposal in DotNetCad.Demo without changing the live drawing.")]
    public async Task<string> DiscardStagedProposal(string? proposalId = null) =>
        await SendStructured(new CadMcpRequest("discard_staged", ProposalId: Guid.TryParse(proposalId, out var id) ? id : null));

    [McpServerTool, Description("Query entities semantically by layer, comma-separated entity types (Line,Circle,Arc,Rectangle,Polyline,Text,Dimension), text substring, length range, or bounding box window.")]
    public async Task<string> QueryEntities(
        string? layer = null,
        string? entityTypes = null,
        string? textContains = null,
        double? minLength = null,
        double? maxLength = null,
        double? windowMinX = null,
        double? windowMinY = null,
        double? windowMaxX = null,
        double? windowMaxY = null,
        bool allowCrossing = true)
    {
        var parsedTypes = string.IsNullOrWhiteSpace(entityTypes)
            ? null
            : entityTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return await SendStructured(new CadMcpRequest(
            "query_entities",
            Layer: layer,
            EntityTypes: parsedTypes,
            TextContains: textContains,
            MinLength: minLength,
            MaxLength: maxLength,
            WindowMinX: windowMinX,
            WindowMinY: windowMinY,
            WindowMaxX: windowMaxX,
            WindowMaxY: windowMaxY,
            AllowCrossing: allowCrossing));
    }

    [McpServerTool, Description("Detect geometric intersections and minimum-clearance violations between entities (optionally filtered between layerA and layerB, e.g. 'Eletrica' vs 'Drenagem').")]
    public async Task<string> FindSpatialConflicts(string? layerA = null, string? layerB = null, double minClearanceMeters = 0.0) =>
        await SendStructured(new CadMcpRequest("find_conflicts", Layer: layerA, LayerB: layerB, MinClearanceMeters: minClearanceMeters));

    [McpServerTool, Description("Analyze drawing topology: per-layer entity counts, total linear lengths (m), closed areas (m²), and enclosed regions/rooms with their internal text labels.")]
    public async Task<string> AnalyzeDrawingTopology() =>
        await SendStructured(new CadMcpRequest("analyze_topology"));

    [McpServerTool, Description("Retrieve the AI and batch audit trail recorded in the active CAD document, including timestamps, actor identifiers, transaction IDs, and affected entity IDs.")]
    public async Task<string> GetAiAuditHistory() =>
        await SendStructured(new CadMcpRequest("audit_history"));

    [McpServerTool, Description("Add a line to the active layer of the open CAD drawing. Coordinates are in drawing units.")]
    public async Task<string> AddLine(double x1, double y1, double x2, double y2) =>
        await Command($"LINE {Point(x1, y1)} {Point(x2, y2)}");

    [McpServerTool, Description("Add a circle to the active layer of the open CAD drawing.")]
    public async Task<string> AddCircle(double centerX, double centerY, double radius) =>
        await Command($"CIRCLE {Point(centerX, centerY)} {N(radius)}");

    [McpServerTool, Description("Add an axis-aligned rectangle between two opposite corners.")]
    public async Task<string> AddRectangle(double x1, double y1, double x2, double y2) =>
        await Command($"RECT {Point(x1, y1)} {Point(x2, y2)}");

    [McpServerTool, Description("Add a counterclockwise circular arc. Angles are degrees.")]
    public async Task<string> AddArc(double centerX, double centerY, double radius, double startAngle, double endAngle) =>
        await Command($"ARC {Point(centerX, centerY)} {N(radius)} {N(startAngle)} {N(endAngle)}");

    [McpServerTool, Description("Add an open polyline. Supply at least two points separated by spaces, for example '0;0 5;0 5;3'.")]
    public async Task<string> AddPolyline(string points) => await Command($"POLYLINE {points}");

    [McpServerTool, Description("Add a single-line room label at x,y. Height is in drawing units; text may contain spaces.")]
    public async Task<string> AddText(double x, double y, double height, string text) =>
        await Command($"TEXT {Point(x, y)} {N(height)} {text}");

    [McpServerTool, Description("Add an aligned dimension between two points. offsetX,offsetY locate the dimension line; the measured value is displayed in meters.")]
    public async Task<string> AddDimension(double x1, double y1, double x2, double y2,
        double offsetX, double offsetY) =>
        await Command($"DIM {Point(x1, y1)} {Point(x2, y2)} {Point(offsetX, offsetY)}");

    [McpServerTool, Description("Select an existing visible layer or create a new layer for subsequent CAD entities. Names may contain spaces; the layer must be unlocked.")]
    public async Task<string> SetLayer(string name) => await Command($"LAYER {Q(name)}");

    [McpServerTool, Description("Show an existing drawing layer. The change can be undone.")]
    public async Task<string> ShowLayer(string name) => await Command($"LAYER SHOW {Q(name)}");

    [McpServerTool, Description("Hide an existing drawing layer. If it is active, another visible layer becomes active. The change can be undone.")]
    public async Task<string> HideLayer(string name) => await Command($"LAYER HIDE {Q(name)}");

    [McpServerTool, Description("Set a layer color using #RRGGBB. Existing explicit entity colors are preserved; equality does not imply ByLayer. The change can be undone.")]
    public async Task<string> SetLayerColor(string name, string colorHex) =>
        await Command($"LAYER COLOR {Q(name)} {colorHex}");

    [McpServerTool, Description("Create a layer without activating it. Names may contain spaces.")]
    public async Task<string> CreateLayer(string name) => await Command($"LAYER CREATE {Q(name)}");
    [McpServerTool, Description("Rename an unprotected unlocked layer and update all entity references atomically.")]
    public async Task<string> RenameLayer(string name, string newName) => await Command($"LAYER RENAME {Q(name)} {Q(newName)}");
    [McpServerTool, Description("Lock or unlock a layer; locking the active layer selects a visible unlocked successor.")]
    public async Task<string> SetLayerLocked(string name, bool locked) => await Command($"LAYER {(locked ? "LOCK" : "UNLOCK")} {Q(name)}");
    [McpServerTool, Description("Delete an unprotected unlocked layer. Explicit mode: CANCEL, ENTITIES (delete contents), or TRANSFER (requires destination). One undo restores everything.")]
    public async Task<string> DeleteLayer(string name, string mode = "CANCEL", string? destination = null)
    {
        if (mode.ToUpperInvariant() is not ("CANCEL" or "ENTITIES" or "TRANSFER")) throw new ArgumentException("Invalid deletion mode.");
        return await Command($"LAYER DELETE {Q(name)} {mode.ToUpperInvariant()}" + (destination == null ? "" : " " + Q(destination)));
    }
    [McpServerTool, Description("Transfer the UI selection or supplied space-separated entity IDs into a visible unlocked layer. Rejects the entire batch if any source is locked.")]
    public async Task<string> TransferEntitiesToLayer(string name, string? entityIds = null)
    {
        if (entityIds != null && entityIds.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(id => !Guid.TryParse(id, out _))) throw new ArgumentException("Invalid entity IDs.");
        return await Command($"LAYER TRANSFER {Q(name)}" + (entityIds == null ? "" : " " + entityIds));
    }
    private static string Q(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Any(char.IsControl) || name.Contains('"')) throw new ArgumentException("Invalid layer name.");
        return "\"" + name + "\"";
    }

    [McpServerTool, Description("Read precise geometry, layer, color and style properties for one entity ID from describe_drawing.")]
    public async Task<string> GetEntityProperties(string entityId) => await Command($"PROPERTIES {entityId}");

    [McpServerTool, Description("Read visibility, color, active status and entity count for an existing layer.")]
    public async Task<string> GetLayerProperties(string name) => await Command($"PROPERTIES LAYER {Q(name)}");

    [McpServerTool, Description("Remove the part of a line between two points on it. The original line is replaced by up to two lines; use describe_drawing to get its ID and endpoints.")]
    public async Task<string> BreakLine(string lineId, double x1, double y1, double x2, double y2) =>
        await Command($"BREAK {lineId} {Point(x1, y1)} {Point(x2, y2)}");

    [McpServerTool, Description("Trim a line at its intersection with a boundary line. pickX and pickY identify the side to remove and must lie on the line being trimmed.")]
    public async Task<string> TrimLine(string lineId, string boundaryId, double pickX, double pickY) =>
        await Command($"TRIM {lineId} {boundaryId} {Point(pickX, pickY)}");

    [McpServerTool, Description("Extend the START or END of a line until it reaches a boundary line segment.")]
    public async Task<string> ExtendLine(string lineId, string boundaryId, string endpoint) =>
        await Command($"EXTEND {lineId} {boundaryId} {endpoint}");

    [McpServerTool, Description("Create a parallel line or concentric circle. A line requires a positive distance and offsets LEFT by default; specify RIGHT for the other side. For a circle, a signed distance changes the radius.")]
    public async Task<string> OffsetEntity(string entityId, double distance, string? side = null) =>
        await Command(side == null ? $"OFFSET {entityId} {N(distance)}" : $"OFFSET {entityId} {N(distance)} {side}");

    [McpServerTool, Description("Move an existing entity by dx,dy drawing units. Its ID and layer are preserved.")]
    public async Task<string> MoveEntity(string entityId, double dx, double dy) =>
        await Command($"MOVE {entityId} {Point(dx, dy)}");

    [McpServerTool, Description("Copy an existing entity by dx,dy drawing units. The copy receives a new ID and keeps the source layer and style.")]
    public async Task<string> CopyEntity(string entityId, double dx, double dy) =>
        await Command($"COPY {entityId} {Point(dx, dy)}");

    [McpServerTool, Description("Rotate an existing entity around centerX,centerY by angleDegrees. Positive angles are counterclockwise.")]
    public async Task<string> RotateEntity(string entityId, double centerX, double centerY, double angleDegrees) =>
        await Command($"ROTATE {entityId} {Point(centerX, centerY)} {N(angleDegrees)}");

    [McpServerTool, Description("Mirror an existing entity across the axis through x1,y1 and x2,y2. The axis points must differ.")]
    public async Task<string> MirrorEntity(string entityId, double x1, double y1, double x2, double y2) =>
        await Command($"MIRROR {entityId} {Point(x1, y1)} {Point(x2, y2)}");

    [McpServerTool, Description("Fit all visible drawing entities in the CAD viewport. Does not change geometry.")]
    public async Task<string> ZoomExtents() => await Command("ZOOM EXTENTS");

    [McpServerTool, Description("Save the open drawing to a native .cad file, preserving IDs, layers, geometry and preferences. An explicit absolute path is recommended.")]
    public async Task<string> SaveDrawing(string path) => await Command($"SAVE \"{path}\"");

    [McpServerTool, Description("Open a .cad or .dxf file in the desktop app. If there are unsaved changes, save first or explicitly set discardUnsaved to true.")]
    public async Task<string> OpenDrawing(string path, bool discardUnsaved = false) =>
        await Command($"OPEN {(discardUnsaved ? "DISCARD " : "")}\"{path}\"");

    [McpServerTool, Description("Create a new empty drawing. If there are unsaved changes, save first or explicitly set discardUnsaved to true.")]
    public async Task<string> NewDrawing(bool discardUnsaved = false) =>
        await Command(discardUnsaved ? "NEW DISCARD" : "NEW");

    [McpServerTool, Description("Execute one CAD console command on the open drawing. Use HELP to discover syntax; changes appear in the app and undo history.")]
    public async Task<string> ExecuteCadCommand(string command) => await Command(command);

    [McpServerTool, Description("Return SVG markup for the drawing currently open in the CAD application.")]
    public async Task<string> ExportSvg() => await Content("export_svg");

    [McpServerTool, Description("Return ASCII DXF content for the drawing currently open in the CAD application.")]
    public async Task<string> ExportDxf() => await Content("export_dxf");

    private async Task<string> Command(string text)
    {
        var response = await bridge.SendAsync("command", text);
        return response.Success ? response.Message : throw new InvalidOperationException(response.Message);
    }

    private async Task<string> Content(string method)
    {
        var response = await bridge.SendAsync(method);
        return response.Success
            ? response.Content ?? response.Message
            : throw new InvalidOperationException(response.Message);
    }

    private async Task<string> SendStructured(CadMcpRequest request)
    {
        var response = await bridge.SendAsync(request);
        if (!response.Success)
        {
            var fixSuffix = string.IsNullOrWhiteSpace(response.SuggestedFix) ? "" : $" Suggested fix: {response.SuggestedFix}";
            throw new InvalidOperationException($"[{response.Code}] {response.Message}{fixSuffix}");
        }
        return response.Content ?? response.Message;
    }

    private static string N(double number) => number.ToString("R", CultureInfo.InvariantCulture);
    private static string Point(double x, double y) => $"{N(x)};{N(y)}";
}

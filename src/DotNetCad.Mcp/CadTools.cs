using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol.Server;

namespace DotNetCad.Mcp;

[McpServerToolType]
public sealed class CadTools(CadMcpBridgeClient bridge)
{
    [McpServerTool, Description("Describe the drawing currently open in DotNetCad.Demo, including entity IDs, types, layers and bounds.")]
    public async Task<string> DescribeDrawing() => await Content("describe");

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

    [McpServerTool, Description("Select an existing visible layer or create a new layer for subsequent CAD entities. Names cannot contain spaces.")]
    public async Task<string> SetLayer(string name) => await Command($"LAYER {name}");

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

    private static string N(double number) => number.ToString("R", CultureInfo.InvariantCulture);
    private static string Point(double x, double y) => $"{N(x)};{N(y)}";
}

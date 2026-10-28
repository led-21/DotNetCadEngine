using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;
using DotNetCad.Core.Entities;
using DotNetCad.Demo.ViewModels;
using DotNetCad.Infrastructure.Dxf;
using DotNetCad.Infrastructure.Mcp;
using DotNetCad.Infrastructure.Svg;

namespace DotNetCad.Demo.Mcp;

/// <summary>Accepts local MCP bridge requests and applies them on the WPF thread.</summary>
public sealed class CadMcpPipeHost : IDisposable
{
    private readonly MainViewModel _viewModel;
    private readonly Dispatcher _dispatcher;
    private readonly CancellationTokenSource _stop = new();
    private Task? _listener;

    public CadMcpPipeHost(MainViewModel viewModel, Dispatcher dispatcher)
    {
        _viewModel = viewModel;
        _dispatcher = dispatcher;
    }

    public void Start() => _listener = Task.Run(ListenAsync);

    private async Task ListenAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    CadMcpProtocol.PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(_stop.Token);
                using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
                await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                var line = await reader.ReadLineAsync(_stop.Token);
                if (line == null) continue;

                CadMcpResponse response;
                try
                {
                    var request = JsonSerializer.Deserialize<CadMcpRequest>(line)
                        ?? throw new JsonException("Empty request.");
                    response = await _dispatcher.InvokeAsync(() => Handle(request), DispatcherPriority.Background);
                }
                catch (Exception ex) when (ex is JsonException or ArgumentException)
                {
                    response = new CadMcpResponse(false, ex.Message);
                }
                await writer.WriteLineAsync(JsonSerializer.Serialize(response));
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                break;
            }
            catch (IOException)
            {
                // A client disconnected, or another app instance owns the pipe.
                await Task.Delay(500, _stop.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _dispatcher.InvokeAsync(() => _viewModel.AppendConsole($"MCP: {ex.Message}"));
                await Task.Delay(500, _stop.Token).ConfigureAwait(false);
            }
        }
    }

    private CadMcpResponse Handle(CadMcpRequest request)
    {
        switch (request.Method.ToLowerInvariant())
        {
            case "command":
            {
                var result = _viewModel.RunCadCommand(request.Command ?? string.Empty, fromMcp: true);
                return new CadMcpResponse(result.Success, result.Message);
            }
            case "describe":
            {
                var doc = _viewModel.Document;
                var content = JsonSerializer.Serialize(new
                {
                    doc.Title,
                    doc.ActiveLayer,
                    Layers = doc.Layers.Select(l => new { l.Name, l.ColorHex, l.IsVisible, l.IsLocked, l.IsProtected, EntityCount=doc.Entities.Count(e=>e.Layer.Equals(l.Name,StringComparison.OrdinalIgnoreCase)) }),
                    Entities = doc.Entities.Select(e => new
                    {
                        e.Id,
                        e.IsSelected,
                        Type = e.EntityType.ToString(),
                        e.Layer,
                        e.ColorHex,
                        StartPoint = (e as CadLine)?.StartPoint,
                        EndPoint = (e as CadLine)?.EndPoint,
                        Center = (e as CadCircle)?.Center,
                        Radius = (e as CadCircle)?.Radius,
                        Text = (e as CadText)?.Text,
                        Position = (e as CadText)?.Position,
                        Height = (e as CadText)?.Height,
                        MeasureStart = (e as CadDimension)?.StartPoint,
                        MeasureEnd = (e as CadDimension)?.EndPoint,
                        DimensionOffset = (e as CadDimension)?.OffsetPoint,
                        DimensionText = (e as CadDimension)?.FormattedText,
                        Bounds = e.BoundingBox
                    })
                });
                return new CadMcpResponse(true, $"{doc.Entities.Count} entities.", content);
            }
            case "export_svg":
                return new CadMcpResponse(true, "SVG content follows.", SvgCadExporter.ExportToSvg(_viewModel.Document));
            case "export_dxf":
                return new CadMcpResponse(true, "DXF content follows.", DxfCadExporter.ExportToDxf(_viewModel.Document));
            default:
                return new CadMcpResponse(false, "Unknown MCP bridge method.");
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }
}

using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;
using DotNetCad.Core.Audit;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Core.Queries;
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
                    response = new CadMcpResponse(false, ex.Message, Code: "INVALID_REQUEST");
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

    public CadMcpResponse Handle(CadMcpRequest request)
    {
        switch (request.Method.ToLowerInvariant())
        {
            case "command":
            {
                var cmdText = request.Command ?? string.Empty;
                var result = _viewModel.RunCadCommand(cmdText, fromMcp: true);
                var code = result.Success ? "OK" : CadBatchProcessor.ClassifyErrorCode(result.Message);
                var fix = result.Success ? null : CadBatchProcessor.DiagnoseSuggestedFix(cmdText, result.Message);
                return new CadMcpResponse(result.Success, result.Message, Code: code, SuggestedFix: fix);
            }
            case "execute_batch":
            {
                var commands = request.Commands ?? [];
                var label = string.IsNullOrWhiteSpace(request.Summary) ? "Operação em Lote MCP" : request.Summary!;
                var agent = string.IsNullOrWhiteSpace(request.AgentIdentifier) ? "DotNetCad.Mcp" : request.AgentIdentifier!;
                var batchRes = CadBatchProcessor.ExecuteBatch(
                    _viewModel.Document,
                    _viewModel.UndoManager,
                    commands,
                    label,
                    agent,
                    recordAudit: true,
                    actorType: CadAuditActorType.McpAgent);

                _viewModel.AppendConsole($"MCP> BATCH ({commands.Count} cmds): {batchRes.Message}");
                if (batchRes.Success)
                {
                    _viewModel.NotifyExternalDocumentMutation(layersChanged: true);
                }

                var jsonPayload = JsonSerializer.Serialize(batchRes);
                return new CadMcpResponse(
                    batchRes.Success,
                    batchRes.Message,
                    Content: jsonPayload,
                    Code: batchRes.Code,
                    SuggestedFix: batchRes.SuggestedFix,
                    TransactionId: batchRes.TransactionId,
                    CreatedEntityIds: batchRes.CreatedEntityIds,
                    ModifiedEntityIds: batchRes.ModifiedEntityIds,
                    DeletedEntityIds: batchRes.DeletedEntityIds);
            }
            case "stage_batch":
            {
                var commands = request.Commands ?? [];
                var summary = string.IsNullOrWhiteSpace(request.Summary) ? "Proposta IA" : request.Summary!;
                var agent = string.IsNullOrWhiteSpace(request.AgentIdentifier) ? "DotNetCad.Mcp" : request.AgentIdentifier!;
                var stageRes = CadAiStagingManager.StageBatch(_viewModel.Document, commands, summary, agent);

                _viewModel.AppendConsole($"MCP> STAGE: {stageRes.Message}");
                if (stageRes.Success)
                {
                    _viewModel.NotifyExternalDocumentMutation(layersChanged: false);
                }

                var payload = JsonSerializer.Serialize(new
                {
                    stageRes.Success,
                    stageRes.Code,
                    stageRes.Message,
                    stageRes.ProposalId,
                    AddedCount = stageRes.Proposal?.AddedEntities.Count ?? 0,
                    ModifiedCount = stageRes.Proposal?.ModifiedEntities.Count ?? 0,
                    RemovedCount = stageRes.Proposal?.RemovedEntityIds.Count ?? 0
                });

                return new CadMcpResponse(
                    stageRes.Success,
                    stageRes.Message,
                    Content: payload,
                    Code: stageRes.Code,
                    ProposalId: stageRes.ProposalId);
            }
            case "commit_staged":
            {
                var commitRes = CadAiStagingManager.CommitStaged(_viewModel.Document, _viewModel.UndoManager, request.ProposalId);
                _viewModel.AppendConsole($"MCP> COMMIT: {commitRes.Message}");
                if (commitRes.Success)
                {
                    _viewModel.NotifyExternalDocumentMutation(layersChanged: true);
                }

                var payload = JsonSerializer.Serialize(new
                {
                    commitRes.Success,
                    commitRes.Code,
                    commitRes.Message,
                    commitRes.ProposalId,
                    commitRes.TransactionId,
                    CreatedEntityIds = commitRes.CommitBatchResult?.CreatedEntityIds ?? [],
                    ModifiedEntityIds = commitRes.CommitBatchResult?.ModifiedEntityIds ?? [],
                    DeletedEntityIds = commitRes.CommitBatchResult?.DeletedEntityIds ?? []
                });

                return new CadMcpResponse(
                    commitRes.Success,
                    commitRes.Message,
                    Content: payload,
                    Code: commitRes.Code,
                    ProposalId: commitRes.ProposalId,
                    TransactionId: commitRes.TransactionId,
                    CreatedEntityIds: commitRes.CommitBatchResult?.CreatedEntityIds,
                    ModifiedEntityIds: commitRes.CommitBatchResult?.ModifiedEntityIds,
                    DeletedEntityIds: commitRes.CommitBatchResult?.DeletedEntityIds);
            }
            case "discard_staged":
            {
                var discardRes = CadAiStagingManager.DiscardStaged(_viewModel.Document, request.ProposalId);
                _viewModel.AppendConsole($"MCP> DISCARD: {discardRes.Message}");
                if (discardRes.Success)
                {
                    _viewModel.NotifyExternalDocumentMutation(layersChanged: false);
                }

                var payload = JsonSerializer.Serialize(new
                {
                    discardRes.Success,
                    discardRes.Code,
                    discardRes.Message,
                    discardRes.ProposalId
                });

                return new CadMcpResponse(
                    discardRes.Success,
                    discardRes.Message,
                    Content: payload,
                    Code: discardRes.Code,
                    ProposalId: discardRes.ProposalId);
            }
            case "query_entities":
            {
                BoundingBox2D? window = null;
                if (request.WindowMinX.HasValue && request.WindowMinY.HasValue &&
                    request.WindowMaxX.HasValue && request.WindowMaxY.HasValue)
                {
                    window = BoundingBox2D.FromCorners(
                        new Point2D(request.WindowMinX.Value, request.WindowMinY.Value),
                        new Point2D(request.WindowMaxX.Value, request.WindowMaxY.Value));
                }

                var filter = new CadSemanticFilter
                {
                    Layer = request.Layer,
                    EntityTypes = request.EntityTypes,
                    Window = window,
                    AllowCrossing = request.AllowCrossing,
                    TextContains = request.TextContains,
                    MinLength = request.MinLength,
                    MaxLength = request.MaxLength
                };

                var queryRes = CadSemanticQueryEngine.QueryEntities(_viewModel.Document, filter);
                var json = JsonSerializer.Serialize(queryRes);
                return new CadMcpResponse(true, $"Matched {queryRes.MatchedCount} entities.", json);
            }
            case "find_conflicts":
            {
                var conflicts = CadSemanticQueryEngine.FindSpatialConflicts(
                    _viewModel.Document,
                    request.Layer,
                    request.LayerB,
                    request.MinClearanceMeters);
                var json = JsonSerializer.Serialize(new
                {
                    ConflictCount = conflicts.Count,
                    LayerA = request.Layer,
                    LayerB = request.LayerB,
                    MinClearanceMeters = request.MinClearanceMeters,
                    Conflicts = conflicts
                });
                return new CadMcpResponse(true, $"Found {conflicts.Count} spatial conflict(s).", json);
            }
            case "analyze_topology":
            {
                var topo = CadSemanticQueryEngine.AnalyzeTopology(_viewModel.Document);
                var json = JsonSerializer.Serialize(topo);
                return new CadMcpResponse(true, $"Analyzed {topo.TotalEntities} entities across {topo.TotalLayers} layers.", json);
            }
            case "audit_history":
            {
                var doc = _viewModel.Document;
                var json = JsonSerializer.Serialize(new
                {
                    doc.Title,
                    AuditCount = doc.AiAuditHistory.Count,
                    StagedProposal = doc.StagedProposal == null ? null : new
                    {
                        doc.StagedProposal.ProposalId,
                        doc.StagedProposal.CreatedAtUtc,
                        doc.StagedProposal.Summary,
                        doc.StagedProposal.AgentIdentifier,
                        AddedCount = doc.StagedProposal.AddedEntities.Count,
                        ModifiedCount = doc.StagedProposal.ModifiedEntities.Count,
                        RemovedCount = doc.StagedProposal.RemovedEntityIds.Count
                    },
                    Entries = doc.AiAuditHistory
                });
                return new CadMcpResponse(true, $"{doc.AiAuditHistory.Count} audit entries.", json);
            }
            case "describe":
            {
                var doc = _viewModel.Document;
                var content = JsonSerializer.Serialize(new
                {
                    doc.Title,
                    doc.ActiveLayer,
                    StagedProposal = doc.StagedProposal == null ? null : new
                    {
                        doc.StagedProposal.ProposalId,
                        doc.StagedProposal.Summary,
                        AddedCount = doc.StagedProposal.AddedEntities.Count,
                        ModifiedCount = doc.StagedProposal.ModifiedEntities.Count,
                        RemovedCount = doc.StagedProposal.RemovedEntityIds.Count
                    },
                    AuditEntryCount = doc.AiAuditHistory.Count,
                    Layers = doc.Layers.Select(l => new { l.Name, l.ColorHex, l.IsVisible, l.IsLocked, l.IsProtected, EntityCount = doc.Entities.Count(e => e.Layer.Equals(l.Name, StringComparison.OrdinalIgnoreCase)) }),
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
                return new CadMcpResponse(false, "Unknown MCP bridge method.", Code: "UNKNOWN_METHOD");
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }
}

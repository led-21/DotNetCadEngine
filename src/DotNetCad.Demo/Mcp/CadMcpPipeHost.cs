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
                    response = await _dispatcher.InvokeAsync(() => HandleAsync(request)).Task.Unwrap();
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

    private async Task<CadMcpResponse> HandleAsync(CadMcpRequest request)
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
                    Layers = doc.Layers.Select(l => new { l.Name, l.ColorHex, l.IsVisible }),
                    Entities = doc.Entities.Select(e => new
                    {
                        e.Id,
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
            case "mutua_get_linked_project":
            {
                var projectInfo = new
                {
                    Code = _viewModel.Mutua.LinkedProjectCode,
                    Title = _viewModel.Mutua.LinkedProjectTitle,
                    ProjectId = _viewModel.Mutua.LinkedProjectId,
                    Connection = _viewModel.Mutua.ConnectionStatusText,
                    IsConnected = _viewModel.Mutua.IsConnected,
                    IsAuthenticated = _viewModel.Mutua.IsAuthenticated,
                    User = _viewModel.Mutua.AuthenticatedUserName,
                    Disciplines = new[]
                    {
                        new { Code = "ENG-AGR", Name = "Agronomia / Fluxograma Operacional", CadLayer = "ESTRUTURA", Status = "Assigned", Lead = (string?)"Eng. Carlos Mendes" },
                        new { Code = "ENG-CIV", Name = "Engenharia Civil / Fundações e Moega", CadLayer = "ESTRUTURA", Status = "Assigned", Lead = (string?)"Eng. Marcos Rocha" },
                        new { Code = "ENG-ELE", Name = "Engenharia Elétrica / Subestação e SPDA", CadLayer = "ELETRICA", Status = "Open", Lead = (string?)null }
                    },
                    OpenRequirements = new[]
                    {
                        new
                        {
                            RequirementId = _viewModel.Mutua.ElectricalRequirementId,
                            Title = "Projeto Executivo de Subestação 300 kVA e SPDA para Unidade de Beneficiamento",
                            Specialty = "ENG-ELE",
                            Competency = "SUBESTACAO-MT",
                            MinProficiency = "Senior",
                            EstimatedHonorarium = 14500.00
                        }
                    }
                };
                return new CadMcpResponse(true, $"Linked project: {_viewModel.Mutua.LinkedProjectCode}", JsonSerializer.Serialize(projectInfo));
            }
            case "mutua_inspect_cad_disciplines":
            {
                var doc = _viewModel.Document;
                var layerSummaries = doc.Layers.Select(l => new
                {
                    Layer = l.Name,
                    ColorHex = l.ColorHex,
                    IsVisible = l.IsVisible,
                    EntityCount = doc.Entities.Count(e => e.Layer.Equals(l.Name, StringComparison.OrdinalIgnoreCase))
                }).ToList();

                bool hasEstrutura = doc.Layers.Any(l => l.Name.Equals("ESTRUTURA", StringComparison.OrdinalIgnoreCase));
                bool hasEletrica = doc.Layers.Any(l => l.Name.Equals("ELETRICA", StringComparison.OrdinalIgnoreCase));
                bool hasSpda = doc.Layers.Any(l => l.Name.Equals("SPDA", StringComparison.OrdinalIgnoreCase));
                bool hasDrenagem = doc.Layers.Any(l => l.Name.Equals("DRENAGEM", StringComparison.OrdinalIgnoreCase));

                var gaps = new List<string>();
                if (hasEletrica || hasSpda)
                {
                    gaps.Add("Disciplina ELETRICA/SPDA identificada nas camadas do CAD. O projeto PRJ-2026-001 possui requisito aberto para Engenheiro(a) Eletricista (Subestação 300 kVA e SPDA NBR 5419).");
                }

                var analysis = new
                {
                    DocumentTitle = doc.Title,
                    TotalEntities = doc.Entities.Count,
                    Layers = layerSummaries,
                    DetectedDisciplines = new { Estrutura = hasEstrutura, Eletrica = hasEletrica, Spda = hasSpda, Drenagem = hasDrenagem },
                    CollaborationGaps = gaps
                };
                return new CadMcpResponse(true, $"CAD drawing inspected ({doc.Entities.Count} entities across {doc.Layers.Count} layers).", JsonSerializer.Serialize(analysis));
            }
            case "mutua_find_specialists":
            {
                var matches = await _viewModel.Mutua.FindSpecialistsAsync();
                var content = JsonSerializer.Serialize(matches);
                return new CadMcpResponse(true, $"{matches.Count} specialist(s) found.", content);
            }
            case "mutua_prepare_collaboration_draft":
            {
                // STAGE / DRAFT (Safe human-in-the-loop): Never silent mutation!
                Guid recipientId = Guid.Empty;
                string recipientName = "Eng. Helena Vasconcelos";
                string message = "Olá Eng. Helena, identificamos seu portfólio em subestações agroindustriais e gostaríamos de convidá-la para a disciplina elétrica e SPDA do projeto PRJ-2026-001.";
                decimal honorarium = 14500.00m;

                if (!string.IsNullOrWhiteSpace(request.Command))
                {
                    try
                    {
                        var jsonDoc = JsonDocument.Parse(request.Command);
                        if (jsonDoc.RootElement.TryGetProperty("recipientProfileId", out var rId) && rId.TryGetGuid(out var gid))
                            recipientId = gid;
                        if (jsonDoc.RootElement.TryGetProperty("recipientName", out var rName) && !string.IsNullOrWhiteSpace(rName.GetString()))
                            recipientName = rName.GetString()!;
                        if (jsonDoc.RootElement.TryGetProperty("message", out var rMsg) && !string.IsNullOrWhiteSpace(rMsg.GetString()))
                            message = rMsg.GetString()!;
                        if (jsonDoc.RootElement.TryGetProperty("proposedHonorariumBrl", out var rHon) && rHon.TryGetDecimal(out var honVal))
                            honorarium = honVal;
                    }
                    catch { }
                }

                if (recipientId == Guid.Empty)
                {
                    recipientId = Guid.Parse("22222222-2222-2222-2222-222222222202");
                }

                _viewModel.Mutua.StageCollaborationDraft(recipientId, recipientName, message, honorarium);

                return new CadMcpResponse(true,
                    $"Rascunho de convite para {recipientName} preparado com sucesso no painel do CAD. " +
                    "O usuário deve confirmar o envio clicando em 'Confirmar e Enviar Convite' na interface gráfica.",
                    JsonSerializer.Serialize(new { Staged = true, Recipient = recipientName, Honorarium = honorarium }));
            }
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

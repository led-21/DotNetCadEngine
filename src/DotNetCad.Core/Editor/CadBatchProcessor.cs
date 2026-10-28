using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DotNetCad.Core.Audit;
using DotNetCad.Core.Entities;
using DotNetCad.Core.History;

namespace DotNetCad.Core.Editor;

/// <summary>
/// Structured outcome of executing an atomic batch of CAD commands.
/// </summary>
public sealed record CadBatchExecutionResult
{
    public bool Success { get; init; }
    public string Code { get; init; } = "OK";
    public string Message { get; init; } = string.Empty;
    public Guid TransactionId { get; init; } = Guid.NewGuid();
    public string TransactionLabel { get; init; } = string.Empty;
    public int ExecutedCommandCount { get; init; }
    public int? FailedCommandIndex { get; init; }
    public string? FailedCommandText { get; init; }
    public string? SuggestedFix { get; init; }
    public IReadOnlyList<Guid> CreatedEntityIds { get; init; } = [];
    public IReadOnlyList<Guid> ModifiedEntityIds { get; init; } = [];
    public IReadOnlyList<Guid> DeletedEntityIds { get; init; } = [];
    public IReadOnlyList<string> CommandMessages { get; init; } = [];
}

/// <summary>
/// Executes a sequence of CAD commands as an atomic transaction (All-or-Nothing).
/// Groups all resulting undo actions into a single <see cref="CompositeUndoableAction"/>
/// and records an audit entry in <see cref="CadDocument.AiAuditHistory"/>.
/// </summary>
public static class CadBatchProcessor
{
    public static CadBatchExecutionResult ExecuteBatch(
        CadDocument document,
        UndoRedoManager history,
        IReadOnlyList<string> commands,
        string transactionLabel = "Operação em Lote IA",
        string agentIdentifier = "DotNetCad.Mcp",
        bool recordAudit = true,
        CadAuditActorType actorType = CadAuditActorType.McpAgent)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(commands);

        var nonEmptyCommands = commands
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .ToList();

        if (nonEmptyCommands.Count == 0)
        {
            return new CadBatchExecutionResult
            {
                Success = false,
                Code = "EMPTY_BATCH",
                Message = "Nenhum comando válido informado no lote.",
                SuggestedFix = "Informe ao menos um comando CAD válido na lista de comandos do lote."
            };
        }

        var label = string.IsNullOrWhiteSpace(transactionLabel)
            ? "Operação em Lote IA"
            : transactionLabel.Trim();

        var initialActiveLayer = document.ActiveLayer;
        var initialLayersSnapshot = document.Layers.Select(l => l.Clone()).ToList();
        var initialEntityFingerprints = document.Entities
            .ToDictionary(e => e.Id, ComputeEntityFingerprint);
        var initialEntitiesBackup = document.Entities
            .Select(CadAiStagingManager.CloneWithId)
            .ToList();

        var tempHistory = new UndoRedoManager();
        var commandMessages = new List<string>(nonEmptyCommands.Count);

        for (var i = 0; i < nonEmptyCommands.Count; i++)
        {
            var cmdText = nonEmptyCommands[i];
            CadCommandResult cmdResult;
            try
            {
                cmdResult = CadCommandProcessor.Execute(document, tempHistory, cmdText);
            }
            catch (Exception ex)
            {
                RollbackDocumentState(document, tempHistory, initialLayersSnapshot, initialActiveLayer, initialEntitiesBackup);
                return new CadBatchExecutionResult
                {
                    Success = false,
                    Code = "BATCH_COMMAND_EXCEPTION",
                    Message = $"Erro no comando #{i + 1} ('{cmdText}'): {ex.Message}. Lote revertido integralmente.",
                    TransactionLabel = label,
                    ExecutedCommandCount = 0,
                    FailedCommandIndex = i,
                    FailedCommandText = cmdText,
                    SuggestedFix = DiagnoseSuggestedFix(cmdText, ex.Message)
                };
            }

            if (!cmdResult.Success)
            {
                RollbackDocumentState(document, tempHistory, initialLayersSnapshot, initialActiveLayer, initialEntitiesBackup);
                return new CadBatchExecutionResult
                {
                    Success = false,
                    Code = ClassifyErrorCode(cmdResult.Message),
                    Message = $"Falha no comando #{i + 1} ('{cmdText}'): {cmdResult.Message}. Lote revertido integralmente (rollback atômico).",
                    TransactionLabel = label,
                    ExecutedCommandCount = 0,
                    FailedCommandIndex = i,
                    FailedCommandText = cmdText,
                    SuggestedFix = DiagnoseSuggestedFix(cmdText, cmdResult.Message)
                };
            }

            commandMessages.Add(cmdResult.Message);
        }

        // Group all undoable actions recorded in tempHistory into a single CompositeUndoableAction
        var recordedActions = tempHistory.UndoActions;
        if (recordedActions.Count > 0)
        {
            var composite = new CompositeUndoableAction(
                recordedActions,
                $"[Lote IA] {label} ({nonEmptyCommands.Count} cmd)");
            history.RecordExecutedAction(composite);
        }

        var currentEntitiesById = document.Entities.ToDictionary(e => e.Id);
        var createdIds = new List<Guid>();
        var modifiedIds = new List<Guid>();
        var deletedIds = new List<Guid>();

        foreach (var (id, entity) in currentEntitiesById)
        {
            if (!initialEntityFingerprints.TryGetValue(id, out var oldFingerprint))
            {
                createdIds.Add(id);
            }
            else
            {
                var newFingerprint = ComputeEntityFingerprint(entity);
                if (!string.Equals(oldFingerprint, newFingerprint, StringComparison.Ordinal))
                {
                    modifiedIds.Add(id);
                }
            }
        }

        foreach (var oldId in initialEntityFingerprints.Keys)
        {
            if (!currentEntitiesById.ContainsKey(oldId))
            {
                deletedIds.Add(oldId);
            }
        }

        var transactionId = Guid.NewGuid();

        if (recordAudit)
        {
            document.AiAuditHistory.Add(new CadAiAuditEntry
            {
                AuditId = Guid.NewGuid(),
                TimestampUtc = DateTimeOffset.UtcNow,
                ActorType = actorType,
                ActorIdentifier = string.IsNullOrWhiteSpace(agentIdentifier) ? "DotNetCad.Mcp" : agentIdentifier,
                OperationName = "BATCH",
                TransactionId = transactionId,
                Summary = $"{label} ({nonEmptyCommands.Count} comandos: +{createdIds.Count} / ~{modifiedIds.Count} / -{deletedIds.Count})",
                CreatedEntityIds = createdIds,
                ModifiedEntityIds = modifiedIds,
                DeletedEntityIds = deletedIds
            });
        }

        return new CadBatchExecutionResult
        {
            Success = true,
            Code = "OK",
            Message = $"Lote '{label}' executado com sucesso ({nonEmptyCommands.Count} comandos: {createdIds.Count} criadas, {modifiedIds.Count} modificadas, {deletedIds.Count} removidas).",
            TransactionId = transactionId,
            TransactionLabel = label,
            ExecutedCommandCount = nonEmptyCommands.Count,
            CreatedEntityIds = createdIds,
            ModifiedEntityIds = modifiedIds,
            DeletedEntityIds = deletedIds,
            CommandMessages = commandMessages
        };
    }

    private static void RollbackDocumentState(
        CadDocument document,
        UndoRedoManager tempHistory,
        List<CadLayer> initialLayersSnapshot,
        string initialActiveLayer,
        List<ICadEntity> initialEntitiesBackup)
    {
        while (tempHistory.CanUndo)
        {
            tempHistory.Undo();
        }

        document.Layers.Clear();
        foreach (var layer in initialLayersSnapshot)
        {
            document.Layers.Add(layer.Clone());
        }

        document.ActiveLayer = initialActiveLayer;

        if (document.Entities.Count != initialEntitiesBackup.Count ||
            !document.Entities.Select(e => e.Id).SequenceEqual(initialEntitiesBackup.Select(e => e.Id)))
        {
            document.Entities.Clear();
            foreach (var backupEntity in initialEntitiesBackup)
            {
                document.Entities.Add(CadAiStagingManager.CloneWithId(backupEntity));
            }
        }
    }

    public static string ClassifyErrorCode(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "COMMAND_FAILED";
        var upper = message.ToUpperInvariant();
        if (upper.Contains("BLOQUEAD") || upper.Contains("LOCK")) return "LAYER_LOCKED";
        if (upper.Contains("OCULT") || upper.Contains("VISIV") || upper.Contains("HIDDEN")) return "LAYER_HIDDEN";
        if (upper.Contains("NÃO ENCONTRAD") || upper.Contains("NAO ENCONTRAD") || upper.Contains("NOT FOUND")) return "ENTITY_NOT_FOUND";
        if (upper.Contains("DEGENERAD") || upper.Contains("ZERO") || upper.Contains("INVÁLID") || upper.Contains("INVALID") || upper.Contains("MUST BE")) return "GEOMETRY_DEGENERATE";
        if (upper.Contains("DESCONHECID") || upper.Contains("UNKNOWN")) return "UNKNOWN_COMMAND";
        return "COMMAND_FAILED";
    }

    public static string DiagnoseSuggestedFix(string commandText, string errorMessage)
    {
        var code = ClassifyErrorCode(errorMessage);
        return code switch
        {
            "LAYER_LOCKED" => "Desbloqueie a layer alvo com 'LAYER UNLOCK <nome>' ou selecione outra layer ativa antes de modificar entidades.",
            "LAYER_HIDDEN" => "Torne a layer visível com 'LAYER ON <nome>' antes de desenhar ou modificar entidades nela.",
            "ENTITY_NOT_FOUND" => "Use a ferramenta 'query_entities' ou 'describe_drawing' para obter IDs de entidades válidos existentes no documento.",
            "GEOMETRY_DEGENERATE" => "Verifique se comprimentos, raios e distâncias são maiores que a tolerância geométrica mínima (MinEntityDimension).",
            "UNKNOWN_COMMAND" => $"Consulte a sintaxe de comandos CAD suportados. Comando rejeitado: '{commandText}'.",
            _ => "Verifique os parâmetros numéricos (em formato InvariantCulture) e os IDs/layers informados no comando."
        };
    }

    internal static string ComputeEntityFingerprint(ICadEntity entity)
    {
        var sb = new StringBuilder(128);
        var inv = CultureInfo.InvariantCulture;
        sb.Append(entity.EntityType).Append('|')
          .Append(entity.Layer).Append('|')
          .Append(entity.ColorHex).Append('|')
          .Append(entity.LineThickness.ToString("R", inv)).Append('|');

        switch (entity)
        {
            case CadLine line:
                sb.AppendFormat(inv, "{0:R},{1:R}->{2:R},{3:R}", line.StartPoint.X, line.StartPoint.Y, line.EndPoint.X, line.EndPoint.Y);
                break;
            case CadCircle circle:
                sb.AppendFormat(inv, "{0:R},{1:R},R={2:R}", circle.Center.X, circle.Center.Y, circle.Radius);
                break;
            case CadArc arc:
                sb.AppendFormat(inv, "{0:R},{1:R},R={2:R},A={3:R}..{4:R}", arc.Center.X, arc.Center.Y, arc.Radius, arc.StartAngleDegrees, arc.EndAngleDegrees);
                break;
            case CadRectangle rect:
                sb.AppendFormat(inv, "{0:R},{1:R},W={2:R},H={3:R},Rot={4:R}", rect.Origin.X, rect.Origin.Y, rect.Width, rect.Height, rect.RotationDegrees);
                break;
            case CadPolyline poly:
                sb.Append(poly.IsClosed ? "C:" : "O:");
                foreach (var pt in poly.Vertices)
                {
                    sb.AppendFormat(inv, "({0:R},{1:R})", pt.X, pt.Y);
                }
                break;
            case CadText txt:
                sb.AppendFormat(inv, "{0:R},{1:R},H={2:R},R={3:R},T={4}", txt.Position.X, txt.Position.Y, txt.Height, txt.RotationDegrees, txt.Text);
                break;
            case CadDimension dim:
                sb.AppendFormat(inv, "{0:R},{1:R}->{2:R},{3:R}@{4:R},{5:R},T={6}",
                    dim.StartPoint.X, dim.StartPoint.Y,
                    dim.EndPoint.X, dim.EndPoint.Y,
                    dim.OffsetPoint.X, dim.OffsetPoint.Y,
                    dim.CustomTextOverride ?? string.Empty);
                break;
        }

        return sb.ToString();
    }
}

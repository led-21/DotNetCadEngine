using System;
using System.Collections.Generic;
using System.Linq;
using DotNetCad.Core.Entities;
using DotNetCad.Core.History;

namespace DotNetCad.Core.Editor;

/// <summary>
/// Represents a sandboxed AI batch proposal waiting for visual confirmation (COMMIT) or rejection (DISCARD).
/// </summary>
public sealed record CadStagedProposal
{
    public Guid ProposalId { get; init; } = Guid.NewGuid();
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string Summary { get; init; } = string.Empty;
    public string AgentIdentifier { get; init; } = "DotNetCad.Mcp";
    public IReadOnlyList<string> Commands { get; init; } = [];
    public IReadOnlyList<ICadEntity> AddedEntities { get; init; } = [];
    public IReadOnlyList<ICadEntity> ModifiedEntities { get; init; } = [];
    public IReadOnlyList<Guid> RemovedEntityIds { get; init; } = [];
}

/// <summary>
/// Result of staging, committing, or discarding an AI proposal.
/// </summary>
public sealed record CadStagingOperationResult
{
    public bool Success { get; init; }
    public string Code { get; init; } = "OK";
    public string Message { get; init; } = string.Empty;
    public Guid? ProposalId { get; init; }
    public Guid? TransactionId { get; init; }
    public CadStagedProposal? Proposal { get; init; }
    public CadBatchExecutionResult? CommitBatchResult { get; init; }
}

/// <summary>
/// Manages preview staging of AI batch commands in an isolated clone of <see cref="CadDocument"/>
/// before committing them to the live engineering drawing.
/// </summary>
public static class CadAiStagingManager
{
    /// <summary>
    /// Executes a batch of commands against a sandboxed clone of <paramref name="document"/>.
    /// If all commands succeed, stores the resulting diff in <see cref="CadDocument.StagedProposal"/>
    /// without mutating the live entities or layers of <paramref name="document"/>.
    /// </summary>
    public static CadStagingOperationResult StageBatch(
        CadDocument document,
        IReadOnlyList<string> commands,
        string summary,
        string agentIdentifier = "DotNetCad.Mcp")
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(commands);

        if (commands.Count == 0)
        {
            return new CadStagingOperationResult
            {
                Success = false,
                Code = "EMPTY_BATCH",
                Message = "Nenhum comando informado para staging da proposta IA."
            };
        }

        var normalizedLabel = string.IsNullOrWhiteSpace(summary)
            ? $"Proposta IA ({commands.Count} comando(s))"
            : summary.Trim();

        var sandbox = CloneDocumentWithExactIds(document);
        var sandboxHistory = new UndoRedoManager();

        var batchResult = CadBatchProcessor.ExecuteBatch(
            sandbox,
            sandboxHistory,
            commands,
            normalizedLabel,
            agentIdentifier,
            recordAudit: false);

        if (!batchResult.Success)
        {
            return new CadStagingOperationResult
            {
                Success = false,
                Code = batchResult.Code,
                Message = $"Falha ao simular proposta IA no sandbox: {batchResult.Message}",
                CommitBatchResult = batchResult
            };
        }

        var createdSet = batchResult.CreatedEntityIds.ToHashSet();
        var modifiedSet = batchResult.ModifiedEntityIds.ToHashSet();

        var addedEntities = sandbox.Entities
            .Where(e => createdSet.Contains(e.Id))
            .Select(CloneWithId)
            .ToList();

        var modifiedEntities = sandbox.Entities
            .Where(e => modifiedSet.Contains(e.Id))
            .Select(CloneWithId)
            .ToList();

        var proposal = new CadStagedProposal
        {
            ProposalId = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Summary = normalizedLabel,
            AgentIdentifier = string.IsNullOrWhiteSpace(agentIdentifier) ? "DotNetCad.Mcp" : agentIdentifier,
            Commands = commands.ToList(),
            AddedEntities = addedEntities,
            ModifiedEntities = modifiedEntities,
            RemovedEntityIds = batchResult.DeletedEntityIds.ToList()
        };

        document.StagedProposal = proposal;

        return new CadStagingOperationResult
        {
            Success = true,
            Code = "STAGED",
            Message = $"Proposta '{normalizedLabel}' preparada em modo preview ({addedEntities.Count} novas, {modifiedEntities.Count} modificadas, {proposal.RemovedEntityIds.Count} removidas). Use COMMIT para aplicar ou DISCARD para rejeitar.",
            ProposalId = proposal.ProposalId,
            Proposal = proposal
        };
    }

    /// <summary>
    /// Commits the currently staged AI proposal on <paramref name="document"/> as an atomic batch transaction.
    /// </summary>
    public static CadStagingOperationResult CommitStaged(
        CadDocument document,
        UndoRedoManager history,
        Guid? expectedProposalId = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(history);

        var proposal = document.StagedProposal;
        if (proposal is null)
        {
            return new CadStagingOperationResult
            {
                Success = false,
                Code = "NO_STAGED_PROPOSAL",
                Message = "Não há proposta da IA pendente para confirmar (COMMIT)."
            };
        }

        if (expectedProposalId.HasValue && expectedProposalId.Value != Guid.Empty && proposal.ProposalId != expectedProposalId.Value)
        {
            return new CadStagingOperationResult
            {
                Success = false,
                Code = "PROPOSAL_ID_MISMATCH",
                Message = $"O ID da proposta informada ({expectedProposalId.Value}) não corresponde à proposta ativa ({proposal.ProposalId}).",
                ProposalId = proposal.ProposalId
            };
        }

        document.StagedProposal = null;

        var commitResult = CadBatchProcessor.ExecuteBatch(
            document,
            history,
            proposal.Commands,
            proposal.Summary,
            proposal.AgentIdentifier,
            recordAudit: true);

        if (!commitResult.Success)
        {
            document.StagedProposal = proposal;
            return new CadStagingOperationResult
            {
                Success = false,
                Code = commitResult.Code,
                Message = $"Falha ao confirmar proposta '{proposal.Summary}': {commitResult.Message}",
                ProposalId = proposal.ProposalId,
                Proposal = proposal,
                CommitBatchResult = commitResult
            };
        }

        return new CadStagingOperationResult
        {
            Success = true,
            Code = "COMMITTED",
            Message = $"Proposta '{proposal.Summary}' confirmada e aplicada com sucesso ({commitResult.CreatedEntityIds.Count} criadas, {commitResult.ModifiedEntityIds.Count} modificadas, {commitResult.DeletedEntityIds.Count} removidas).",
            ProposalId = proposal.ProposalId,
            TransactionId = commitResult.TransactionId,
            CommitBatchResult = commitResult
        };
    }

    /// <summary>
    /// Discards the currently staged AI proposal without altering <paramref name="document"/>.
    /// </summary>
    public static CadStagingOperationResult DiscardStaged(
        CadDocument document,
        Guid? expectedProposalId = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var proposal = document.StagedProposal;
        if (proposal is null)
        {
            return new CadStagingOperationResult
            {
                Success = false,
                Code = "NO_STAGED_PROPOSAL",
                Message = "Não há proposta da IA pendente para descartar (DISCARD)."
            };
        }

        if (expectedProposalId.HasValue && expectedProposalId.Value != Guid.Empty && proposal.ProposalId != expectedProposalId.Value)
        {
            return new CadStagingOperationResult
            {
                Success = false,
                Code = "PROPOSAL_ID_MISMATCH",
                Message = $"O ID da proposta informada ({expectedProposalId.Value}) não corresponde à proposta ativa ({proposal.ProposalId}).",
                ProposalId = proposal.ProposalId
            };
        }

        document.StagedProposal = null;

        return new CadStagingOperationResult
        {
            Success = true,
            Code = "DISCARDED",
            Message = $"Proposta '{proposal.Summary}' ({proposal.ProposalId}) descartada sem alterar o documento.",
            ProposalId = proposal.ProposalId
        };
    }

    internal static CadDocument CloneDocumentWithExactIds(CadDocument source)
    {
        var clone = new CadDocument
        {
            Title = source.Title,
            ActiveLayer = source.ActiveLayer,
            Preferences = source.Preferences.Clone(),
            Layers = source.Layers.Select(l => l.Clone()).ToList(),
            Entities = source.Entities.Select(CloneWithId).ToList()
        };

        return clone;
    }

    internal static ICadEntity CloneWithId(ICadEntity entity)
    {
        var cloned = entity.Clone();
        if (cloned is CadEntity ce)
        {
            ce.Id = entity.Id;
        }
        cloned.IsSelected = entity.IsSelected;
        return cloned;
    }
}

using System;
using System.Linq;
using DotNetCad.Core.Audit;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Core.History;
using DotNetCad.Core.Queries;
using DotNetCad.Infrastructure.Native;
using Xunit;

namespace DotNetCad.Core.Tests;

public sealed class Sprint4AiCadTests
{
    [Fact]
    public void ExecuteBatch_SucceedsAtomically_GroupsIntoSingleUndoStep_AndRecordsAudit()
    {
        var doc = new CadDocument();
        var history = new UndoRedoManager();

        var commands = new[]
        {
            "LAYER CREATE Eletrica",
            "LAYER ACTIVE Eletrica",
            "LINE 0;0 10;0",
            "CIRCLE 5;5 2",
            "RECT 0;0 10;10"
        };

        var result = CadBatchProcessor.ExecuteBatch(
            doc,
            history,
            commands,
            "Traçado Elétrico Base",
            "Gemini-Agent-01");

        Assert.True(result.Success, result.Message);
        Assert.Equal("OK", result.Code);
        Assert.Equal(5, result.ExecutedCommandCount);
        Assert.Equal(3, result.CreatedEntityIds.Count);
        Assert.Equal(3, doc.Entities.Count);
        Assert.Equal("Eletrica", doc.ActiveLayer);

        // Entire 5-command batch must be grouped into a single CompositeUndoableAction
        Assert.Equal(1, history.UndoCount);
        Assert.Single(doc.AiAuditHistory);
        Assert.Equal("Gemini-Agent-01", doc.AiAuditHistory[0].ActorIdentifier);
        Assert.Equal(3, doc.AiAuditHistory[0].CreatedEntityIds.Count);

        // Single Undo reverts all 3 entities + layer creation
        history.Undo();
        Assert.Empty(doc.Entities);
        Assert.Null(doc.GetLayer("Eletrica"));

        // Single Redo restores everything
        history.Redo();
        Assert.Equal(3, doc.Entities.Count);
        Assert.NotNull(doc.GetLayer("Eletrica"));
    }

    [Fact]
    public void ExecuteBatch_RollsBackAllPriorCommands_WhenOneCommandFails()
    {
        var doc = new CadDocument();
        var history = new UndoRedoManager();

        // Add one baseline entity before the batch
        var preLine = new CadLine(new Point2D(0, 0), new Point2D(5, 0));
        doc.AddEntity(preLine);
        var initialActiveLayer = doc.ActiveLayer;

        var commands = new[]
        {
            "LAYER CREATE Drenagem",
            "LAYER ACTIVE Drenagem",
            "LINE 0;10 20;10",
            "CIRCLE 10;10 -5" // Invalid negative radius -> must trigger atomic rollback!
        };

        var result = CadBatchProcessor.ExecuteBatch(
            doc,
            history,
            commands,
            "Lote com Erro",
            "Gemini-Agent-01");

        Assert.False(result.Success);
        Assert.Equal(3, result.FailedCommandIndex);
        Assert.NotNull(result.SuggestedFix);

        // Verify 100% rollback: only preLine remains, Drenagem layer does not exist, active layer restored, undo stack clean
        Assert.Single(doc.Entities);
        Assert.Equal(preLine.Id, doc.Entities[0].Id);
        Assert.Null(doc.GetLayer("Drenagem"));
        Assert.Equal(initialActiveLayer, doc.ActiveLayer);
        Assert.Equal(0, history.UndoCount);
        Assert.Empty(doc.AiAuditHistory);
    }

    [Fact]
    public void StagingWorkflow_StagesInSandboxWithoutMutatingDocument_ThenCommitsOrDiscards()
    {
        var doc = new CadDocument();
        var history = new UndoRedoManager();

        var existingLine = new CadLine(new Point2D(0, 0), new Point2D(10, 0));
        doc.AddEntity(existingLine);

        var stageCommands = new[]
        {
            $"MOVE {existingLine.Id} 0;5",
            "CIRCLE 5;5 3",
            "TEXT 5;5 0.5 SILO-01"
        };

        var stageResult = CadAiStagingManager.StageBatch(doc, stageCommands, "Proposta Layout Silo", "Copilot-CAD");
        Assert.True(stageResult.Success, stageResult.Message);
        Assert.NotNull(doc.StagedProposal);
        Assert.Equal(2, doc.StagedProposal!.AddedEntities.Count);
        Assert.Single(doc.StagedProposal.ModifiedEntities);

        // Live document must remain untouched while in Staged preview!
        Assert.Single(doc.Entities);
        Assert.Equal(new Point2D(0, 0), ((CadLine)doc.Entities[0]).StartPoint);
        Assert.Equal(0, history.UndoCount);

        // Test Discard first
        var discardRes = CadAiStagingManager.DiscardStaged(doc, doc.StagedProposal.ProposalId);
        Assert.True(discardRes.Success);
        Assert.Null(doc.StagedProposal);
        Assert.Single(doc.Entities);

        // Stage again and Commit
        var stageResult2 = CadAiStagingManager.StageBatch(doc, stageCommands, "Proposta Layout Silo V2", "Copilot-CAD");
        Assert.True(stageResult2.Success);

        var commitRes = CadAiStagingManager.CommitStaged(doc, history, stageResult2.ProposalId);
        Assert.True(commitRes.Success, commitRes.Message);
        Assert.Null(doc.StagedProposal);
        Assert.Equal(3, doc.Entities.Count);
        Assert.Equal(new Point2D(0, 5), ((CadLine)doc.Entities.First(e => e.Id == existingLine.Id)).StartPoint);
        Assert.Equal(1, history.UndoCount);
        Assert.Single(doc.AiAuditHistory);
    }

    [Fact]
    public void SemanticQueryEngine_QueriesFilters_DetectsSpatialConflicts_AndAnalyzesTopology()
    {
        var doc = new CadDocument();
        var history = new UndoRedoManager();

        var setupBatch = new[]
        {
            "LAYER CREATE Arquitetura",
            "LAYER ACTIVE Arquitetura",
            "RECT 0;0 20;15",
            "TEXT 10;7 0.5 SALA DE CONTROLE",
            "LAYER CREATE Eletrica",
            "LAYER ACTIVE Eletrica",
            "LINE 0;5 25;5",
            "LAYER CREATE Drenagem",
            "LAYER ACTIVE Drenagem",
            "LINE 12;0 12;15", // Crosses Eletrica line at (12, 5)!
            "LINE 0;5.4 25;5.4" // Parallel to Eletrica line at 0.4m distance (< 0.5m clearance)
        };

        var batchRes = CadBatchProcessor.ExecuteBatch(doc, history, setupBatch, "Setup Multidisciplinar");
        Assert.True(batchRes.Success, batchRes.Message);

        // 1. Semantic Entity Query by Layer
        var queryEletrica = CadSemanticQueryEngine.QueryEntities(doc, new CadSemanticFilter { Layer = "Eletrica" });
        Assert.Equal(1, queryEletrica.MatchedCount);
        Assert.Equal(25.0, queryEletrica.TotalMatchedLengthMeters, 3);

        // 2. Semantic Query by Text substring
        var queryText = CadSemanticQueryEngine.QueryEntities(doc, new CadSemanticFilter { TextContains = "CONTROLE" });
        Assert.Equal(1, queryText.MatchedCount);
        Assert.Equal("SALA DE CONTROLE", queryText.Entities[0].TextContent);

        // 3. Spatial Conflict Detection between Eletrica and Drenagem (with 0.5m minimum clearance)
        var conflicts = CadSemanticQueryEngine.FindSpatialConflicts(doc, "Eletrica", "Drenagem", minClearanceMeters: 0.5);
        Assert.Contains(conflicts, c => c.ConflictType == "INTERSECTION" && Math.Abs(c.Location.X - 12.0) < 1e-3 && Math.Abs(c.Location.Y - 5.0) < 1e-3);
        Assert.Contains(conflicts, c => c.ConflictType == "CLEARANCE_VIOLATION" && c.DistanceMeters < 0.5);

        // 4. Topology & Room Label Analysis
        var topology = CadSemanticQueryEngine.AnalyzeTopology(doc);
        Assert.Equal(5, topology.TotalEntities);
        Assert.Equal(300.0, topology.TotalClosedAreaSquareMeters, 2);
        Assert.Single(topology.EnclosedRegions);
        Assert.Equal(300.0, topology.EnclosedRegions[0].AreaSquareMeters, 2);
        Assert.Contains("SALA DE CONTROLE", topology.EnclosedRegions[0].EnclosedLabels);
    }

    [Fact]
    public void CadDrawingSerializer_PreservesAiAuditHistory_RoundTrip()
    {
        var doc = new CadDocument();
        var history = new UndoRedoManager();

        CadBatchProcessor.ExecuteBatch(
            doc,
            history,
            ["LINE 0;0 10;0", "CIRCLE 5;5 2"],
            "Lote Auditado",
            "Agente-CREA-IA");

        Assert.Single(doc.AiAuditHistory);
        var originalAudit = doc.AiAuditHistory[0];

        var json = CadDrawingSerializer.Serialize(doc);
        var restored = CadDrawingSerializer.Deserialize(json);

        Assert.Single(restored.AiAuditHistory);
        var restoredAudit = restored.AiAuditHistory[0];
        Assert.Equal(originalAudit.AuditId, restoredAudit.AuditId);
        Assert.Equal(originalAudit.ActorIdentifier, restoredAudit.ActorIdentifier);
        Assert.Equal(originalAudit.TransactionId, restoredAudit.TransactionId);
        Assert.Equal(originalAudit.CreatedEntityIds, restoredAudit.CreatedEntityIds);
    }

    [Fact]
    public void ConsoleCommands_BatchStageCommitQueryConflictsTopologyAudit_WorkEndToEnd()
    {
        var doc = new CadDocument();
        var history = new UndoRedoManager();

        var stageRes = CadCommandProcessor.Execute(doc, history, "STAGE \"Galpão Base\" RECT 0;0 10;8 | TEXT 5;4 0.4 GALPAO-A");
        Assert.True(stageRes.Success, stageRes.Message);
        Assert.NotNull(doc.StagedProposal);
        Assert.Empty(doc.Entities);

        var commitRes = CadCommandProcessor.Execute(doc, history, "COMMIT");
        Assert.True(commitRes.Success, commitRes.Message);
        Assert.Null(doc.StagedProposal);
        Assert.Equal(2, doc.Entities.Count);

        var topoRes = CadCommandProcessor.Execute(doc, history, "TOPOLOGY");
        Assert.True(topoRes.Success);
        Assert.Contains("GALPAO-A", topoRes.Message);

        var queryRes = CadCommandProcessor.Execute(doc, history, "QUERY TYPE Rectangle");
        Assert.True(queryRes.Success);
        Assert.Contains("Matched 1/2", queryRes.Message);

        var auditRes = CadCommandProcessor.Execute(doc, history, "AUDIT");
        Assert.True(auditRes.Success);
        Assert.Contains("Galpão Base", auditRes.Message);
    }
}

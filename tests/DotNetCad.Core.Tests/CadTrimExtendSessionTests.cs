using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Core.History;
using Xunit;

namespace DotNetCad.Core.Tests;

public class CadTrimExtendSessionTests
{
    [Fact]
    public void TrimSession_FullInteractiveWorkflow_ExecutesAndPreservesState()
    {
        var doc = new CadDocument();
        var history = new UndoRedoManager();

        var target1 = new CadLine(new(0, 5), new(10, 5));
        var target2 = new CadLine(new(0, 8), new(10, 8));
        var boundary = new CadLine(new(5, 0), new(5, 10));

        doc.AddEntity(target1);
        doc.AddEntity(target2);
        doc.AddEntity(boundary);

        var session = new CadTrimExtendSession(CadToolType.Trim);
        Assert.Equal(0, session.Step);
        Assert.Contains("Selecione os limites", session.Prompt);

        // Step 0: Select boundary
        session.ToggleBoundary(boundary);
        Assert.Single(session.Boundaries);
        Assert.Same(boundary, session.Boundaries[0]);

        // Confirm boundary
        session.ConfirmBoundaries();
        Assert.Equal(1, session.Step);
        Assert.Contains("Clique no segmento que deseja aparar", session.Prompt);

        // Hover over target1 near start (1, 5)
        session.Hover(new(1, 5), doc.Entities, 0.3, doc);
        Assert.NotNull(session.HoverTrimResult);
        var discarded = Assert.IsType<CadLine>(session.HoverTrimResult.DiscardedSegment);
        Assert.Equal(0.0, discarded.StartPoint.X, 3);
        Assert.Equal(5.0, discarded.EndPoint.X, 3);

        // Execute click on target1
        var execResult1 = session.ExecuteClick(new(1, 5), doc.Entities, 0.3, doc, history);
        Assert.True(execResult1.Success);
        Assert.True(execResult1.DrawingChanged);
        Assert.Equal(1, history.UndoCount);

        // Verify tool remains in Step 1 for consecutive trims
        Assert.Equal(1, session.Step);

        // Consecutive trim: cut target2 near end (9, 8)
        session.Hover(new(9, 8), doc.Entities, 0.3, doc);
        Assert.NotNull(session.HoverTrimResult);

        var execResult2 = session.ExecuteClick(new(9, 8), doc.Entities, 0.3, doc, history);
        Assert.True(execResult2.Success);
        Assert.Equal(2, history.UndoCount);

        // Verify remaining target2 is [0..5]
        var remaining2 = Assert.Single(doc.Entities.OfType<CadLine>(), l => Math.Abs(l.StartPoint.Y - 8.0) < 0.1);
        Assert.Equal(0.0, remaining2.StartPoint.X, 3);
        Assert.Equal(5.0, remaining2.EndPoint.X, 3);

        // Test Undo / Redo
        history.Undo();
        Assert.Equal(1, history.UndoCount);
        // target2 restored to [0..10]
        var restored2 = Assert.Single(doc.Entities.OfType<CadLine>(), l => Math.Abs(l.StartPoint.Y - 8.0) < 0.1);
        Assert.Equal(10.0, restored2.EndPoint.X, 3);

        history.Redo();
        Assert.Equal(2, history.UndoCount);
        var reapplied2 = Assert.Single(doc.Entities.OfType<CadLine>(), l => Math.Abs(l.StartPoint.Y - 8.0) < 0.1);
        Assert.Equal(5.0, reapplied2.EndPoint.X, 3);
    }

    [Fact]
    public void ExtendSession_InteractiveWorkflow_ExtendsConsecutiveEntities()
    {
        var doc = new CadDocument();
        var history = new UndoRedoManager();

        var line1 = new CadLine(new(0, 2), new(4, 2));
        var line2 = new CadLine(new(0, 6), new(3, 6));
        var boundary = new CadLine(new(8, 0), new(8, 10));

        doc.AddEntity(line1);
        doc.AddEntity(line2);
        doc.AddEntity(boundary);

        var session = new CadTrimExtendSession(CadToolType.Extend);
        session.ToggleBoundary(boundary);
        session.ConfirmBoundaries();

        // Hover over line1 near end (3.5, 2)
        session.Hover(new(3.5, 2), doc.Entities, 0.3, doc);
        Assert.NotNull(session.HoverExtendResult);
        var extLine1 = Assert.IsType<CadLine>(session.HoverExtendResult.ExtendedEntity);
        Assert.Equal(8.0, extLine1.EndPoint.X, 3);

        // Execute click on line1
        var res1 = session.ExecuteClick(new(3.5, 2), doc.Entities, 0.3, doc, history);
        Assert.True(res1.Success);
        Assert.Equal(1, history.UndoCount);
        Assert.Equal(1, session.Step); // Remains in Step 1

        // Execute click on line2 near end (2.8, 6)
        var res2 = session.ExecuteClick(new(2.8, 6), doc.Entities, 0.3, doc, history);
        Assert.True(res2.Success);
        Assert.Equal(2, history.UndoCount);

        var modifiedLine2 = Assert.Single(doc.Entities.OfType<CadLine>(), l => Math.Abs(l.StartPoint.Y - 6.0) < 0.1);
        Assert.Equal(8.0, modifiedLine2.EndPoint.X, 3);

        // Test Undo
        history.Undo();
        var undoLine2 = Assert.Single(doc.Entities.OfType<CadLine>(), l => Math.Abs(l.StartPoint.Y - 6.0) < 0.1);
        Assert.Equal(3.0, undoLine2.EndPoint.X, 3);
    }

    [Fact]
    public void TrimSession_RejectsLockedLayer()
    {
        var doc = new CadDocument();
        var history = new UndoRedoManager();

        doc.Layers.Add(new CadLayer { Name = "LockedLayer", IsLocked = true });
        var target = new CadLine(new(0, 5), new(10, 5), "LockedLayer");
        var boundary = new CadLine(new(5, 0), new(5, 10));

        doc.AddEntity(target);
        doc.AddEntity(boundary);

        var session = new CadTrimExtendSession(CadToolType.Trim);
        session.ToggleBoundary(boundary);
        session.ConfirmBoundaries();

        // Hover over target on locked layer
        session.Hover(new(2, 5), doc.Entities, 0.3, doc);
        Assert.Null(session.HoverTrimResult);
        Assert.Contains("bloqueada", session.Feedback);

        // Attempt click
        var res = session.ExecuteClick(new(2, 5), doc.Entities, 0.3, doc, history);
        Assert.False(res.Success);
        Assert.Contains("bloqueada", res.Message);
        Assert.Equal(0, history.UndoCount);
        Assert.Equal(new Point2D(0, 5), target.StartPoint);
    }

    [Fact]
    public void ExtendSession_RejectsLockedLayer()
    {
        var doc = new CadDocument();
        var history = new UndoRedoManager();

        doc.Layers.Add(new CadLayer { Name = "LockedLayer", IsLocked = true });
        var target = new CadLine(new(0, 5), new(3, 5), "LockedLayer");
        var boundary = new CadLine(new(6, 0), new(6, 10));

        doc.AddEntity(target);
        doc.AddEntity(boundary);

        var session = new CadTrimExtendSession(CadToolType.Extend);
        session.ToggleBoundary(boundary);
        session.ConfirmBoundaries();

        var res = session.ExecuteClick(new(2.8, 5), doc.Entities, 0.3, doc, history);
        Assert.False(res.Success);
        Assert.Contains("bloqueada", res.Message);
        Assert.Equal(0, history.UndoCount);
    }

    [Fact]
    public void Session_FallbackToAllVisibleEntities_WhenEnterPressedWithoutSelection()
    {
        var doc = new CadDocument();
        var l1 = new CadLine(new(0, 0), new(10, 0));
        var l2 = new CadLine(new(5, -5), new(5, 5));
        doc.AddEntity(l1);
        doc.AddEntity(l2);

        var session = new CadTrimExtendSession(CadToolType.Trim);
        Assert.Empty(session.Boundaries);

        // Pressing Enter with empty boundaries selects all visible entities as boundaries
        session.ConfirmBoundaries(doc.Entities);
        Assert.Equal(2, session.Boundaries.Count);
        Assert.Equal(1, session.Step);
    }

    [Fact]
    public void Session_Cancel_RollsBackStepOrClearsBoundaries()
    {
        var session = new CadTrimExtendSession(CadToolType.Trim);
        var b = new CadLine(new(0, 0), new(1, 1));
        session.ToggleBoundary(b);
        session.ConfirmBoundaries();
        Assert.Equal(1, session.Step);

        // Cancel during Step 1 moves back to Step 0
        session.Cancel();
        Assert.Equal(0, session.Step);

        // Cancel during Step 0 clears boundaries
        session.Cancel();
        Assert.Empty(session.Boundaries);
    }
}

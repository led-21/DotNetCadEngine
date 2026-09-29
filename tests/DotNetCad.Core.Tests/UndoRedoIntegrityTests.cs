using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Core.History;
using DotNetCad.Core.History.Actions;
using Xunit;

namespace DotNetCad.Core.Tests;

public class UndoRedoIntegrityTests
{
    private sealed class ThrowingAction : IUndoableAction
    {
        public string Description => "Action designed to fail";
        public void Execute() => throw new InvalidOperationException("Simulated failure in middle of composite execution");
        public void Undo() { }
    }

    [Fact]
    public void AddAndDeleteEntityAction_UndoAndRedo_MaintainsIntegrity()
    {
        var doc = new CadDocument();
        var undoManager = new UndoRedoManager();
        var line = new CadLine(new Point2D(0, 0), new Point2D(10, 10));

        var addAction = new AddEntityAction(doc, line);
        undoManager.ExecuteAction(addAction);

        Assert.Single(doc.Entities);
        Assert.True(undoManager.CanUndo);
        Assert.False(undoManager.CanRedo);

        undoManager.Undo();
        Assert.Empty(doc.Entities);
        Assert.False(undoManager.CanUndo);
        Assert.True(undoManager.CanRedo);

        undoManager.Redo();
        Assert.Single(doc.Entities);
    }

    [Fact]
    public void DeleteEntityAction_Undo_RestoresOriginalIndex()
    {
        var doc = new CadDocument();
        var undoManager = new UndoRedoManager();

        var l1 = new CadLine(new Point2D(0, 0), new Point2D(1, 1));
        var l2 = new CadLine(new Point2D(2, 2), new Point2D(3, 3));
        var l3 = new CadLine(new Point2D(4, 4), new Point2D(5, 5));

        doc.AddEntity(l1);
        doc.AddEntity(l2);
        doc.AddEntity(l3);

        // Delete l2 (index 1)
        var deleteAction = new DeleteEntityAction(doc, l2);
        undoManager.ExecuteAction(deleteAction);

        Assert.Equal(2, doc.Entities.Count);
        Assert.DoesNotContain(l2, doc.Entities);

        // Undo: should restore l2 exactly at index 1
        undoManager.Undo();
        Assert.Equal(3, doc.Entities.Count);
        Assert.Same(l2, doc.Entities[1]);
    }

    [Fact]
    public void CompositeUndoableAction_WhenInnerActionFails_PerformsFullAtomicRollback()
    {
        var doc = new CadDocument();
        var undoManager = new UndoRedoManager();

        var l1 = new CadLine(new Point2D(0, 0), new Point2D(1, 1));
        var l2 = new CadLine(new Point2D(2, 2), new Point2D(3, 3));

        var act1 = new AddEntityAction(doc, l1);
        var act2 = new ThrowingAction();
        var act3 = new AddEntityAction(doc, l2);

        var composite = new CompositeUndoableAction([act1, act2, act3]);

        Assert.Throws<InvalidOperationException>(() => composite.Execute());

        // Because of reverse atomic rollback, l1 must have been rolled back and document remains empty!
        Assert.Empty(doc.Entities);
    }

    [Fact]
    public void UndoRedoManager_LimitsStackDepthTo100()
    {
        var doc = new CadDocument();
        var undoManager = new UndoRedoManager();

        for (int i = 0; i < 150; i++)
        {
            var line = new CadLine(new Point2D(i, 0), new Point2D(i, 1));
            undoManager.ExecuteAction(new AddEntityAction(doc, line));
        }

        Assert.Equal(100, undoManager.UndoCount);
    }
}

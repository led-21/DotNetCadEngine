using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.History;
using Xunit;

namespace DotNetCad.Core.Tests;

public class CadCommandProcessorTests
{
    [Fact]
    public void CommandsShareDocumentAndUndoHistory()
    {
        var document = new CadDocument();
        var history = new UndoRedoManager();

        Assert.True(CadCommandProcessor.Execute(document, history, "LAYER AI").Success);
        Assert.True(CadCommandProcessor.Execute(document, history, "LINE 0;0 5;0").Success);
        Assert.True(CadCommandProcessor.Execute(document, history, "CIRCLE 2;2 1").Success);
        Assert.Equal(2, document.Entities.Count);
        Assert.All(document.Entities, entity => Assert.Equal("AI", entity.Layer));

        var undo = CadCommandProcessor.Execute(document, history, "UNDO");
        Assert.True(undo.Success);
        Assert.Single(document.Entities);
        Assert.IsType<CadLine>(document.Entities[0]);

        var redo = CadCommandProcessor.Execute(document, history, "REDO");
        Assert.True(redo.Success);
        Assert.Equal(2, document.Entities.Count);
    }

    [Theory]
    [InlineData("CIRCLE 0;0 -1")]
    [InlineData("LINE 0;0 0;0")]
    [InlineData("RECT 0;0 0;2")]
    [InlineData("POLYLINE 0;0 0;0")]
    public void InvalidGeometryDoesNotChangeDrawing(string command)
    {
        var document = new CadDocument();
        var history = new UndoRedoManager();

        var result = CadCommandProcessor.Execute(document, history, command);

        Assert.False(result.Success);
        Assert.Empty(document.Entities);
        Assert.False(history.CanUndo);
    }
}

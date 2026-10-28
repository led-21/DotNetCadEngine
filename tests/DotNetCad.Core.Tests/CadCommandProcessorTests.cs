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

    [Fact]
    public void BreakCreatesDoorGapAndUndoRestoresOriginalLine()
    {
        var document = new CadDocument();
        var history = new UndoRedoManager();
        var original = new CadLine(new(0, 0), new(10, 0));
        document.AddEntity(original);

        var result = CadCommandProcessor.Execute(document, history, $"BREAK {original.Id} 4;0 6;0");

        Assert.True(result.Success);
        var pieces = document.Entities.Cast<CadLine>().ToList();
        Assert.Equal(2, pieces.Count);
        Assert.Equal(4, pieces[0].EndPoint.X);
        Assert.Equal(6, pieces[1].StartPoint.X);
        Assert.True(CadCommandProcessor.Execute(document, history, "UNDO").Success);
        Assert.Same(original, Assert.Single(document.Entities));
        Assert.True(CadCommandProcessor.Execute(document, history, "REDO").Success);
        Assert.Equal(2, document.Entities.Count);
    }

    [Fact]
    public void TrimAndExtendReplaceOnlyTargetLine()
    {
        var document = new CadDocument();
        var history = new UndoRedoManager();
        var target = new CadLine(new(0, 0), new(10, 0));
        var boundary = new CadLine(new(6, -2), new(6, 2));
        document.AddEntity(target);
        document.AddEntity(boundary);

        var trimmed = CadCommandProcessor.Execute(document, history, $"TRIM {target.Id} {boundary.Id} 5.5;0");
        Assert.True(trimmed.Success);
        var remaining = Assert.Single(document.Entities.OfType<CadLine>(), line => line.Id != boundary.Id);
        Assert.Equal(6, remaining.StartPoint.X);
        Assert.Equal(10, remaining.EndPoint.X);
        Assert.True(CadCommandProcessor.Execute(document, history, "UNDO").Success);
        Assert.Same(target, document.Entities[0]);

        var shortened = new CadLine(new(0, 0), new(4, 0));
        document.RemoveEntity(target);
        document.AddEntity(shortened);
        var extended = CadCommandProcessor.Execute(document, history, $"EXTEND {shortened.Id} {boundary.Id} END");
        Assert.True(extended.Success);
        var result = Assert.Single(document.Entities.OfType<CadLine>(), line => line.Id != boundary.Id);
        Assert.Equal(6, result.EndPoint.X);
    }

    [Fact]
    public void OffsetRespectsSideAndRejectsInvalidCircleRadius()
    {
        var document = new CadDocument();
        var history = new UndoRedoManager();
        var line = new CadLine(new(0, 0), new(10, 0));
        var circle = new CadCircle(new(0, 0), 2);
        document.AddEntity(line);
        document.AddEntity(circle);

        Assert.True(CadCommandProcessor.Execute(document, history, $"OFFSET {line.Id} 1 RIGHT").Success);
        var parallel = Assert.Single(document.Entities.OfType<CadLine>(), item => item.Id != line.Id);
        Assert.Equal(-1, parallel.StartPoint.Y);
        Assert.False(CadCommandProcessor.Execute(document, history, $"OFFSET {line.Id} -1").Success);
        Assert.False(CadCommandProcessor.Execute(document, history, $"OFFSET {circle.Id} -3").Success);
        Assert.Single(document.Entities.OfType<CadCircle>());
    }

    [Fact]
    public void TextAndDimensionUseActiveLayerAndUndoHistory()
    {
        var document = new CadDocument();
        var history = new UndoRedoManager();
        Assert.True(CadCommandProcessor.Execute(document, history, "LAYER ROOMS").Success);

        Assert.True(CadCommandProcessor.Execute(document, history, "TEXT 2;3 0.5 Quarto 1").Success);
        var label = Assert.Single(document.Entities.OfType<CadText>());
        Assert.Equal("Quarto 1", label.Text);
        Assert.Equal(0.5, label.Height);
        Assert.Equal("ROOMS", label.Layer);

        Assert.True(CadCommandProcessor.Execute(document, history, "DIM 0;0 4;0 2;-1").Success);
        var dimension = Assert.Single(document.Entities.OfType<CadDimension>());
        Assert.Equal($"{4.0:F2} m", dimension.FormattedText);
        Assert.Equal("ROOMS", dimension.Layer);
        Assert.True(CadCommandProcessor.Execute(document, history, "UNDO").Success);
        Assert.Empty(document.Entities.OfType<CadDimension>());
        Assert.Single(document.Entities.OfType<CadText>());
    }

    [Theory]
    [InlineData("TEXT 1;1 0 Too small")]
    [InlineData("DIM 0;0 0;0 1;1")]
    [InlineData("DIM 0;0 4;0 2;0")]
    public void InvalidAnnotationDoesNotChangeDrawing(string command)
    {
        var document = new CadDocument();
        var history = new UndoRedoManager();
        Assert.False(CadCommandProcessor.Execute(document, history, command).Success);
        Assert.Empty(document.Entities);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void MovePreservesIdentityAndSupportsUndoRedo()
    {
        var document = new CadDocument();
        var history = new UndoRedoManager();
        var line = new CadLine(new(0, 0), new(4, 0), "PAREDES");
        document.AddEntity(line);

        Assert.True(CadCommandProcessor.Execute(document, history, $"MOVE {line.Id} 2;3").Success);
        Assert.Same(line, Assert.Single(document.Entities));
        Assert.Equal(2, line.StartPoint.X);
        Assert.Equal(3, line.StartPoint.Y);
        Assert.Equal("PAREDES", line.Layer);

        Assert.True(CadCommandProcessor.Execute(document, history, "UNDO").Success);
        Assert.Equal(0, line.StartPoint.X);
        Assert.Equal(0, line.StartPoint.Y);
        Assert.True(CadCommandProcessor.Execute(document, history, "REDO").Success);
        Assert.Equal(2, line.StartPoint.X);
    }

    [Fact]
    public void CopyCreatesIndependentEntityWithSourceStyleAndUndoRemovesIt()
    {
        var document = new CadDocument();
        var history = new UndoRedoManager();
        var source = new CadText { Position = new(1, 2), Text = "Quarto", Height = 0.5,
            Layer = "TEXTOS", ColorHex = "#FF0000", LineThickness = 2 };
        document.AddEntity(source);

        Assert.True(CadCommandProcessor.Execute(document, history, $"COPY {source.Id} 3;4").Success);
        var copy = Assert.Single(document.Entities.OfType<CadText>(), item => item.Id != source.Id);
        Assert.Equal(1, source.Position.X);
        Assert.Equal(4, copy.Position.X);
        Assert.Equal(6, copy.Position.Y);
        Assert.Equal(source.Text, copy.Text);
        Assert.Equal(source.Layer, copy.Layer);
        Assert.Equal(source.ColorHex, copy.ColorHex);
        Assert.Equal(source.LineThickness, copy.LineThickness);
        Assert.True(CadCommandProcessor.Execute(document, history, "UNDO").Success);
        Assert.Same(source, Assert.Single(document.Entities));
        Assert.True(CadCommandProcessor.Execute(document, history, "REDO").Success);
        Assert.Contains(document.Entities, item => item.Id == copy.Id);
    }

    [Fact]
    public void RotateAndMirrorPreserveLineIdentityAndAreUndoable()
    {
        var document = new CadDocument();
        var history = new UndoRedoManager();
        var line = new CadLine(new(1, 0), new(2, 0));
        document.AddEntity(line);

        Assert.True(CadCommandProcessor.Execute(document, history, $"ROTATE {line.Id} 0;0 90").Success);
        Assert.Equal(0, line.StartPoint.X, 8);
        Assert.Equal(1, line.StartPoint.Y, 8);
        Assert.True(CadCommandProcessor.Execute(document, history, $"MIRROR {line.Id} 0;0 1;0").Success);
        Assert.Equal(-1, line.StartPoint.Y, 8);
        Assert.True(CadCommandProcessor.Execute(document, history, "UNDO").Success);
        Assert.Equal(1, line.StartPoint.Y, 8);
        Assert.True(CadCommandProcessor.Execute(document, history, "UNDO").Success);
        Assert.Equal(1, line.StartPoint.X, 8);
        Assert.Equal(0, line.StartPoint.Y, 8);
        Assert.Same(line, Assert.Single(document.Entities));
    }

    [Fact]
    public void MirroringRectangleKeepsItsCornersAndUndoRestoresOriginal()
    {
        var document = new CadDocument();
        var history = new UndoRedoManager();
        var rectangle = new CadRectangle(new(1, 1), 2, 3);
        document.AddEntity(rectangle);

        Assert.True(CadCommandProcessor.Execute(document, history, $"MIRROR {rectangle.Id} 0;0 0;1").Success);
        var vertices = rectangle.GetVertices();
        Assert.Contains(vertices, point => Math.Abs(point.X + 1) < 1e-8 && Math.Abs(point.Y - 1) < 1e-8);
        Assert.Contains(vertices, point => Math.Abs(point.X + 3) < 1e-8 && Math.Abs(point.Y - 4) < 1e-8);
        Assert.True(CadCommandProcessor.Execute(document, history, "UNDO").Success);
        Assert.Equal(1, rectangle.Origin.X, 8);
        Assert.Equal(1, rectangle.Origin.Y, 8);
        Assert.Equal(0, rectangle.RotationDegrees, 8);
    }

    [Theory]
    [InlineData("MOVE", "0;0")]
    [InlineData("COPY", "0;0")]
    [InlineData("ROTATE", "0;0 360")]
    [InlineData("MIRROR", "0;0 0;0")]
    [InlineData("MOVE", "not-a-point")]
    public void InvalidTransformLeavesDocumentAndHistoryUntouched(string command, string arguments)
    {
        var document = new CadDocument();
        var history = new UndoRedoManager();
        var line = new CadLine(new(0, 0), new(2, 0));
        document.AddEntity(line);

        Assert.False(CadCommandProcessor.Execute(document, history, $"{command} {line.Id} {arguments}").Success);
        Assert.Same(line, Assert.Single(document.Entities));
        Assert.Equal(0, line.StartPoint.X);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void LayerVisibilityMovesActiveLayerAndUndoRestoresIt()
    {
        var document = new CadDocument();
        var history = new UndoRedoManager();
        var geometry = document.GetLayer("Geometry")!;

        var hidden = CadCommandProcessor.Execute(document, history, "LAYER HIDE Geometry");
        Assert.True(hidden.Success);
        Assert.True(hidden.LayersChanged);
        Assert.False(geometry.IsVisible);
        Assert.Equal("0", document.ActiveLayer);
        Assert.True(CadCommandProcessor.Execute(document, history, "UNDO").Success);
        Assert.True(geometry.IsVisible);
        Assert.Equal("Geometry", document.ActiveLayer);
        Assert.True(CadCommandProcessor.Execute(document, history, "REDO").Success);
        Assert.False(geometry.IsVisible);

        Assert.True(CadCommandProcessor.Execute(document, history, "LAYER SHOW Geometry").Success);
        Assert.True(geometry.IsVisible);
    }

    [Fact]
    public void CannotHideTheLastVisibleLayer()
    {
        var document = new CadDocument();
        var history = new UndoRedoManager();
        foreach (var layer in document.Layers.Where(layer => layer.Name != "Geometry")) layer.IsVisible = false;

        var result = CadCommandProcessor.Execute(document, history, "LAYER HIDE Geometry");

        Assert.False(result.Success);
        Assert.True(document.GetLayer("Geometry")!.IsVisible);
        Assert.Equal("Geometry", document.ActiveLayer);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void LayerColorPreservesAllExplicitEntityColors()
    {
        var document = new CadDocument();
        var history = new UndoRedoManager();
        var color = document.GetLayer("Geometry")!.ColorHex;
        var byLayer = new CadLine(new(0, 0), new(1, 0), colorHex: color);
        var custom = new CadLine(new(0, 1), new(1, 1), colorHex: "#AA0000");
        document.AddEntity(byLayer);
        document.AddEntity(custom);

        Assert.True(CadCommandProcessor.Execute(document, history, "LAYER COLOR Geometry #12ab34").Success);
        Assert.Equal("#12AB34", document.GetLayer("Geometry")!.ColorHex);
        Assert.Equal(color, byLayer.ColorHex);
        Assert.Equal("#AA0000", custom.ColorHex);
        Assert.True(CadCommandProcessor.Execute(document, history, "UNDO").Success);
        Assert.Equal(color, document.GetLayer("Geometry")!.ColorHex);
        Assert.Equal(color, byLayer.ColorHex);
        Assert.True(CadCommandProcessor.Execute(document, history, "REDO").Success);
        Assert.Equal(color, byLayer.ColorHex);
    }

    [Fact]
    public void PropertiesReportsPreciseEntityAndLayerDataWithoutEditing()
    {
        var document = new CadDocument();
        var history = new UndoRedoManager();
        var line = new CadLine(new(1.25, 2), new(4.5, 2));
        document.AddEntity(line);

        var entity = CadCommandProcessor.Execute(document, history, $"PROPERTIES {line.Id}");
        Assert.True(entity.Success);
        Assert.Contains($"ID: {line.Id}", entity.Message);
        Assert.Contains("Start: 1.25;2", entity.Message);
        Assert.Contains("Length: 3.25", entity.Message);
        var layer = CadCommandProcessor.Execute(document, history, "PROPERTIES LAYER Geometry");
        Assert.True(layer.Success);
        Assert.Contains("Entities: 1", layer.Message);
        Assert.Contains("Visible: True", layer.Message);
        Assert.False(history.CanUndo);
    }

    [Theory]
    [InlineData("LAYER COLOR Geometry red")]
    [InlineData("LAYER COLOR Missing #FFFFFF")]
    [InlineData("LAYER HIDE Missing")]
    [InlineData("PROPERTIES not-a-guid")]
    [InlineData("PROPERTIES LAYER Missing")]
    public void InvalidLayerOrPropertiesCommandDoesNotChangeHistory(string command)
    {
        var document = new CadDocument();
        var history = new UndoRedoManager();
        Assert.False(CadCommandProcessor.Execute(document, history, command).Success);
        Assert.False(history.CanUndo);
    }

    [Theory]
    [InlineData("BREAK", "11;0 12;0")]
    [InlineData("BREAK", "4;1 6;0")]
    [InlineData("TRIM", "not-a-guid 1;0")]
    public void InvalidEditLeavesDocumentAndHistoryUntouched(string command, string arguments)
    {
        var document = new CadDocument();
        var history = new UndoRedoManager();
        var line = new CadLine(new(0, 0), new(10, 0));
        document.AddEntity(line);

        var result = CadCommandProcessor.Execute(document, history, $"{command} {line.Id} {arguments}");

        Assert.False(result.Success);
        Assert.Same(line, Assert.Single(document.Entities));
        Assert.False(history.CanUndo);
    }
}

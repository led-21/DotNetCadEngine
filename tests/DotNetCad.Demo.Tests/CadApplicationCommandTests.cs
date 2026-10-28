using System.IO;
using System.Threading;
using DotNetCad.Core.Entities;
using DotNetCad.Demo.ViewModels;
using Xunit;

namespace DotNetCad.Demo.Tests;

public class CadApplicationCommandTests
{
    [Fact]
    public void SaveOpenNewAndZoomCommandsProtectAndRestoreDrawing()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            var path = Path.Combine(Path.GetDirectoryName(typeof(CadApplicationCommandTests).Assembly.Location)!,
                $"dotnetcad-{Guid.NewGuid():N}.cad");
            try
            {
                var viewModel = new MainViewModel();
                Assert.NotNull(viewModel.SaveDrawingCommand);
                Assert.NotNull(viewModel.OpenDrawingCommand);
                viewModel.NewDrawingCommand.Execute(null);
                Assert.Empty(viewModel.Document.Entities);
                Assert.True(viewModel.RunCadCommand("LAYER PAREDES").Success);
                Assert.True(viewModel.RunCadCommand("LAYER COLOR PAREDES #123456").Success);
                Assert.Equal("#123456", viewModel.Layers.Single(layer => layer.Name == "PAREDES").ColorHex);
                viewModel.UndoCommand.Execute(null);
                Assert.Equal("#38BDF8", viewModel.Layers.Single(layer => layer.Name == "PAREDES").ColorHex);
                viewModel.RedoCommand.Execute(null);
                Assert.Equal("#123456", viewModel.Layers.Single(layer => layer.Name == "PAREDES").ColorHex);
                Assert.True(viewModel.RunCadCommand("LINE 0;0 4;0").Success);
                var line = Assert.IsType<CadLine>(Assert.Single(viewModel.Document.Entities));
                var id = line.Id;
                Assert.True(viewModel.RunCadCommand($"SAVE \"{path}\"").Success);
                Assert.True(File.Exists(path));

                Assert.True(viewModel.RunCadCommand($"MOVE {id} 2;0").Success);
                Assert.False(viewModel.RunCadCommand($"OPEN \"{path}\"").Success);
                Assert.Equal(2, line.StartPoint.X);
                Assert.True(viewModel.RunCadCommand($"OPEN DISCARD \"{path}\"").Success);
                var restored = Assert.IsType<CadLine>(Assert.Single(viewModel.Document.Entities));
                Assert.Equal(id, restored.Id);
                Assert.Equal(0, restored.StartPoint.X);
                Assert.Equal("PAREDES", viewModel.Document.ActiveLayer);

                File.WriteAllText(path, "{ invalid drawing");
                Assert.False(viewModel.RunCadCommand($"OPEN DISCARD \"{path}\"").Success);
                Assert.Same(restored, Assert.Single(viewModel.Document.Entities));
                viewModel.SaveDrawingCommand.Execute(null);
                Assert.Contains("\"Version\": 1", File.ReadAllText(path));

                var zoomCalled = false;
                viewModel.ZoomExtentsHandler = () => { zoomCalled = true; return true; };
                Assert.True(viewModel.RunCadCommand("ZOOM EXTENTS").Success);
                Assert.True(zoomCalled);
                viewModel.NewDrawingCommand.Execute(null);
                Assert.Empty(viewModel.Document.Entities);
                Assert.True(viewModel.RunCadCommand("LINE 0;0 1;0").Success);
                Assert.False(viewModel.RunCadCommand("NEW").Success);
                Assert.Single(viewModel.Document.Entities);
                Assert.True(viewModel.RunCadCommand("NEW DISCARD").Success);
                Assert.Empty(viewModel.Document.Entities);
            }
            catch (Exception ex) { error = ex; }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) throw error;
    }

    [Fact]
    public void NewToolbarAndLayerCommandsFunctionCorrectly()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var viewModel = new MainViewModel();
                
                // Test ZoomExtentsCommand and ResetViewCommand
                var zoomCalled = false;
                var resetCalled = false;
                viewModel.ZoomExtentsHandler = () => { zoomCalled = true; return true; };
                viewModel.ResetViewHandler = () => { resetCalled = true; };

                viewModel.ZoomExtentsCommand.Execute(null);
                Assert.True(zoomCalled);

                viewModel.ResetViewCommand.Execute(null);
                Assert.True(resetCalled);

                // Test AddLayerCommand and ShowAllLayersCommand
                viewModel.NewLayerInput = "ELETRICA";
                viewModel.AddLayerCommand.Execute(null);
                Assert.Contains(viewModel.Layers, l => l.Name == "ELETRICA");
                Assert.Equal(string.Empty, viewModel.NewLayerInput);

                var layer = viewModel.Layers.First(l => l.Name == "ELETRICA");
                layer.IsVisible = false;
                Assert.False(viewModel.Document.Layers.First(l => l.Name == "ELETRICA").IsVisible);

                viewModel.ShowAllLayersCommand.Execute(null);
                Assert.True(viewModel.Document.Layers.First(l => l.Name == "ELETRICA").IsVisible);

                // Test AddTextPromptCommand
                viewModel.MouseWorldX = 10.5;
                viewModel.MouseWorldY = 20.5;
                viewModel.AddTextPromptCommand.Execute(null);
                Assert.Contains(viewModel.Document.Entities, e => e is CadText text && text.Position.X == 10.5 && text.Position.Y == 20.5);

                // Test DeleteSelectedCommand
                var entity = viewModel.Document.Entities.First();
                entity.IsSelected = true;
                viewModel.SelectedCount = 1;
                viewModel.DeleteSelectedCommand.Execute(null);
                Assert.DoesNotContain(entity, viewModel.Document.Entities);
                Assert.Equal(0, viewModel.SelectedCount);
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) throw error;
    }
}

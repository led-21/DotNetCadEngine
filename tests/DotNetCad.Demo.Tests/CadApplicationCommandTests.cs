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
}

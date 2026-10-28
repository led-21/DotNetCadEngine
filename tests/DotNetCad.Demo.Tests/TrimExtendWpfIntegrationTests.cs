using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Core.History;
using DotNetCad.Demo.Controls;
using DotNetCad.Demo.ViewModels;
using Xunit;

namespace DotNetCad.Demo.Tests;

public class TrimExtendWpfIntegrationTests
{
    [Fact]
    public void Trim_ViewportInteractiveFlow_SelectsBoundary_TrimsSegment_SupportsUndoRedo()
    {
        Sta(() =>
        {
            using var host = new HwndSource(new HwndSourceParameters("Trim viewport test") { Width = 800, Height = 600 });
            var doc = new CadDocument();
            var history = new UndoRedoManager();

            // Target horizontal: (0, 5) to (10, 5)
            // Boundary vertical: (5, 0) to (5, 10)
            var target = new CadLine(new(0, 5), new(10, 5));
            var boundary = new CadLine(new(5, 0), new(5, 10));
            doc.AddEntity(target);
            doc.AddEntity(boundary);

            var vp = new CadViewport
            {
                Document = doc,
                UndoManager = history,
                Width = 800,
                Height = 600
            };
            host.RootVisual = vp;

            // 1. Activate Trim tool
            vp.ActiveTool = CadToolType.Trim;
            vp.Measure(new Size(800, 600));
            vp.Arrange(new Rect(0, 0, 800, 600));
            vp.UpdateLayout();

            var sessionField = typeof(CadViewport).GetField("_trimExtendSession", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var session = (CadTrimExtendSession)sessionField.GetValue(vp)!;
            Assert.NotNull(session);
            Assert.Equal(0, session.Step);
            Assert.Contains("Selecione os limites de corte", vp.CommandStatus);

            // 2. Press Enter to confirm boundaries (uses visible entities)
            SimulateKeyDown(vp, Key.Enter);
            Assert.Equal(1, session.Step);
            Assert.Contains("Clique no segmento que deseja aparar", vp.CommandStatus);

            // 3. Hover over target near start (1, 5)
            SimulateMouseMove(vp, new Point2D(1, 5));
            Assert.NotNull(session.HoverTrimResult);

            // 4. Click to trim near start
            SimulateMouseDown(vp, new Point2D(1, 5));
            Assert.Equal(1, history.UndoCount);
            Assert.Equal(1, session.Step); // Remains in Step 1 for consecutive trims

            var remaining = Assert.Single(doc.Entities.OfType<CadLine>(), l => l.Id != boundary.Id);
            Assert.Equal(5.0, remaining.StartPoint.X, 3);
            Assert.Equal(10.0, remaining.EndPoint.X, 3);

            // 5. Test Undo / Redo
            history.Undo();
            var restored = Assert.Single(doc.Entities.OfType<CadLine>(), l => l.Id != boundary.Id);
            Assert.Equal(0.0, restored.StartPoint.X, 3);
            Assert.Equal(10.0, restored.EndPoint.X, 3);

            history.Redo();
            var reapplied = Assert.Single(doc.Entities.OfType<CadLine>(), l => l.Id != boundary.Id);
            Assert.Equal(5.0, reapplied.StartPoint.X, 3);

            // 6. Press Enter to finish
            SimulateKeyDown(vp, Key.Enter);
            Assert.Equal(CadToolType.Select, vp.ActiveTool);
            Assert.Null(sessionField.GetValue(vp));
        });
    }

    [Fact]
    public void Extend_ViewportInteractiveFlow_ExtendsToBoundary_SupportsUndoRedo()
    {
        Sta(() =>
        {
            using var host = new HwndSource(new HwndSourceParameters("Extend viewport test") { Width = 800, Height = 600 });
            var doc = new CadDocument();
            var history = new UndoRedoManager();

            // Line: (0, 5) to (4, 5)
            // Boundary: vertical line at X=8 from (8, 0) to (8, 10)
            var target = new CadLine(new(0, 5), new(4, 5));
            var boundary = new CadLine(new(8, 0), new(8, 10));
            doc.AddEntity(target);
            doc.AddEntity(boundary);

            var vp = new CadViewport
            {
                Document = doc,
                UndoManager = history,
                Width = 800,
                Height = 600
            };
            host.RootVisual = vp;

            // 1. Activate Extend tool
            vp.ActiveTool = CadToolType.Extend;
            vp.Measure(new Size(800, 600));
            vp.Arrange(new Rect(0, 0, 800, 600));
            vp.UpdateLayout();

            var sessionField = typeof(CadViewport).GetField("_trimExtendSession", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var session = (CadTrimExtendSession)sessionField.GetValue(vp)!;
            Assert.NotNull(session);
            Assert.Equal(0, session.Step);
            Assert.Contains("Selecione os limites de extensão", vp.CommandStatus);

            // 2. Press Enter to confirm boundaries
            SimulateKeyDown(vp, Key.Enter);
            Assert.Equal(1, session.Step);
            Assert.Contains("Clique próximo à extremidade", vp.CommandStatus);

            // 3. Hover near endpoint (3.8, 5)
            SimulateMouseMove(vp, new Point2D(3.8, 5));
            Assert.NotNull(session.HoverExtendResult);
            var extCandidate = Assert.IsType<CadLine>(session.HoverExtendResult.ExtendedEntity);
            Assert.Equal(8.0, extCandidate.EndPoint.X, 3);

            // 4. Click to extend
            SimulateMouseDown(vp, new Point2D(3.8, 5));
            Assert.Equal(1, history.UndoCount);
            Assert.Equal(1, session.Step); // Remains in Step 1 for consecutive extends

            var extended = Assert.Single(doc.Entities.OfType<CadLine>(), l => l.Id != boundary.Id);
            Assert.Equal(8.0, extended.EndPoint.X, 3);

            // 5. Test Undo / Redo
            history.Undo();
            var restored = Assert.Single(doc.Entities.OfType<CadLine>(), l => l.Id != boundary.Id);
            Assert.Equal(4.0, restored.EndPoint.X, 3);

            history.Redo();
            var reapplied = Assert.Single(doc.Entities.OfType<CadLine>(), l => l.Id != boundary.Id);
            Assert.Equal(8.0, reapplied.EndPoint.X, 3);

            // 6. Press Enter to finish
            SimulateKeyDown(vp, Key.Enter);
            Assert.Equal(CadToolType.Select, vp.ActiveTool);
        });
    }

    [Fact]
    public void Trim_EscapeKey_CancelsStepOrExitsTool()
    {
        Sta(() =>
        {
            using var host = new HwndSource(new HwndSourceParameters("Trim Esc test") { Width = 600, Height = 400 });
            var doc = new CadDocument();
            var history = new UndoRedoManager();
            var vp = new CadViewport { Document = doc, UndoManager = history, Width = 600, Height = 400 };
            host.RootVisual = vp;

            vp.ActiveTool = CadToolType.Trim;
            var sessionField = typeof(CadViewport).GetField("_trimExtendSession", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var session = (CadTrimExtendSession)sessionField.GetValue(vp)!;
            Assert.Equal(0, session.Step);

            // Advance to Step 1
            SimulateKeyDown(vp, Key.Enter);
            Assert.Equal(1, session.Step);

            // Esc in Step 1 rolls back to Step 0
            SimulateKeyDown(vp, Key.Escape);
            Assert.Equal(0, session.Step);
            Assert.Equal(CadToolType.Trim, vp.ActiveTool);

            // Esc in Step 0 exits to Select
            SimulateKeyDown(vp, Key.Escape);
            Assert.Equal(CadToolType.Select, vp.ActiveTool);
        });
    }

    [Fact]
    public void MainViewModel_CommandsAndShortcuts_ActivateTrimAndExtend()
    {
        Sta(() =>
        {
            var vm = new MainViewModel();

            // Tool activation via command
            vm.TrimSelectedCommand.Execute(null);
            Assert.Equal(CadToolType.Trim, vm.ActiveTool);

            vm.ExtendSelectedCommand.Execute(null);
            Assert.Equal(CadToolType.Extend, vm.ActiveTool);

            // Activation via Console shortcuts
            vm.RunCadCommand("TR");
            Assert.Equal(CadToolType.Trim, vm.ActiveTool);

            vm.RunCadCommand("EX");
            Assert.Equal(CadToolType.Extend, vm.ActiveTool);

            vm.RunCadCommand("TRIM");
            Assert.Equal(CadToolType.Trim, vm.ActiveTool);

            vm.RunCadCommand("ESTENDER");
            Assert.Equal(CadToolType.Extend, vm.ActiveTool);
        });
    }

    #region Helpers

    private static void SimulateKeyDown(CadViewport vp, Key key)
    {
        var e = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(vp) ?? new HwndSource(0, 0, 0, 0, 0, "", nint.Zero), 0, key)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        };
        typeof(CadViewport).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(vp, [e]);
    }

    private static void SimulateMouseDown(CadViewport vp, Point2D worldPt)
    {
        vp.HandleTrimExtendHover(worldPt);
        vp.HandleTrimExtendClick(worldPt);
    }

    private static void SimulateMouseMove(CadViewport vp, Point2D worldPt)
    {
        vp.HandleTrimExtendHover(worldPt);
    }

    private static void Sta(Action action)
    {
        Exception? error = null;
        var t = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (error != null) throw error;
    }

    #endregion
}

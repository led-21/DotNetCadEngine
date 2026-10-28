using System.Threading;
using DotNetCad.Core.Entities;
using DotNetCad.Demo.ViewModels;
using DotNetCad.Infrastructure.Svg;
using Xunit;

namespace DotNetCad.Demo.Tests;

public sealed class MutuaConectaDemoTests
{
    private static void RunInSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error != null)
        {
            throw new AggregateException("STA thread failed.", error);
        }
    }

    [Fact]
    public void MainViewModel_MutuaCommands_ExecuteCorrectly()
    {
        RunInSta(() =>
        {
            var viewModel = new MainViewModel();

            Assert.NotNull(viewModel.Mutua);
            Assert.Equal("PRJ-2026-001", viewModel.Mutua.LinkedProjectCode);
            Assert.True(viewModel.Mutua.IsPanelVisible);

            // Test MUTUA STATUS command
            var statusResult = viewModel.RunCadCommand("MUTUA STATUS");
            Assert.True(statusResult.Success);
            Assert.Contains("PRJ-2026-001", statusResult.Message);

            // Test MUTUA SILOS command (loads the 4 multidisciplinary layers)
            var silosResult = viewModel.RunCadCommand("MUTUA SILOS");
            Assert.True(silosResult.Success);
            Assert.True(silosResult.DrawingChanged);
            Assert.True(silosResult.LayersChanged);

            var doc = viewModel.Document;
            Assert.Equal("UnidadeBeneficiamentoSilos.cad", doc.Title);
            Assert.NotNull(doc.GetLayer("ESTRUTURA"));
            Assert.NotNull(doc.GetLayer("ELETRICA"));
            Assert.NotNull(doc.GetLayer("SPDA"));
            Assert.NotNull(doc.GetLayer("DRENAGEM"));

            // Check entities
            Assert.True(doc.Entities.Count >= 10);
            Assert.Contains(doc.Entities, e => e.Layer == "ESTRUTURA" && e is CadCircle);
            Assert.Contains(doc.Entities, e => e.Layer == "ESTRUTURA" && e is CadRectangle);
            Assert.Contains(doc.Entities, e => e.Layer == "ELETRICA" && e is CadRectangle);
            Assert.Contains(doc.Entities, e => e.Layer == "SPDA");
            Assert.Contains(doc.Entities, e => e.Layer == "DRENAGEM");

            // SVG export must generate valid markup
            var svg = SvgCadExporter.ExportToSvg(doc);
            Assert.Contains("<svg", svg);
            Assert.Contains("ESTRUTURA", svg);
            Assert.Contains("ELETRICA", svg);
        });
    }

    [Fact]
    public void MutuaViewModel_DraftCollaboration_RequiresHumanConfirmation()
    {
        RunInSta(() =>
        {
            var viewModel = new MainViewModel();
            var mutua = viewModel.Mutua;

            Assert.False(mutua.IsDraftPendingConfirmation);

            var helenaId = Guid.Parse("22222222-2222-2222-2222-222222222202");
            mutua.StageCollaborationDraft(
                helenaId,
                "Eng. Helena Vasconcelos",
                "Convite para assumir subestação 300 kVA",
                14500.00m);

            Assert.True(mutua.IsDraftPendingConfirmation);
            Assert.Equal("Eng. Helena Vasconcelos", mutua.DraftRecipientName);
            Assert.Equal(helenaId, mutua.DraftRecipientProfileId);
            Assert.Equal(14500.00m, mutua.DraftHonorarium);
            Assert.Contains("subestação", mutua.DraftMessage);

            // User cancels the draft
            mutua.CancelDraftCommand.Execute(null);
            Assert.False(mutua.IsDraftPendingConfirmation);
        });
    }

    [Fact]
    public void MutuaViewModel_TogglePanel_Works()
    {
        RunInSta(() =>
        {
            var viewModel = new MainViewModel();
            Assert.True(viewModel.Mutua.IsPanelVisible);

            viewModel.Mutua.TogglePanelCommand.Execute(null);
            Assert.False(viewModel.Mutua.IsPanelVisible);

            viewModel.Mutua.TogglePanelCommand.Execute(null);
            Assert.True(viewModel.Mutua.IsPanelVisible);
        });
    }
}

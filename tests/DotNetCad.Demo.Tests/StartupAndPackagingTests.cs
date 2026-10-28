using System.IO;
using System.Reflection;
using System.Threading;
using DotNetCad.Core.Entities;
using DotNetCad.Demo.ViewModels;
using DotNetCad.Demo.Views;
using Xunit;

namespace DotNetCad.Demo.Tests;

public class StartupAndPackagingTests
{
    [Fact]
    public void SplashScreenViewInstantiatesAndFormatsVersionCorrectly()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var splash = new SplashScreenView();
                Assert.NotNull(splash);
                Assert.Contains("v", splash.VersionText);
                Assert.Contains("x64 Windows", splash.VersionText);

                splash.UpdateStatus("Teste de status técnico", 50);
                Assert.Equal("Teste de status técnico", splash.StatusText);
                Assert.False(splash.IsProgressIndeterminate);
                Assert.Equal(50, splash.ProgressValue);

                splash.UpdateStatus("Indeterminado", null);
                Assert.True(splash.IsProgressIndeterminate);

                splash.Close();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error != null) throw error;
    }

    [Fact]
    public void AppMutexConstantIsDefinedAndStable()
    {
        Assert.Equal("DotNetCadEngine_Running_Mutex", App.AppMutexName);
    }

    [Fact]
    public void OpeningFileViaCliArgumentPreservesDrawingOnFailure()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var vm = new MainViewModel();
                var initialEntityCount = vm.Document.Entities.Count;

                // 1. Arquivo não existente não descarta desenho aberto
                var nonExistentPath = @"C:\NonExistentDirectory\FakeDrawing.cad";
                var resultNonExistent = vm.ReplaceFromFile(nonExistentPath);
                Assert.False(resultNonExistent.Success);
                Assert.Equal(initialEntityCount, vm.Document.Entities.Count);

                // 2. Arquivo com espaços no caminho e formato válido .cad
                var tempDir = Path.Combine(Path.GetTempPath(), "DotNetCad Test Spaced Path");
                Directory.CreateDirectory(tempDir);
                var validFilePath = Path.Combine(tempDir, "Projeto Silo 2026.cad");

                try
                {
                    vm.RunCadCommand("LINE 0;0 10;10");
                    var saveResult = vm.RunCadCommand($"SAVE \"{validFilePath}\"");
                    Assert.True(saveResult.Success);
                    Assert.True(File.Exists(validFilePath));

                    // Novo ViewModel abrindo arquivo com espaços
                    var vm2 = new MainViewModel();
                    var openResult = vm2.ReplaceFromFile(validFilePath);
                    Assert.True(openResult.Success);
                    Assert.Contains(vm2.Document.Entities, e => e is CadLine);
                    Assert.Equal("Projeto Silo 2026.cad", vm2.Document.Title);
                }
                finally
                {
                    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                }

                // 3. Arquivo corrompido não descarta desenho aberto
                var corruptedPath = Path.Combine(Path.GetTempPath(), $"Corrupted_{Guid.NewGuid():N}.cad");
                File.WriteAllText(corruptedPath, "{ invalid json content not a cad }");
                try
                {
                    var resultCorrupt = vm.ReplaceFromFile(corruptedPath);
                    Assert.False(resultCorrupt.Success);
                    Assert.Equal(initialEntityCount + 1, vm.Document.Entities.Count); // Preservou a linha adicionada
                }
                finally
                {
                    if (File.Exists(corruptedPath)) File.Delete(corruptedPath);
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error != null) throw error;
    }
}

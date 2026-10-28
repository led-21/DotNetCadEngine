using System.IO;
using System.Threading;
using System.Windows;
using DotNetCad.Demo.ViewModels;
using DotNetCad.Demo.Views;

namespace DotNetCad.Demo;

public partial class App : Application
{
    public const string AppMutexName = "DotNetCadEngine_Running_Mutex";
    private Mutex? _appMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Aquisição de Mutex para identificação de processo em execução (usado pelo instalador Inno Setup)
        try
        {
            _appMutex = new Mutex(true, AppMutexName, out _);
        }
        catch
        {
            // Mutex de processo não deve impedir execução em ambientes restritos
        }

        // Garante que o fechamento da splash não encerre a aplicação prematuramente
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        SplashScreenView? splash = null;

        try
        {
            // 1. Exibir Splash Screen técnica
            splash = new SplashScreenView();
            splash.Show();

            // 2. Extração de argumento de linha de comando (abrir arquivo pelo Explorer / CLI)
            string? fileToOpen = null;
            if (e.Args.Length > 0)
            {
                var candidate = e.Args[0].Trim('"', '\'');
                if (!string.IsNullOrWhiteSpace(candidate) && !candidate.StartsWith("-") && !candidate.StartsWith("/"))
                {
                    fileToOpen = candidate;
                }
            }

            // 3. Etapas reais de inicialização assíncrona
            splash.UpdateStatus("Carregando tema e preferências...", 20);
            await Task.Delay(100); // Garante renderização do primeiro frame da splash

            splash.UpdateStatus("Inicializando subsistemas geométricos 2D...", 50);
            await Task.Delay(100);

            splash.UpdateStatus("Configurando viewport e console técnico...", 75);

            // 4. Criação da janela principal e ViewModel na thread de UI
            var mainWindow = new MainWindow();
            if (mainWindow.DataContext is MainViewModel viewModel)
            {
                if (!string.IsNullOrWhiteSpace(fileToOpen))
                {
                    splash.UpdateStatus($"Abrindo '{Path.GetFileName(fileToOpen)}'...", 90);
                    if (File.Exists(fileToOpen))
                    {
                        var result = viewModel.ReplaceFromFile(fileToOpen);
                        if (!result.Success)
                        {
                            viewModel.AppendConsole($"CAD AVISO: Não foi possível abrir o arquivo '{fileToOpen}': {result.Message}");
                        }
                    }
                    else
                    {
                        viewModel.AppendConsole($"CAD AVISO: Arquivo informado não foi encontrado: '{fileToOpen}'.");
                    }
                }
                else
                {
                    splash.UpdateStatus("Pronto.", 100);
                }
            }

            // 5. Transição suave: exibir janela principal e fechar splash
            mainWindow.Show();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            splash.Close();
            splash = null;
        }
        catch (Exception ex)
        {
            try { splash?.Close(); } catch { }
            HandleStartupFailure(ex);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_appMutex != null)
            {
                _appMutex.ReleaseMutex();
                _appMutex.Dispose();
                _appMutex = null;
            }
        }
        catch
        {
            // Ignora falhas de liberação no encerramento
        }

        base.OnExit(e);
    }

    private static void HandleStartupFailure(Exception ex)
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DotNetCadEngine", "Logs");

        var logFile = Path.Combine(logDir, "startup-error.log");

        try
        {
            Directory.CreateDirectory(logDir);
            File.WriteAllText(logFile,
                $"[{DateTime.UtcNow:O}] Falha na inicialização do DotNetCadEngine:{Environment.NewLine}{ex}");
        }
        catch
        {
            // Fallback se não for possível gravar em disco
        }

        MessageBox.Show(
            $"Ocorreu um erro ao inicializar o DotNetCadEngine:{Environment.NewLine}{Environment.NewLine}" +
            $"{ex.Message}{Environment.NewLine}{Environment.NewLine}" +
            $"Detalhes foram registrados em:{Environment.NewLine}{logFile}",
            "Erro de Inicialização — DotNetCadEngine",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        Environment.Exit(1);
    }
}

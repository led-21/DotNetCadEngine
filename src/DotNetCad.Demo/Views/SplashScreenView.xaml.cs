using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace DotNetCad.Demo.Views;

public partial class SplashScreenView : Window
{
    public SplashScreenView()
    {
        InitializeComponent();
        SetVersionText();
        MouseDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        };
    }

    private void SetVersionText()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var infoVer = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var ver = !string.IsNullOrWhiteSpace(infoVer)
                ? infoVer.Split('+')[0]
                : assembly.GetName().Version?.ToString(3) ?? "1.0.0";

            TxtVersion.Text = $"v{ver} (x64 Windows)";
        }
        catch
        {
            TxtVersion.Text = "v1.0.0 (x64 Windows)";
        }
    }

    public string VersionText => TxtVersion.Text;
    public string StatusText => TxtStatus.Text;
    public bool IsProgressIndeterminate => StartupProgress.IsIndeterminate;
    public double ProgressValue => StartupProgress.Value;

    public void UpdateStatus(string message, double? percent = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => UpdateStatus(message, percent));
            return;
        }

        TxtStatus.Text = message;
        if (percent.HasValue && percent.Value >= 0 && percent.Value <= 100)
        {
            StartupProgress.IsIndeterminate = false;
            StartupProgress.Value = percent.Value;
        }
        else
        {
            StartupProgress.IsIndeterminate = true;
        }
    }
}

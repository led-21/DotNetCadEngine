using System.Windows;
using DotNetCad.Demo.Mcp;
using DotNetCad.Demo.ViewModels;

namespace DotNetCad.Demo;

public partial class MainWindow : Window
{
    private CadMcpPipeHost? _mcpHost;
    public bool EnableMcpHost { get; set; } = true;

    private void ResizeConsole(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) => ConsolePanel.Height = Math.Clamp(ConsolePanel.Height - e.VerticalChange, 65, Math.Max(65, ActualHeight * .35));
    private void ResizeInspector(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) => InspectorPanel.Width = Math.Clamp(InspectorPanel.Width - e.HorizontalChange, 130, Math.Max(130, ActualWidth * .4));
    private void ResizeConsoleKey(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is System.Windows.Input.Key.Up or System.Windows.Input.Key.Down) { ConsolePanel.Height = Math.Clamp(ConsolePanel.Height + (e.Key == System.Windows.Input.Key.Up ? 10 : -10),65,Math.Max(65,ActualHeight*.35)); e.Handled=true; }
    }
    private void ResizeInspectorKey(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is System.Windows.Input.Key.Left or System.Windows.Input.Key.Right) { InspectorPanel.Width = Math.Clamp(InspectorPanel.Width + (e.Key == System.Windows.Input.Key.Left ? 10 : -10),130,Math.Max(130,ActualWidth*.4)); e.Handled=true; }
    }
    public MainWindow()
    {
        InitializeComponent();
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.PropertyChanged += (_, e) => {
                if (e.PropertyName == nameof(MainViewModel.ActiveTool)) Dispatcher.BeginInvoke(() => Viewport.Focus());
            };
            viewModel.ZoomExtentsHandler = Viewport.ZoomExtents;
            viewModel.ResetViewHandler = Viewport.ResetView;
            _mcpHost = new CadMcpPipeHost(viewModel, Dispatcher);
            Loaded += (_, _) => { if (EnableMcpHost) _mcpHost.Start(); };
            Closing += (_, e) => e.Cancel = !viewModel.ConfirmPendingChanges();
            Closed += (_, _) => _mcpHost.Dispose();
            viewModel.ConsoleHistory.CollectionChanged += (_, _) =>
            {
                if (ConsoleOutput.Items.Count > 0)
                    ConsoleOutput.ScrollIntoView(ConsoleOutput.Items[^1]);
            };
        }
    }
}

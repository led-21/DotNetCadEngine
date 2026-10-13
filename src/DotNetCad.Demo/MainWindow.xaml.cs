using System.Windows;
using DotNetCad.Demo.Mcp;
using DotNetCad.Demo.ViewModels;

namespace DotNetCad.Demo;

public partial class MainWindow : Window
{
    private CadMcpPipeHost? _mcpHost;

    public MainWindow()
    {
        InitializeComponent();
        if (DataContext is MainViewModel viewModel)
        {
            _mcpHost = new CadMcpPipeHost(viewModel, Dispatcher);
            Loaded += (_, _) => _mcpHost.Start();
            Closed += (_, _) => _mcpHost.Dispose();
            viewModel.ConsoleHistory.CollectionChanged += (_, _) =>
            {
                if (ConsoleOutput.Items.Count > 0)
                    ConsoleOutput.ScrollIntoView(ConsoleOutput.Items[^1]);
            };
        }
    }
}

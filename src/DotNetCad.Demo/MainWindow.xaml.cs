using System.Windows;
using DotNetCad.Demo.Mcp;
using DotNetCad.Demo.ViewModels;

namespace DotNetCad.Demo;

public partial class MainWindow : Window
{
    private CadMcpPipeHost? _mcpHost;
    public bool EnableMcpHost { get; set; } = true;

    public bool EnableLayoutPreferences { get; set; } = true;
    private double _expandedConsoleHeight=180;
    private bool _layoutReady;
    private void ResizeInspector(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) => InspectorPanel.Width = Math.Clamp(InspectorPanel.Width - e.HorizontalChange, 130, Math.Max(130, ActualWidth * .4));
    private void ConsoleExpandedChanged(object? sender, EventArgs e)
    {
        if(!_layoutReady)return;
        ConsoleRow.Height=new GridLength(ConsoleView.IsHistoryExpanded ? Math.Min(_expandedConsoleHeight,ConsoleRow.MaxHeight):76);
        ConsoleDivider.IsEnabled=ConsoleView.IsHistoryExpanded;
    }
    private void ConsoleResized(object sender,System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if(ConsoleView.IsHistoryExpanded)_expandedConsoleHeight=Math.Clamp(ConsoleRow.ActualHeight,76,600);
    }
    private void ConsoleResizeKey(object sender,System.Windows.Input.KeyEventArgs e)
    {
        if(ConsoleView.IsHistoryExpanded && e.Key is System.Windows.Input.Key.Up or System.Windows.Input.Key.Down)_expandedConsoleHeight=Math.Clamp(ConsoleRow.ActualHeight,76,600);
    }
    private void ResetConsoleSize(object? sender,EventArgs e)
    {
        _expandedConsoleHeight=180;ConsoleView.IsHistoryExpanded=true;ConstrainConsole();
    }
    private void ConstrainConsole()
    {
        if(!_layoutReady)return;
        ConsoleRow.MaxHeight=Math.Max(76,WorkspaceGrid.ActualHeight-85);
        ConsoleRow.Height=new GridLength(ConsoleView.IsHistoryExpanded?Math.Min(_expandedConsoleHeight,ConsoleRow.MaxHeight):76);
        InspectorPanel.Width=Math.Clamp(InspectorPanel.Width,130,Math.Max(130,ActualWidth*.4));
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
                if (e.PropertyName == nameof(MainViewModel.ActiveTool) && !ConsoleView.IsKeyboardFocusWithin) Dispatcher.BeginInvoke(() => Viewport.Focus());
            };
            viewModel.UiDispatcher=Dispatcher;
            viewModel.InteractiveOperationActive=()=>Viewport.HasInteractiveOperation;
            viewModel.CancelInteractiveOperation=Viewport.CancelOperation;
            viewModel.ZoomExtentsHandler = Viewport.ZoomExtents;
            viewModel.ResetViewHandler = Viewport.ResetView;
            _mcpHost = new CadMcpPipeHost(viewModel, Dispatcher);
            Loaded += (_, _) => { if (EnableMcpHost) _mcpHost.Start(); };
            Closing += (_, e) => e.Cancel = !viewModel.ConfirmPendingChanges();
            Closed += (_, _) => {
                _mcpHost.Dispose();
                if(EnableLayoutPreferences)try{new CadLayoutPreferences(_expandedConsoleHeight,ConsoleView.IsHistoryExpanded,InspectorPanel.Width,InspectorExpander.IsExpanded).Save();}
                catch(Exception ex) when(ex is System.IO.IOException or UnauthorizedAccessException){viewModel.AppendConsole("Não foi possível salvar preferências de layout: "+ex.Message);}
            };
            Loaded += (_, _) => {
                if(EnableLayoutPreferences){var p=CadLayoutPreferences.Load();_expandedConsoleHeight=p.ConsoleHeight;ConsoleView.IsHistoryExpanded=p.ConsoleExpanded;InspectorPanel.Width=p.InspectorWidth;InspectorExpander.IsExpanded=p.InspectorExpanded;}
                _layoutReady=true;ConstrainConsole();ConsoleExpandedChanged(this,EventArgs.Empty);
            };
            SizeChanged+=(_,_)=>ConstrainConsole();
            WorkspaceGrid.SizeChanged+=(_,_)=>ConstrainConsole();

        }
    }
}

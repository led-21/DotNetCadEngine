using System.Windows;

using DotNetCad.Demo.Mcp;

using DotNetCad.Demo.ViewModels;



namespace DotNetCad.Demo;



public partial class MainWindow : Window

{
    private void CopyMutuaDemoCredential_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string value } button) return;
        try
        {
            Clipboard.SetText(value);
            if (DataContext is MainViewModel vm)
                vm.Mutua.StatusMessage = button.Content?.ToString() == "Copiar senha"
                    ? "Senha demonstrativa copiada."
                    : "E-mail demonstrativo copiado.";
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            if (DataContext is MainViewModel vm)
                vm.Mutua.StatusMessage = "Área de transferência ocupada. Tente copiar novamente.";
        }
    }

    private void MutuaPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is DotNetCad.Demo.ViewModels.MainViewModel vm && sender is System.Windows.Controls.PasswordBox input) vm.Mutua.DemoPassword = input.Password;
    }


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

    public void UpdateConsoleVisibility(bool isVisible)

    {

        if (!_layoutReady) return;

        if (isVisible)

        {

            ConsoleDivider.Visibility = Visibility.Visible;

            ConsoleView.Visibility = Visibility.Visible;

            ConsoleRow.MinHeight = 76;

            ConsoleRow.Height = new GridLength(ConsoleView.IsHistoryExpanded ? Math.Min(_expandedConsoleHeight, ConsoleRow.MaxHeight) : 76);

            ConsoleDivider.IsEnabled = ConsoleView.IsHistoryExpanded;

            Dispatcher.BeginInvoke(() => ConsoleView.FocusInput());

        }

        else

        {

            ConsoleDivider.Visibility = Visibility.Collapsed;

            ConsoleView.Visibility = Visibility.Collapsed;

            ConsoleRow.MinHeight = 0;

            ConsoleRow.Height = new GridLength(0);

            ConsoleDivider.IsEnabled = false;

            Dispatcher.BeginInvoke(() => Viewport.Focus());

        }

    }



    private void ConstrainConsole()

    {

        if(!_layoutReady)return;

        if (DataContext is MainViewModel vm && !vm.IsConsoleVisible)

        {

            UpdateConsoleVisibility(false);

            return;

        }

        ConsoleRow.MaxHeight=Math.Max(76,WorkspaceGrid.ActualHeight-85);

        ConsoleRow.Height=new GridLength(ConsoleView.IsHistoryExpanded?Math.Min(_expandedConsoleHeight,ConsoleRow.MaxHeight):76);

        InspectorPanel.Width=Math.Clamp(InspectorPanel.Width,130,Math.Max(130,ActualWidth*.4));

    }



    private void ExitApplication_Click(object sender, RoutedEventArgs e) => Close();



    private void ShowShortcutsHelp_Click(object sender, RoutedEventArgs e)

    {

        MessageBox.Show(

            "ATALHOS DE TECLADO DO DOTNETCAD ENGINE:\n\n" +

            "• F2: Ativar / Desativar Console de Comandos\n" +

            "• F3: Alternar Captura de Objetos (Osnap)\n" +

            "• F8: Alternar Modo Ortogonal (Ortho)\n" +

            "• F10: Alternar Rastreamento Polar\n" +

            "• Ctrl + Z: Desfazer\n" +

            "• Ctrl + Y: Refazer\n" +

            "• Ctrl + A: Selecionar Todas as Entidades\n" +

            "• Ctrl + 0: Enquadrar Desenho (Zoom Extents)\n" +

            "• Ctrl + S: Salvar Desenho (.cad)\n" +

            "• Ctrl + Shift + S: Salvar Como…\n" +

            "• Ctrl + N: Novo Desenho\n" +

            "• Ctrl + O: Abrir Desenho\n" +

            "• Esc: Cancelar operação / Limpar Seleção\n" +

            "• Delete: Excluir Entidades Selecionadas\n" +

            "• Tab: Alternar entidade sob o cursor / Completar comando no console",

            "Atalhos de Teclado — DotNetCadEngine",

            MessageBoxButton.OK,

            MessageBoxImage.Information);

    }



    private void ShowConsoleHelp_Click(object sender, RoutedEventArgs e)

    {

        if (DataContext is MainViewModel vm)

        {

            vm.IsConsoleVisible = true;

            vm.RunCadCommand("HELP");

        }

    }



    private void ShowAbout_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "DotNetCadEngine — Edição Mútua Conecta\n\n" +
            "Versão: 1.0.0 (Windows x64)\n" +
            "Plataforma: .NET 10.0 WPF (Windows Desktop)\n" +
            "Ecossistema: Mútua Conecta • Plataforma Multidisciplinar de Engenharia\n" +
            "Formatos: .cad (Nativo), DXF (AutoCAD), SVG (Vetor Web)\n" +
            "Recursos: Entrada Numérica, Snaps, Cotas, Camadas e Servidor MCP Integrado.\n\n" +
            "© 2026 Mútua Conecta. Todos os direitos reservados.\n" +
            "Desenvolvido para o ecossistema profissional das engenharias, agronomia e geociências.",
            "Sobre o DotNetCadEngine — Mútua Conecta",

            MessageBoxButton.OK,

            MessageBoxImage.Information);

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

                if (e.PropertyName == nameof(MainViewModel.IsConsoleVisible)) UpdateConsoleVisibility(viewModel.IsConsoleVisible);

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

                if(EnableLayoutPreferences)try{new CadLayoutPreferences(_expandedConsoleHeight,ConsoleView.IsHistoryExpanded,InspectorPanel.Width,InspectorExpander.IsExpanded,viewModel.IsConsoleVisible).Save();}

                catch(Exception ex) when(ex is System.IO.IOException or UnauthorizedAccessException){viewModel.AppendConsole("Não foi possível salvar preferências de layout: "+ex.Message);}

            };

            Loaded += (_, _) => {

                if(EnableLayoutPreferences){var p=CadLayoutPreferences.Load();_expandedConsoleHeight=p.ConsoleHeight;InspectorPanel.Width=p.InspectorWidth;InspectorExpander.IsExpanded=p.InspectorExpanded;}

                // Start every session with the command console available, even if it was hidden on exit.
                viewModel.IsConsoleVisible = true;
                ConsoleView.IsHistoryExpanded = true;

                _layoutReady=true;ConstrainConsole();UpdateConsoleVisibility(viewModel.IsConsoleVisible);ConsoleExpandedChanged(this,EventArgs.Empty);

            };

            SizeChanged+=(_,_)=>ConstrainConsole();

            WorkspaceGrid.SizeChanged+=(_,_)=>ConstrainConsole();



        }

    }

}

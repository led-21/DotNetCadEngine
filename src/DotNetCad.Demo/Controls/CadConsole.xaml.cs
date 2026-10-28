using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DotNetCad.Core.Editor;
using DotNetCad.Demo.ViewModels;
using Microsoft.Win32;
namespace DotNetCad.Demo.Controls;

public partial class CadConsole : UserControl
{
    private MainViewModel? _vm;
    private System.ComponentModel.ICollectionView? _messages;
    private ScrollViewer? _scroll;
    private bool _following=true,_scrollQueued,_anchorQueued;
    private ConsoleMessage? _readAnchor;
    private int _unread;
    private string _prefix="";
    private int _completion;
    private bool _completing;
    public event EventHandler? ExpandedChanged;
    public event EventHandler? ResetSizeRequested;
    private void ResetSize(object sender,RoutedEventArgs e)=>ResetSizeRequested?.Invoke(this,EventArgs.Empty);
    public bool IsHistoryExpanded {get=>HistoryToggle.IsChecked==true;set=>HistoryToggle.IsChecked=value;}
    public void FocusInput() => Input?.Focus();
    public CadConsole()
    {
        InitializeComponent();
        DataContextChanged+=(_,_)=>Attach();
        Loaded+=(_,_)=>{Attach();_scroll=FindScroll(Output);};
        Unloaded+=(_,_)=>{if(_vm!=null)_vm.Console.Messages.CollectionChanged-=MessagesChanged;};
    }
    private void Attach()
    {
        if(_vm!=null)_vm.Console.Messages.CollectionChanged-=MessagesChanged;
        _vm=DataContext as MainViewModel;
        if(_vm==null)return;
        _messages=System.Windows.Data.CollectionViewSource.GetDefaultView(_vm.Console.Messages);
        _messages.Filter=Matches;Output.ItemsSource=_messages;
        _vm.Console.Messages.CollectionChanged+=MessagesChanged;
        ArgumentHelp.Text="Enter executa • ↑/↓ histórico • Tab completa • HELP mostra sintaxe";
    }
    private bool Matches(object obj)=>obj is ConsoleMessage m && (ErrorsOnly.IsChecked!=true || m.Severity==ConsoleSeverity.Erro) && m.Display.Contains(Search.Text,StringComparison.OrdinalIgnoreCase);
    private void FilterChanged(object sender,RoutedEventArgs e)
    {
        if(Output==null)return;
        _messages?.Refresh();
    }
    private void ToggleHistory(object sender,RoutedEventArgs e)
    {
        if(HistoryBody==null)return;
        HistoryBody.Visibility=IsHistoryExpanded?Visibility.Visible:Visibility.Collapsed;
        ExpandedChanged?.Invoke(this,EventArgs.Empty);
    }
    private void Execute(object sender,RoutedEventArgs e)
    {
        _vm?.ExecuteConsoleEntry();Input.Focus();Input.CaretIndex=Input.Text.Length;
    }
    private void InputChanged(object sender,TextChangedEventArgs e)
    {
        if(Input==null || ArgumentHelp==null)return;
        if(!_completing){_prefix="";_completion=0;}
        var hint=CadCommandCatalog.Hint(Input.Text);
        var suggestions=CadCommandCatalog.Complete(Input.Text.Trim());
        ArgumentHelp.Text=hint.Length>0?hint:suggestions.Count>0?"Tab: "+string.Join(" · ",suggestions):"Comando desconhecido. Digite HELP para ver a sintaxe.";
    }
    private void InputKeyDown(object sender,KeyEventArgs e)
    {
        if(_vm==null)return;
        if(e.Key==Key.Enter){Execute(sender,e);e.Handled=true;}
        else if(e.Key==Key.Up && Keyboard.Modifiers==ModifierKeys.None){Input.Text=_vm.Console.Previous(Input.Text);Input.CaretIndex=Input.Text.Length;e.Handled=true;}
        else if(e.Key==Key.Down && Keyboard.Modifiers==ModifierKeys.None){Input.Text=_vm.Console.Next(Input.Text);Input.CaretIndex=Input.Text.Length;e.Handled=true;}
        else if(e.Key==Key.Tab && Keyboard.Modifiers==ModifierKeys.None && !string.IsNullOrWhiteSpace(Input.Text) && !Input.Text.Trim().Contains(' '))
        {
            if(_prefix.Length==0 && CadCommandCatalog.Complete(Input.Text.Trim()) is {Count:1} exact && exact[0].Equals(Input.Text.Trim(),StringComparison.OrdinalIgnoreCase))return;
            if(_prefix.Length==0)_prefix=Input.Text.Trim();
            var choices=CadCommandCatalog.Complete(_prefix);
            if(choices.Count>0){_completing=true;Input.Text=choices[_completion++%choices.Count];_completing=false;Input.CaretIndex=Input.Text.Length;e.Handled=true;}
        }
        else if(e.Key==Key.Escape){_prefix="";_completion=0;ArgumentHelp.Text="Entrada preservada. A operação interativa é cancelada pelo botão Cancelar operação.";e.Handled=true;}
        // Editing shortcuts deliberately remain native TextBox commands.
    }
    private void CancelOperation(object sender,RoutedEventArgs e){_vm?.CancelInteractiveOperation?.Invoke();Input.Focus();}
    private void CopySelected(object sender,RoutedEventArgs e)
    {
        var text=string.Join(Environment.NewLine,Output.SelectedItems.Cast<ConsoleMessage>().Select(m=>m.Display));
        if(text.Length>0)TryClipboard(text);
    }
    private void CopyCommand(object sender,RoutedEventArgs e){if(Output.SelectedItem is ConsoleMessage {IsCommand:true} m)TryClipboard(m.Text);}
    private void TryClipboard(string text)
    {
        try{Clipboard.SetText(text);}catch(System.Runtime.InteropServices.ExternalException ex){_vm?.Console.Add("Não foi possível copiar: "+ex.Message,severity:ConsoleSeverity.Erro);}
    }
    private void ClearMessages(object sender,RoutedEventArgs e){_vm?.Console.ClearOutput();_unread=0;UpdateUnread();}
    private void ExportLog(object sender,RoutedEventArgs e)
    {
        var dialog=new SaveFileDialog {Filter="Texto (*.txt)|*.txt",DefaultExt=".txt",FileName="console-cad.txt"};
        if(dialog.ShowDialog()!=true)return;
        try{File.WriteAllLines(dialog.FileName,Output.Items.Cast<ConsoleMessage>().Select(m=>m.Display));}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){_vm?.Console.Add("Falha ao exportar: "+ex.Message,severity:ConsoleSeverity.Erro);}
    }
    private void MessagesChanged(object? sender,NotifyCollectionChangedEventArgs e)
    {
        if(e.Action==NotifyCollectionChangedAction.Reset){_unread=0;UpdateUnread();return;}
        if(e.Action==NotifyCollectionChangedAction.Remove && !_following && _readAnchor!=null && !_anchorQueued)
        {
            _anchorQueued=true;
            var anchor=_readAnchor;
            Dispatcher.BeginInvoke(DispatcherPriority.Background,()=>{
                _anchorQueued=false;
                if(!_following && Output.Items.Contains(anchor))_scroll?.ScrollToVerticalOffset(Output.Items.IndexOf(anchor));
            });
        }
        if(e.Action!=NotifyCollectionChangedAction.Add)return;
        if(!_following || !IsHistoryExpanded){_unread+=e.NewItems?.Count??0;UpdateUnread();return;}
        QueueScroll();
    }
    private void QueueScroll()
    {
        if(_scrollQueued)return;_scrollQueued=true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background,()=>{_scrollQueued=false;if(_following && IsHistoryExpanded){_scroll??=FindScroll(Output);_scroll?.ScrollToEnd();}});
    }
    private void OutputScrolled(object sender,ScrollChangedEventArgs e)
    {
        if(e.VerticalChange==0)return;
        _scroll??=FindScroll(Output);
        if(_scroll==null)return;
        _following=_scroll.VerticalOffset>=_scroll.ScrollableHeight-0.5;
        if(_following){_readAnchor=null;_unread=0;UpdateUnread();}
        else if(Output.Items.Count>0)_readAnchor=Output.Items[Math.Clamp((int)_scroll.VerticalOffset,0,Output.Items.Count-1)] as ConsoleMessage;
    }
    private void ShowNewMessages(object sender,RoutedEventArgs e){IsHistoryExpanded=true;_following=true;_unread=0;UpdateUnread();QueueScroll();}
    private void UpdateUnread(){NewMessages.Content=$"{_unread} nova(s) ↓";NewMessages.Visibility=_unread>0?Visibility.Visible:Visibility.Collapsed;}
    private static ScrollViewer? FindScroll(DependencyObject element)
    {
        if(element is ScrollViewer scroll)return scroll;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(element);i++){var result=FindScroll(VisualTreeHelper.GetChild(element,i));if(result!=null)return result;}
        return null;
    }
}

using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using DotNetCad.Demo.Controls;
using DotNetCad.Demo.ViewModels;
using Xunit;
namespace DotNetCad.Demo.Tests;
public class ConsoleViewTests
{
    [Fact]
    public void HistoryCollapseKeyboardFilterAndReadingPastRemainIndependent()
    {
        Exception? error=null;
        var thread=new Thread(()=>{
            Window? window=null;
            try {
                var vm=new MainViewModel();var console=new CadConsole {DataContext=vm};
                window=new Window {Content=console,Width=640,Height=320};window.Show();Pump();
                var input=(TextBox)console.FindName("Input");var output=(ListBox)console.FindName("Output");
                var search=(TextBox)console.FindName("Search");var errors=(CheckBox)console.FindName("ErrorsOnly");
                for(int i=0;i<500;i++)vm.Console.Add($"mensagem {i:D4} "+new string('X',300));
                Pump();var scroll=FindScroll(output)!;Assert.True(scroll.ScrollableHeight>0);
                scroll.ScrollToTop();Pump();Assert.False((bool)typeof(CadConsole).GetField("_following",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(console)!);
                var position=scroll.VerticalOffset;
                vm.Console.Add("nova mensagem",ConsoleOrigin.MCP);Pump();
                Assert.Equal(position,scroll.VerticalOffset);Assert.True((int)typeof(CadConsole).GetField("_unread",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(console)!>0);
                var container=output.ItemContainerGenerator.ContainerFromIndex(0);Assert.NotNull(container);
                Assert.Null(output.ItemContainerGenerator.ContainerFromIndex(300)); // recycling virtualization
                search.Text="nova mensagem";Pump();Assert.Single(output.Items.Cast<object>());
                errors.IsChecked=true;Pump();Assert.Empty(output.Items.Cast<object>());
                vm.Console.Add("nova mensagem inválida",ConsoleOrigin.MCP,ConsoleSeverity.Erro);Pump();Assert.Single(output.Items.Cast<object>());
                console.IsHistoryExpanded=false;Pump();Assert.Equal(Visibility.Collapsed,((FrameworkElement)console.FindName("HistoryBody")).Visibility);Assert.True(input.IsVisible);
                console.IsHistoryExpanded=true;Pump();
                input.Text="LINE inválida";input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                typeof(CadConsole).GetMethod("Execute",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(console,[console,new RoutedEventArgs()]);
                Assert.Equal("LINE inválida",input.Text);Assert.True(input.IsKeyboardFocusWithin);
                input.SelectAll();Assert.True(ApplicationCommands.Copy.CanExecute(null,input));
                var entityCount=vm.Document.Entities.Count;var undo=vm.UndoManager.UndoCount;
                ApplicationCommands.Delete.Execute(null,input);Pump();Assert.Equal(entityCount,vm.Document.Entities.Count);Assert.Equal(undo,vm.UndoManager.UndoCount);
                vm.Console.Remember("LIST");input.Text="rascunho";
                var source=PresentationSource.FromVisual(input)!;
                void Key(Key key)=>typeof(CadConsole).GetMethod("InputKeyDown",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(console,[input,new KeyEventArgs(Keyboard.PrimaryDevice,source,0,key){RoutedEvent=Keyboard.PreviewKeyDownEvent}]);
                Key(System.Windows.Input.Key.Up);Assert.Equal("LIST",input.Text);Key(System.Windows.Input.Key.Down);Assert.Equal("rascunho",input.Text);
                input.Text="CIR";Key(System.Windows.Input.Key.Tab);Assert.Equal("CIRCLE",input.Text);
                vm.UiDispatcher=Dispatcher.CurrentDispatcher;
                var task=System.Threading.Tasks.Task.Run(()=>vm.AppendConsole("mensagem de outra thread"));task.GetAwaiter().GetResult();Pump();Assert.Equal("mensagem de outra thread",vm.Console.Messages.Last().Text);
            }catch(Exception ex){error=ex;}finally{window?.Close();}
        });thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error!=null)throw error;
    }
    private static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,()=>frame.Continue=false);Dispatcher.PushFrame(frame);}
    private static ScrollViewer? FindScroll(System.Windows.DependencyObject d){if(d is ScrollViewer s)return s;for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(d);i++){var result=FindScroll(System.Windows.Media.VisualTreeHelper.GetChild(d,i));if(result!=null)return result;}return null;}
}

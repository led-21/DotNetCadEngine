using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DotNetCad.Core.Editor;
namespace DotNetCad.Demo.ViewModels;
internal static class LayerDialogs
{
    private static Window Create(string title,StackPanel panel) => new() {Title=title,Content=panel,SizeToContent=SizeToContent.WidthAndHeight,MinWidth=340,MaxWidth=560,WindowStartupLocation=WindowStartupLocation.CenterOwner,Owner=Application.Current?.MainWindow,Background=new SolidColorBrush(Color.FromRgb(30,41,59)),Foreground=Brushes.White};
    public static string? Rename(string name)
    {
        var panel=new StackPanel {Margin=new Thickness(16)};
        panel.Children.Add(new TextBlock {Text="Novo nome da camada:"});
        var input=new TextBox {Text=name,Margin=new Thickness(0,8,0,8)};panel.Children.Add(input);
        var window=Create("Renomear camada",panel);
        var ok=new Button {Content="Renomear",IsDefault=true};ok.Click+=(_,_)=>window.DialogResult=true;panel.Children.Add(ok);
        var cancel=new Button {Content="Cancelar",IsCancel=true,Margin=new Thickness(0,6,0,0)};panel.Children.Add(cancel);
        window.Loaded+=(_,_)=>{input.Focus();input.SelectAll();};
        return window.ShowDialog()==true?input.Text:null;
    }
    public static (LayerDeleteMode Mode,string? Target) Delete(string name,int count,IEnumerable<string> targets)
    {
        var panel=new StackPanel {Margin=new Thickness(16)};
        panel.Children.Add(new TextBlock {Text=$"Camada {name}: {count} entidade(s).",TextWrapping=TextWrapping.Wrap,MaxWidth=460});
        panel.Children.Add(new TextBlock {Text="Destino para transferir entidades:",Margin=new Thickness(0,8,0,4)});
        var target=new ComboBox {ItemsSource=targets.ToArray(),MinWidth=300};panel.Children.Add(target);
        var window=Create("Excluir camada",panel);var result=LayerDeleteMode.Cancel;
        var transfer=new Button {Content="Transferir entidades e excluir camada",Margin=new Thickness(0,8,0,4)};
        transfer.Click+=(_,_)=>{if(target.SelectedItem!=null){result=LayerDeleteMode.Transfer;window.DialogResult=true;}};panel.Children.Add(transfer);
        var remove=new Button {Content=$"Excluir camada e suas {count} entidade(s)",Margin=new Thickness(0,4,0,4)};
        remove.Click+=(_,_)=>{result=LayerDeleteMode.DeleteEntities;window.DialogResult=true;};panel.Children.Add(remove);
        panel.Children.Add(new Button {Content="Cancelar",IsCancel=true});
        return window.ShowDialog()==true?(result,target.SelectedItem as string):(LayerDeleteMode.Cancel,null);
    }
}

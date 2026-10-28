using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DotNetCad.Demo;
using DotNetCad.Demo.Controls;
using DotNetCad.Demo.ViewModels;
using Xunit;
namespace DotNetCad.Demo.Tests;
public class LayoutMatrixTests
{
    [Fact]
    public void OpenAndRenderLayoutMatrix()
    {
        Exception? error=null;
        var thread=new Thread(()=>{
            MainWindow? window=null;
            try {
                var app=Application.Current ?? new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source=new Uri("/DotNetCad.Demo;component/Themes/DarkCadTheme.xaml",UriKind.Relative) });
                window=new MainWindow { EnableMcpHost=false, WindowStartupLocation=WindowStartupLocation.Manual, Left=0,Top=0 };
                var vm=(MainViewModel)window.DataContext; vm.UnsavedChangesPrompt=()=>MessageBoxResult.No;
                for(int i=0;i<60;i++) vm.RunCadCommand("LAYER Camada_de_teste_com_nome_longo_"+i);
                vm.AppendConsole(new string('X',400));
                window.Show(); window.UpdateLayout();
                var root=(FrameworkElement)window.Content;
                var viewport=(CadViewport)window.FindName("Viewport");
                var console=(Expander)window.FindName("ConsoleExpander");
                var dir=System.IO.Path.Combine(AppContext.BaseDirectory,"layout-validation");System.IO.Directory.CreateDirectory(dir);
                var log=new List<string>();
                foreach(var scale in new[]{1.0,1.5,2.0}) foreach(var size in new[]{(640,480),(800,600),(1100,720)}) {
                    root.LayoutTransform=Transform.Identity; window.Width=size.Item1;window.Height=size.Item2;window.UpdateLayout();
                    // At high simulated DPI, persistent panel headers must still allow reclaiming canvas space.
                    console.IsExpanded=false;window.UpdateLayout();
                    Assert.True(viewport.ActualWidth>0 && viewport.ActualHeight>0,$"No viewport at {size}/{scale}");
                    var bitmap=new RenderTargetBitmap((int)(window.ActualWidth*scale),(int)(window.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(window);
                    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using(var file=System.IO.File.Create(System.IO.Path.Combine(dir,$"{size.Item1}x{size.Item2}-{scale}.png")))encoder.Save(file);
                    log.Add($"Window {window.ActualWidth}x{window.ActualHeight}; render DPI scale {scale}; viewport {viewport.ActualWidth}x{viewport.ActualHeight}; native DPI {VisualTreeHelper.GetDpi(window).PixelsPerInchX}; console collapsed");
                    console.IsExpanded=true;window.UpdateLayout();
                }
                root.LayoutTransform=Transform.Identity;window.WindowState=WindowState.Maximized;window.UpdateLayout();
                log.Add($"Maximized: {window.ActualWidth}x{window.ActualHeight}; viewport {viewport.ActualWidth}x{viewport.ActualHeight}");
                System.IO.File.WriteAllLines(System.IO.Path.Combine(dir,"results.txt"),log);
            } catch(Exception ex){error=ex;} finally {window?.Close();}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error!=null)throw error;
    }
}

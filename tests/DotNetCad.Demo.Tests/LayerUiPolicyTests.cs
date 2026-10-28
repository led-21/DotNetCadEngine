using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.History;
using DotNetCad.Demo.Controls;
using DotNetCad.Demo.ViewModels;
using Xunit;
namespace DotNetCad.Demo.Tests;
public class LayerUiPolicyTests
{
    [Fact]
    public void PanelConsoleAndMcpUseTheSameLayerRules()
    {
        Sta(()=>{
            var vm=new MainViewModel();
            vm.NewLayerInput="Camada com espaços";vm.AddLayerCommand.Execute(null);
            Assert.Contains(vm.Layers,l=>l.Name=="Camada com espaços");
            var active=vm.Document.ActiveLayer;
            vm.SelectedLayer=vm.Layers.Single(l=>l.Name=="Camada com espaços");
            Assert.Equal(active,vm.Document.ActiveLayer);
            vm.SelectedLayer.ActivateCommand.Execute(null);Assert.Equal("Camada com espaços",vm.Document.ActiveLayer);
            vm.RunCadCommand("LINE 0;0 1;0");var entity=vm.Document.Entities.Last();entity.IsSelected=true;
            Assert.Equal(1,vm.Layers.Single(l=>l.Name=="Camada com espaços").EntityCount);
            vm.Layers.Single(l=>l.Name=="Camada com espaços").IsLocked=true;
            var count=vm.UndoManager.UndoCount;
            Assert.False(vm.RunCadCommand($"MOVE {entity.Id} 1;0",fromMcp:true).Success);
            vm.DeleteSelectedCommand.Execute(null);Assert.Contains(entity,vm.Document.Entities);Assert.Equal(count,vm.UndoManager.UndoCount);
            Assert.True(vm.RunCadCommand("LAYER UNLOCK \"Camada com espaços\"",true).Success);
            vm.LayerFilter="com espaços";Assert.Single(vm.FilteredLayers.Cast<object>());
            foreach(var layer in vm.Layers.Where(l=>l.Name!="0").ToArray())layer.IsVisible=false;
            var zero=vm.Layers.Single(l=>l.Name=="0");count=vm.UndoManager.UndoCount;zero.IsVisible=false;
            var checkbox=new System.Windows.Controls.CheckBox {DataContext=zero};
            checkbox.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,new System.Windows.Data.Binding(nameof(LayerItemViewModel.IsVisible)) {Mode=System.Windows.Data.BindingMode.TwoWay,UpdateSourceTrigger=System.Windows.Data.UpdateSourceTrigger.PropertyChanged});
            checkbox.IsChecked=false;
            Assert.True(checkbox.IsChecked);
            Assert.True(zero.IsVisible);Assert.False(vm.RunCadCommand("LAYER HIDE 0",true).Success);Assert.Equal(count,vm.UndoManager.UndoCount);
            vm.ShowAllLayersCommand.Execute(null);Assert.Equal(count+1,vm.UndoManager.UndoCount);
            vm.UndoCommand.Execute(null);Assert.Single(vm.Document.Layers,l=>l.IsVisible);
        });
    }
    [Fact]
    public void LockedLayerBlocksViewportDeleteGripsAndDrawing()
    {
        Sta(()=>{
            using var source=new HwndSource(new HwndSourceParameters("Layer policy") { Width=400,Height=300 });
            var d=new CadDocument();var h=new UndoRedoManager();var line=new CadLine(new(0,0),new(1,0));d.AddEntity(line);
            var vp=new CadViewport {Document=d,UndoManager=h};source.RootVisual=vp;
            line.IsSelected=true;d.GetLayer("Geometry")!.IsLocked=true;vp.UpdateGrips();
            var grips=(System.Collections.ICollection)typeof(CadViewport).GetField("_currentGrips",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(vp)!;
            Assert.Empty(grips.Cast<object>());
            typeof(CadViewport).GetMethod("OnKeyDown",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(vp,[new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.Delete){RoutedEvent=Keyboard.KeyDownEvent}]);
            Assert.Single(d.Entities);Assert.Equal(0,h.UndoCount);
            vp.ActiveTool=CadToolType.Line;
            for(int i=0;i<2;i++)typeof(CadViewport).GetMethod("OnMouseDown",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(vp,[new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=Mouse.MouseDownEvent}]);
            Assert.Single(d.Entities);Assert.Equal(0,h.UndoCount);
        });
    }
    private static void Sta(Action action)
    {
        Exception? error=null;var thread=new Thread(()=>{try{action();}catch(Exception ex){error=ex;}});
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error!=null)throw error;
    }
}

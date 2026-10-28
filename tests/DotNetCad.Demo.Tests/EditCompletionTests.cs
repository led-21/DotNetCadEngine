using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Core.History;
using DotNetCad.Demo.Controls;
using Xunit;
namespace DotNetCad.Demo.Tests;
public class EditCompletionTests
{
    [Theory]
    [InlineData(CadToolType.Move)][InlineData(CadToolType.Copy)][InlineData(CadToolType.Rotate)][InlineData(CadToolType.Offset)]
    public void LastPointCompletesWithoutExtraEnter(CadToolType tool)
    {
        Sta(()=>{
            using var host=new HwndSource(new HwndSourceParameters("CAD editing") {Width=600,Height=360});
            var doc=new CadDocument();var history=new UndoRedoManager();var line=new CadLine(new(1,0),new(3,0));doc.AddEntity(line);
            var vp=new CadViewport {Document=doc,UndoManager=history,Width=600,Height=360};host.RootVisual=vp;line.IsSelected=true;vp.ActiveTool=tool;
            var session=(CadModificationSession)typeof(CadViewport).GetField("_modification",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(vp)!;
            if(tool==CadToolType.Offset) {Pick(vp,new(0,0));Pick(vp,new(0,2));}else Pick(vp,new(0,0));
            var target=tool==CadToolType.Rotate?new Point2D(0,2):new Point2D(2,2);
            session.Hover(target);vp.Measure(new Size(600,360));vp.Arrange(new Rect(0,0,600,360));vp.InvalidateVisual();vp.UpdateLayout();
            Assert.Equal(0,history.UndoCount);Assert.Equal(new Point2D(1,0),line.StartPoint);
            var bitmap=new RenderTargetBitmap(600,360,96,96,PixelFormats.Pbgra32);bitmap.Render(vp);
            var dir=System.IO.Path.Combine(AppContext.BaseDirectory,"edit-validation");System.IO.Directory.CreateDirectory(dir);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=System.IO.File.Create(System.IO.Path.Combine(dir,tool+"-preview.png")))encoder.Save(file);
            Pick(vp,target);Assert.Equal(1,history.UndoCount);Assert.Equal(CadToolType.Select,vp.ActiveTool);
            if(tool==CadToolType.Rotate){Assert.Equal(0,line.StartPoint.X,8);Assert.Equal(1,line.StartPoint.Y,8);}
            if(tool==CadToolType.Move)Assert.Equal(new Point2D(3,2),line.StartPoint);
            if(tool is CadToolType.Copy or CadToolType.Offset)Assert.Equal(2,doc.Entities.Count);
            history.Undo();Assert.Single(doc.Entities);Assert.Equal(new Point2D(1,0),line.StartPoint);history.Redo();Assert.Equal(1,history.UndoCount);
        });
    }
    [Theory]
    [InlineData(true)][InlineData(false)]
    public void MirrorOptionIsAParameterAndCompletesWithoutEnter(bool keep)
    {
        Sta(()=>{
            using var host=new HwndSource(new HwndSourceParameters("CAD mirror") {Width=400,Height=300});
            var d=new CadDocument();var h=new UndoRedoManager();var line=new CadLine(new(1,0),new(2,0));d.AddEntity(line);
            var vp=new CadViewport {Document=d,UndoManager=h};host.RootVisual=vp;line.IsSelected=true;vp.ActiveTool=CadToolType.Mirror;
            Pick(vp,new(0,0));Pick(vp,new(1,1));Assert.Equal(0,h.UndoCount);
            var menu=vp.ContextMenu!;((MenuItem)menu.Items[keep?0:1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));menu.IsOpen=false;
            Assert.Equal(1,h.UndoCount);Assert.Equal(CadToolType.Select,vp.ActiveTool);Assert.Equal(keep?2:1,d.Entities.Count);
            h.Undo();Assert.Single(d.Entities);Assert.Equal(new Point2D(1,0),line.StartPoint);h.Redo();Assert.Equal(keep?2:1,d.Entities.Count);
        });
    }
    [Theory]
    [InlineData("2,35",true)][InlineData("NaN",false)]
    public void PreselectedOffsetAcceptsTypedDistanceAndSideClickWithoutEnter(string input,bool valid)
    {
        Sta(()=>{
            using var host=new HwndSource(new HwndSourceParameters("Offset without Enter"){Width=400,Height=300});
            var d=new CadDocument();var h=new UndoRedoManager();var line=new CadLine(new(0,1000000),new(5,1000000));d.AddEntity(line);
            var vp=new CadViewport{Document=d,UndoManager=h};host.RootVisual=vp;line.IsSelected=true;vp.ActiveTool=CadToolType.Offset;
            var session=(CadModificationSession)typeof(CadViewport).GetField("_modification",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(vp)!;
            Assert.Equal(1,session.Step); // preselection is accepted immediately
            typeof(CadViewport).GetMethod("OnTextInput",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(vp,[new System.Windows.Input.TextCompositionEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,new System.Windows.Input.TextComposition(System.Windows.Input.InputManager.Current,vp,input)){RoutedEvent=System.Windows.Input.TextCompositionManager.TextInputEvent}]);
            Assert.Equal(0,h.UndoCount);Assert.Equal(1,session.Step);
            if(valid){Assert.Single(session.Preview);Assert.False(session.IsReady);}
            typeof(CadViewport).GetMethod("OnMouseDown",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(vp,[new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Left){RoutedEvent=System.Windows.Input.Mouse.MouseDownEvent}]);
            Assert.Equal(valid?1:0,h.UndoCount);Assert.Equal(valid?2:1,d.Entities.Count);
            if(valid){Assert.Equal(2.35,Math.Abs(((CadLine)d.Entities.Last()).StartPoint.Y-line.StartPoint.Y),7);h.Undo();Assert.Single(d.Entities);h.Redo();Assert.Equal(2,d.Entities.Count);}
            else {Assert.Equal(1,session.Step);Assert.NotEmpty(session.Error);}
        });
    }
    private static void Pick(CadViewport v,Point2D p)=>typeof(CadViewport).GetMethod("AcceptModificationPoint",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(v,[p]);
    private static void Sta(Action action){Exception? error=null;var t=new Thread(()=>{try{action();}catch(Exception ex){error=ex;}});t.SetApartmentState(ApartmentState.STA);t.Start();t.Join();if(error!=null)throw error;}
}

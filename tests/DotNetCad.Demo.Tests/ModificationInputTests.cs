using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.History;
using DotNetCad.Demo.Controls;
using Xunit;
namespace DotNetCad.Demo.Tests;
public class ModificationInputTests
{
    [Fact]
    public void KeyboardEntryConfirmationAndEscapeAreIsolated()
    {
        Exception? error = null;
        var t = new Thread(() => {
            try {
                using var source = new HwndSource(new HwndSourceParameters("CAD test") { Width=400,Height=300 });
                var d = new CadDocument(); var h = new UndoRedoManager();
                var line = new CadLine(new(0,0),new(1,0)); d.AddEntity(line);
                var vp = new CadViewport { Document=d, UndoManager=h };
                source.RootVisual = vp; line.IsSelected=true;
                void Key(Key key) => typeof(CadViewport).GetMethod("OnKeyDown",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(vp,[new KeyEventArgs(Keyboard.PrimaryDevice,source,0,key) { RoutedEvent=Keyboard.KeyDownEvent }]);
                void Text(string text) => typeof(CadViewport).GetMethod("OnTextInput",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(vp,[new TextCompositionEventArgs(Keyboard.PrimaryDevice,new TextComposition(InputManager.Current,vp,text)) { RoutedEvent=TextCompositionManager.TextInputEvent }]);
                vp.ActiveTool=CadToolType.Move;
                Text("0;0"); Key(System.Windows.Input.Key.Enter);
                Text("@2,35;0"); Key(System.Windows.Input.Key.Enter);
                Assert.Equal(0,line.StartPoint.X);
                Key(System.Windows.Input.Key.Enter); Assert.Equal(2.35,line.StartPoint.X,8);
                Assert.Equal(1,h.UndoCount);
                vp.ActiveTool=CadToolType.Copy;
                var click = new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left) { RoutedEvent=Mouse.MouseDownEvent };
                typeof(CadViewport).GetMethod("OnMouseDown",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(vp,[click]);
                Assert.True(click.Handled);
                var session = (CadModificationSession)typeof(CadViewport).GetField("_modification",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(vp)!;
                Assert.Equal(2,session.Step);
                Assert.Equal(1,h.UndoCount);
                Key(System.Windows.Input.Key.Escape);
                vp.ActiveTool=CadToolType.Text;
                Text("Anotação real"); Key(System.Windows.Input.Key.Enter);
                Text("NaN"); Key(System.Windows.Input.Key.Enter);
                Assert.Equal(1,(int)typeof(CadViewport).GetField("_textStep",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(vp)!);
                Key(System.Windows.Input.Key.Escape);
                vp.ActiveTool=CadToolType.Text;
                Text("Anotação real"); Key(System.Windows.Input.Key.Enter);
                Text("0,25"); Key(System.Windows.Input.Key.Enter);
                Assert.Single(d.Entities);
                typeof(CadViewport).GetMethod("OnMouseDown",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(vp,[new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left) { RoutedEvent=Mouse.MouseDownEvent }]);
                Assert.Single(d.Entities);
                Key(System.Windows.Input.Key.Enter);
                var annotation=Assert.IsType<CadText>(d.Entities.Last());
                Assert.Equal("Anotação real",annotation.Text); Assert.Equal(.25,annotation.Height);
                h.Undo(); Assert.Single(d.Entities); h.Redo(); Assert.Equal(2,d.Entities.Count);
                h.Undo();
                foreach (var stage in new[]{0,1,2}) {
                    vp.ActiveTool=CadToolType.Mirror;
                    if(stage>0) { Text("0;0"); Key(System.Windows.Input.Key.Enter); }
                    if(stage>1) { Text("1;1"); Key(System.Windows.Input.Key.Enter); }
                    Key(System.Windows.Input.Key.Escape);
                    Assert.Equal(1,h.UndoCount); Assert.Equal(2.35,line.StartPoint.X,8);
                }
            } catch(Exception ex) { error=ex; }
        });
        t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
        if(error!=null) throw error;
    }
}

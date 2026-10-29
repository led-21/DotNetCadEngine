using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.History;
using DotNetCad.Demo.Controls;
using DotNetCad.Demo.Views;
using Xunit;

namespace DotNetCad.Demo.Tests;

public class TextInsertionTests
{
    private static void Sta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (error != null) throw error;
    }

    [Fact]
    public void PrintableKeysAreNotSwallowedByTextTool() => Sta(() =>
    {
        using var source = new HwndSource(new HwndSourceParameters("Text input test") { Width=400,Height=300 });
        var viewport = new CadViewport { Document=new CadDocument(), UndoManager=new UndoRedoManager(), ActiveTool=CadToolType.Text };
        source.RootVisual = viewport;
        foreach (var key in new[] {Key.A, Key.Space, Key.D2, Key.OemComma})
        {
            var input = new KeyEventArgs(Keyboard.PrimaryDevice,source,0,key) {RoutedEvent=Keyboard.KeyDownEvent};
            typeof(CadViewport).GetMethod("OnKeyDown",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(viewport,[input]);
            Assert.False(input.Handled);
        }
    });

    [Fact]
    public void DialogTextIsPlacedByMouseAndSupportsUndoRedoAndCancel() => Sta(() =>
    {
        using var source = new HwndSource(new HwndSourceParameters("Text placement test") { Width=400,Height=300 });
        var document = new CadDocument(); var undo = new UndoRedoManager();
        var viewport = new CadViewport {Document=document,UndoManager=undo}; source.RootVisual=viewport;
        viewport.BeginTextInsertion("Anotação da Mútua",.25);
        Assert.Empty(document.Entities); // Accepting the dialog only prepares placement.
        typeof(CadViewport).GetMethod("OnMouseDown",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(viewport,
            [new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left) {RoutedEvent=Mouse.MouseDownEvent}]);
        var text = Assert.IsType<CadText>(Assert.Single(document.Entities));
        Assert.Equal("Anotação da Mútua",text.Text); Assert.Equal(.25,text.Height);
        Assert.Equal(CadToolType.Select,viewport.ActiveTool);
        undo.Undo(); Assert.Empty(document.Entities); undo.Redo(); Assert.Single(document.Entities);
        viewport.BeginTextInsertion("Não inserir",1); viewport.CancelOperation();
        Assert.Single(document.Entities); Assert.Equal(1,undo.UndoCount);
    });

    [Fact]
    public void DialogAcceptsTextAndDecimalComma() => Sta(() =>
    {
        var dialog = new TextEntryDialog {WindowStartupLocation=WindowStartupLocation.Manual,Left=-10000,Top=-10000};
        ((TextBox)dialog.FindName("ContentInput")).Text="Silo da Mútua";
        ((TextBox)dialog.FindName("HeightInput")).Text="0,25";
        dialog.Loaded += (_,_) => typeof(TextEntryDialog).GetMethod("ConfirmClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(dialog,[dialog,new RoutedEventArgs()]);
        Assert.True(dialog.ShowDialog());
        Assert.Equal("Silo da Mútua",dialog.AnnotationText);
        Assert.Equal(.25,dialog.AnnotationHeight);
    });

    [Fact]
    public void DialogRejectsEmptyTextAndInvalidHeight() => Sta(() =>
    {
        var dialog = new TextEntryDialog();
        void Confirm() => typeof(TextEntryDialog).GetMethod("ConfirmClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(dialog,[dialog,new RoutedEventArgs()]);
        Confirm();
        Assert.Contains("Digite",((TextBlock)dialog.FindName("ValidationMessage")).Text);
        ((TextBox)dialog.FindName("ContentInput")).Text="Silo 01";
        ((TextBox)dialog.FindName("HeightInput")).Text="NaN";
        Confirm();
        Assert.Contains("altura válida",((TextBlock)dialog.FindName("ValidationMessage")).Text);
        dialog.Close();
    });
}

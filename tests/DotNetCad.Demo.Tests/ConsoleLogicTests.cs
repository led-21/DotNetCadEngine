using DotNetCad.Core.Editor;
using DotNetCad.Demo;
using DotNetCad.Demo.ViewModels;
using Xunit;
namespace DotNetCad.Demo.Tests;
public class ConsoleLogicTests
{
    [Fact]
    public void NavigationPreservesDraftAndDoesNotMixMcpOutputWithInput()
    {
        var m=new CadConsoleModel();m.Remember("LINE 0;0 1;0");m.Remember("LIST");m.Add("MCP result",ConsoleOrigin.MCP);
        Assert.Equal("LIST",m.Previous("rascunho não enviado"));Assert.Equal("LINE 0;0 1;0",m.Previous("LIST"));
        Assert.Equal("LIST",m.Next("LINE 0;0 1;0"));Assert.Equal("rascunho não enviado",m.Next("LIST"));
        Assert.Equal(2,m.Commands.Count);Assert.Single(m.Messages);
        m.ClearOutput();Assert.Empty(m.Messages);Assert.Equal(2,m.Commands.Count);
    }
    [Fact]
    public void MessageAndInputMemoryAreBoundedAndExportIsReadable()
    {
        var m=new CadConsoleModel();for(int i=0;i<2000;i++){m.Add(new string('X',3000)+i);m.Remember("LIST "+i);}
        Assert.True(m.Messages.Count<=CadConsoleModel.MessageLimit);Assert.True(m.Messages.Sum(x=>x.Text.Length)<=CadConsoleModel.CharacterLimit);
        Assert.Equal(CadConsoleModel.CommandLimit,m.Commands.Count);
        m.Add("erro de teste",ConsoleOrigin.MCP,ConsoleSeverity.Erro);Assert.Contains("[MCP] [Erro] erro de teste",m.Export());
        m.Add(new string('Z',100000));Assert.True(m.Messages.Last().Text.Length<33000);
    }
    [Fact]
    public void CatalogIsSharedByHelpCompletionAndHints()
    {
        Assert.Equal(CadCommandCatalog.Help,CadCommandProcessor.Help);
        foreach(var syntax in CadCommandCatalog.Syntax) {var name=syntax.Split(' ')[0];Assert.Contains(name,CadCommandCatalog.Complete(name));Assert.Contains(syntax,CadCommandCatalog.Hint(name));}
        Assert.Contains("LAYER DELETE",CadCommandCatalog.Hint("LAYER D"));Assert.True(CadCommandCatalog.IsReadOnly("PROPERTIES id"));Assert.False(CadCommandCatalog.IsReadOnly("CLEAR"));
    }
    [Fact]
    public void ErrorKeepsInputAndClearOutputDoesNotChangeDrawingOrHistory()
    {
        Sta(()=>{
            var vm=new MainViewModel();vm.RunCadCommand("LINE 0;0 1;0");var entityCount=vm.Document.Entities.Count;var undo=vm.UndoManager.UndoCount;
            vm.ConsoleInput="LINE inválida";Assert.False(vm.ExecuteConsoleEntry().Success);Assert.Equal("LINE inválida",vm.ConsoleInput);
            vm.Console.ClearOutput();Assert.Empty(vm.Console.Messages);Assert.Equal(undo,vm.UndoManager.UndoCount);Assert.Equal(entityCount,vm.Document.Entities.Count);
            vm.ConsoleInput="LIST";Assert.True(vm.ExecuteConsoleEntry().Success);Assert.Equal("",vm.ConsoleInput);
            vm.InteractiveOperationActive=()=>true;
            Assert.False(vm.RunCadCommand("CLEAR",true).Success);Assert.False(vm.RunCadCommand("UNDO").Success);
            Assert.True(vm.RunCadCommand("LIST",true).Success);Assert.Equal(entityCount,vm.Document.Entities.Count);Assert.Equal(undo,vm.UndoManager.UndoCount);
            Assert.False(vm.ReplaceFromFile("irrelevant.cad").Success);
        });
    }
    [Fact]
    public void PreferencesPersistOnlyLayoutAndHandleCorruption()
    {
        var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),Guid.NewGuid()+".json");
        try {new CadLayoutPreferences(250,false,200,false).Save(path);var json=System.IO.File.ReadAllText(path);Assert.DoesNotContain("LINE",json);Assert.Equal(250,CadLayoutPreferences.Load(path).ConsoleHeight);
            System.IO.File.WriteAllText(path,"invalid");Assert.Equal(new CadLayoutPreferences(),CadLayoutPreferences.Load(path));}
        finally{System.IO.File.Delete(path);}
    }
    [Fact]
    public void ToggleConsoleCommand_SwitchesVisibilityAndLabel()
    {
        Sta(() =>
        {
            var vm = new MainViewModel();
            Assert.True(vm.IsConsoleVisible);
            Assert.Equal("Ocultar Console (F2)", vm.ConsoleVisibilityLabel);

            // Toggle to false
            vm.ToggleConsoleCommand.Execute(null);
            Assert.False(vm.IsConsoleVisible);
            Assert.Equal("Exibir Console (F2)", vm.ConsoleVisibilityLabel);

            // Toggle back to true
            vm.ToggleConsoleCommand.Execute(null);
            Assert.True(vm.IsConsoleVisible);
            Assert.Equal("Ocultar Console (F2)", vm.ConsoleVisibilityLabel);
        });
    }

    [Fact]
    public void ConsoleCommand_ControlsVisibility()
    {
        Sta(() =>
        {
            var vm = new MainViewModel();
            Assert.True(vm.IsConsoleVisible);

            // Command CONSOLE OFF / HIDE
            var r1 = vm.RunCadCommand("CONSOLE OFF");
            Assert.True(r1.Success);
            Assert.False(vm.IsConsoleVisible);

            // Command CONSOLE ON / SHOW
            var r2 = vm.RunCadCommand("CONSOLE ON");
            Assert.True(r2.Success);
            Assert.True(vm.IsConsoleVisible);

            // Command CONSOLE (toggle)
            var r3 = vm.RunCadCommand("CONSOLE");
            Assert.True(r3.Success);
            Assert.False(vm.IsConsoleVisible);

            var r4 = vm.RunCadCommand("TOGGLECONSOLE");
            Assert.True(r4.Success);
            Assert.True(vm.IsConsoleVisible);
        });
    }

    [Fact]
    public void Preferences_PersistsConsoleVisibility()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            new CadLayoutPreferences(220, true, 190, true, IsConsoleVisible: false).Save(path);
            var loaded = CadLayoutPreferences.Load(path);
            Assert.False(loaded.IsConsoleVisible);
            Assert.Equal(220, loaded.ConsoleHeight);

            new CadLayoutPreferences(220, true, 190, true, IsConsoleVisible: true).Save(path);
            var loaded2 = CadLayoutPreferences.Load(path);
            Assert.True(loaded2.IsConsoleVisible);
        }
        finally
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    private static void Sta(Action action){Exception? error=null;var t=new Thread(()=>{try{action();}catch(Exception ex){error=ex;}});t.SetApartmentState(ApartmentState.STA);t.Start();t.Join();if(error!=null)throw error;}
}

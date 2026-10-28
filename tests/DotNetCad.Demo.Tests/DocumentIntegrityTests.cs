using System.Windows;
using DotNetCad.Core.Entities;
using DotNetCad.Demo.ViewModels;
using Xunit;
namespace DotNetCad.Demo.Tests;
public class DocumentIntegrityTests
{
    [Fact]
    public void ReplacementAndCloseRespectCancelAndSaveFailure()
    {
        Exception? error=null;
        var thread=new Thread(()=>{
            var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),Guid.NewGuid()+".cad");
            try {
                var vm=new MainViewModel(); var doc=vm.Document;
                vm.RunCadCommand("LINE 0;0 1;0"); var history=vm.UndoManager.UndoCount;
                vm.UnsavedChangesPrompt=()=>MessageBoxResult.Cancel;
                Assert.False(vm.ConfirmPendingChanges()); vm.NewDrawingCommand.Execute(null); vm.LoadSampleDrawingCommand.Execute(null);
                Assert.Same(doc,vm.Document); Assert.Equal(history,vm.UndoManager.UndoCount);
                vm.UnsavedChangesPrompt=()=>MessageBoxResult.Yes; vm.SavePathPrompt=()=>null;
                Assert.False(vm.ConfirmPendingChanges());
                vm.SavePathPrompt=()=>System.IO.Path.Combine(path,"missing","drawing.cad");
                Assert.False(vm.ConfirmPendingChanges()); Assert.True(vm.HasUnsavedChanges);
                Assert.False(vm.ReplaceFromFile(path).Success); Assert.Same(doc,vm.Document); Assert.Equal(history,vm.UndoManager.UndoCount);
                Assert.True(vm.RunCadCommand("SAVE "+path).Success); Assert.False(vm.HasUnsavedChanges);
                vm.Document.Entities[0].IsSelected=true; Assert.False(vm.HasUnsavedChanges);
                vm.RunCadCommand("LINE 2;0 3;0"); Assert.True(vm.HasUnsavedChanges);
                vm.RunCadCommand("UNDO"); Assert.False(vm.HasUnsavedChanges); vm.RunCadCommand("REDO"); Assert.True(vm.HasUnsavedChanges);
                vm.UnsavedChangesPrompt=()=>MessageBoxResult.Cancel;
                Assert.False(vm.ReplaceFromFile(path).Success); Assert.Same(doc,vm.Document);
                vm.UnsavedChangesPrompt=()=>MessageBoxResult.No;
                Assert.True(vm.ReplaceFromFile(path).Success); Assert.NotSame(doc,vm.Document); Assert.False(vm.HasUnsavedChanges); Assert.Equal(0,vm.UndoManager.UndoCount);
                vm.RunCadCommand("LINE 4;0 5;0"); Assert.True(vm.RunCadCommand("SAVE").Success);
            } catch(Exception ex){error=ex;} finally {System.IO.File.Delete(path);}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error!=null)throw error;
    }
}

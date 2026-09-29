using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DotNetCad.Core.History;

public class UndoRedoManager : INotifyPropertyChanged
{
    private const int MaxUndoLimit = 100;
    private Stack<IUndoableAction> _undoStack = new();
    private Stack<IUndoableAction> _redoStack = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;

    public string UndoDescription => CanUndo ? _undoStack.Peek().Description : string.Empty;
    public string RedoDescription => CanRedo ? _redoStack.Peek().Description : string.Empty;

    public void ExecuteAction(IUndoableAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action.Execute();
        _undoStack.Push(action);
        _redoStack.Clear();
        EnforceLimit();
        NotifyAll();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var action = _undoStack.Pop();
        try
        {
            action.Undo();
            _redoStack.Push(action);
        }
        catch
        {
            _undoStack.Push(action);
            throw;
        }
        finally
        {
            NotifyAll();
        }
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var action = _redoStack.Pop();
        try
        {
            action.Execute();
            _undoStack.Push(action);
            EnforceLimit();
        }
        catch
        {
            _redoStack.Push(action);
            throw;
        }
        finally
        {
            NotifyAll();
        }
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        NotifyAll();
    }

    private void EnforceLimit()
    {
        if (_undoStack.Count > MaxUndoLimit)
        {
            var arr = _undoStack.ToArray();
            Array.Reverse(arr);
            _undoStack = new Stack<IUndoableAction>(arr.Skip(arr.Length - MaxUndoLimit));
        }
    }

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoCount));
        OnPropertyChanged(nameof(RedoCount));
        OnPropertyChanged(nameof(UndoDescription));
        OnPropertyChanged(nameof(RedoDescription));
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

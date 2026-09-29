namespace DotNetCad.Core.History;

public sealed class CompositeUndoableAction : IUndoableAction
{
    private readonly List<IUndoableAction> _actions;
    private readonly string _description;

    public CompositeUndoableAction(IEnumerable<IUndoableAction> actions, string description = "Composite Action")
    {
        ArgumentNullException.ThrowIfNull(actions);
        _actions = actions.ToList();
        _description = description;
    }

    public string Description => _description;

    public void Execute()
    {
        var executed = new List<IUndoableAction>(_actions.Count);
        try
        {
            foreach (var action in _actions)
            {
                action.Execute();
                executed.Add(action);
            }
        }
        catch
        {
            // Reverse atomic rollback of already executed sub-actions
            for (int i = executed.Count - 1; i >= 0; i--)
            {
                try
                {
                    executed[i].Undo();
                }
                catch
                {
                    // Preserve original exception
                }
            }
            throw;
        }
    }

    public void Undo()
    {
        for (int i = _actions.Count - 1; i >= 0; i--)
        {
            _actions[i].Undo();
        }
    }
}

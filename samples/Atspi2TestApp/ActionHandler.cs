using Avalonia.DBus.Wire;
using static Atspi2TestApp.Program;

namespace Atspi2TestApp;

internal sealed class ActionHandler : OrgA11yAtspiActionHandler
{
    private readonly AtspiServer _server;
    private readonly AccessibleNode _node;

    public ActionHandler(AtspiServer server, AccessibleNode node)
    {
        _server = server;
        _node = node;
        Version = ActionVersion;
        NActions = node.Action == null ? 0 : 1;
    }

    public override Connection Connection => _server.A11yConnection;

    protected override ValueTask<string> OnGetDescriptionAsync(Message request, int index)
    {
        return ValueTask.FromResult(_node.Action?.Description ?? string.Empty);
    }

    protected override ValueTask<string> OnGetNameAsync(Message request, int index)
    {
        return ValueTask.FromResult(_node.Action?.Name ?? string.Empty);
    }

    protected override ValueTask<string> OnGetLocalizedNameAsync(Message request, int index)
    {
        return ValueTask.FromResult(_node.Action?.LocalizedName ?? string.Empty);
    }

    protected override ValueTask<string> OnGetKeyBindingAsync(Message request, int index)
    {
        return ValueTask.FromResult(_node.Action?.KeyBinding ?? string.Empty);
    }

    protected override ValueTask<(string, string, string)[]> OnGetActionsAsync(Message request)
    {
        if (_node.Action == null)
        {
            return ValueTask.FromResult(Array.Empty<(string, string, string)>());
        }

        return ValueTask.FromResult(new[]
        {
            (_node.Action.LocalizedName, _node.Action.Description, _node.Action.KeyBinding)
        });
    }

    protected override ValueTask<bool> OnDoActionAsync(Message request, int index)
    {
        if (_node.Role == RoleCheckBox)
        {
            if (!_node.States.Add(StateChecked))
            {
                _node.States.Remove(StateChecked);
            }
        }

        return ValueTask.FromResult(true);
    }
}

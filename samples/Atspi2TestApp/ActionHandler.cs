using Avalonia.DBus.SourceGen;
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

    public override DBusConnection Connection => _server.A11yConnection;

    protected override ValueTask<string> OnGetDescriptionAsync(DBusMessage request, int index)
    {
        return ValueTask.FromResult(_node.Action?.Description ?? string.Empty);
    }

    protected override ValueTask<string> OnGetNameAsync(DBusMessage request, int index)
    {
        return ValueTask.FromResult(_node.Action?.Name ?? string.Empty);
    }

    protected override ValueTask<string> OnGetLocalizedNameAsync(DBusMessage request, int index)
    {
        return ValueTask.FromResult(_node.Action?.LocalizedName ?? string.Empty);
    }

    protected override ValueTask<string> OnGetKeyBindingAsync(DBusMessage request, int index)
    {
        return ValueTask.FromResult(_node.Action?.KeyBinding ?? string.Empty);
    }

    protected override ValueTask<DBusArray<DBusStruct>> OnGetActionsAsync(DBusMessage request)
    {
        if (_node.Action == null)
        {
            return ValueTask.FromResult(new DBusArray<DBusStruct>("(sss)"));
        }

        var entry = new DBusStruct(_node.Action.LocalizedName, _node.Action.Description, _node.Action.KeyBinding);
        return ValueTask.FromResult(new DBusArray<DBusStruct>(entry));
    }

    protected override ValueTask<bool> OnDoActionAsync(DBusMessage request, int index)
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

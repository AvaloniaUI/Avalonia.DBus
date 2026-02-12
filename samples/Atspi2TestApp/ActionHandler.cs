using static Atspi2TestApp.Program;

using Atspi2TestApp.DBusXml;
namespace Atspi2TestApp;

internal sealed class ActionHandler : IOrgA11yAtspiAction
{
    private readonly AccessibleNode _node;

    public ActionHandler(AtspiServer server, AccessibleNode node)
    {
        _ = server;
        _node = node;
    }

    public uint Version => ActionVersion;

    public int NActions => _node.Action == null ? 0 : 1;

    public ValueTask<string> GetDescriptionAsync(int index)
    {
        return ValueTask.FromResult(_node.Action?.LocalizedDescription ?? string.Empty);
    }

    public ValueTask<string> GetNameAsync(int index)
    {
        return ValueTask.FromResult(_node.Action?.LocalizedName ?? string.Empty);
    }

    public ValueTask<string> GetLocalizedNameAsync(int index)
    {
        return ValueTask.FromResult(_node.Action?.LocalizedName ?? string.Empty);
    }

    public ValueTask<string> GetKeyBindingAsync(int index)
    {
        return ValueTask.FromResult(_node.Action?.KeyBinding ?? string.Empty);
    }

    public ValueTask<List<AtSpiAction>> GetActionsAsync()
    {
        if (_node.Action == null)
            return ValueTask.FromResult(new List<AtSpiAction>());

        return ValueTask.FromResult(new List<AtSpiAction> { _node.Action });
    }

    public ValueTask<bool> DoActionAsync(int index)
    {
        if (_node.Role == RoleCheckBox)
        {
            if (!_node.States.Add(StateChecked))
                _node.States.Remove(StateChecked);
        }

        return ValueTask.FromResult(true);
    }
}

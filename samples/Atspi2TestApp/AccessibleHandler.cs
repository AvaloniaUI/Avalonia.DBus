using Avalonia.DBus;
using Avalonia.DBus.SourceGen;
using static Atspi2TestApp.Program;

namespace Atspi2TestApp;

internal sealed class AccessibleHandler : OrgA11yAtspiAccessibleHandler
{
    private readonly AtspiServer _server;
    private readonly AccessibleNode _node;

    public AccessibleHandler(AtspiServer server, AccessibleNode node)
    {
        _server = server;
        _node = node;
        RefreshProperties();
    }

    public override DBusConnection Connection => _server.A11yConnection;

    public void RefreshProperties()
    {
        Version = AccessibleVersion;
        Name = _node.Name;
        Description = _node.Description;
        Parent = _server.GetReference(_node.Parent);
        ChildCount = _node.Children.Count;
        Locale = _node.Locale;
        AccessibleId = _node.AccessibleId;
        HelpText = _node.HelpText;
    }

    protected override ValueTask<DbusStruct_Rsoz> OnGetChildAtIndexAsync(DBusMessage request, int index)
    {
        var child = index >= 0 && index < _node.Children.Count ? _node.Children[index] : null;
        return ValueTask.FromResult(_server.GetReference(child));
    }

    protected override ValueTask<List<DbusStruct_Rsoz>> OnGetChildrenAsync(DBusMessage request)
    {
        if (_node.Children.Count == 0)
        {
            return ValueTask.FromResult(new List<DbusStruct_Rsoz>());
        }

        var children = new List<DbusStruct_Rsoz>(_node.Children.Count);
        for (var i = 0; i < _node.Children.Count; i++)
        {
            children.Add(_server.GetReference(_node.Children[i]));
        }

        return ValueTask.FromResult(children);
    }

    protected override ValueTask<int> OnGetIndexInParentAsync(DBusMessage request)
    {
        var index = _node.Parent == null ? -1 : _node.Parent.Children.IndexOf(_node);
        return ValueTask.FromResult(index);
    }

    protected override ValueTask<List<DbusStruct_Ruarsozz>> OnGetRelationSetAsync(DBusMessage request)
    {
        return ValueTask.FromResult(AtspiServer.s_emptyRelations);
    }

    protected override ValueTask<uint> OnGetRoleAsync(DBusMessage request)
    {
        return ValueTask.FromResult((uint)_node.Role);
    }

    protected override ValueTask<string> OnGetRoleNameAsync(DBusMessage request)
    {
        return ValueTask.FromResult(_server.GetRoleName(_node.Role));
    }

    protected override ValueTask<string> OnGetLocalizedRoleNameAsync(DBusMessage request)
    {
        return ValueTask.FromResult(_server.GetRoleName(_node.Role));
    }

    protected override ValueTask<List<uint>> OnGetStateAsync(DBusMessage request)
    {
        return ValueTask.FromResult(BuildStateSet(_node.States));
    }

    protected override ValueTask<Dictionary<string, string>> OnGetAttributesAsync(DBusMessage request)
    {
        return ValueTask.FromResult(new Dictionary<string, string>());
    }

    protected override ValueTask<DbusStruct_Rsoz> OnGetApplicationAsync(DBusMessage request)
    {
        return ValueTask.FromResult(_server.GetReference(_server.Tree.Root));
    }

    protected override ValueTask<List<string>> OnGetInterfacesAsync(DBusMessage request)
    {
        if (_node.Interfaces.Count == 0)
        {
            return ValueTask.FromResult(new List<string>());
        }

        var interfaces = _node.Interfaces.OrderBy(static iface => iface, StringComparer.Ordinal).ToArray();
        return ValueTask.FromResult(new List<string>(interfaces));
    }
}

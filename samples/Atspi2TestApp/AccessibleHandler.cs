using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.DBus.SourceGen;
using Avalonia.DBus.Wire;
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

    protected override ValueTask<DBusStruct> OnGetChildAtIndexAsync(DBusMessage request, int index)
    {
        var child = index >= 0 && index < _node.Children.Count ? _node.Children[index] : null;
        return ValueTask.FromResult(_server.GetReference(child));
    }

    protected override ValueTask<DBusArray<DBusStruct>> OnGetChildrenAsync(DBusMessage request)
    {
        if (_node.Children.Count == 0)
        {
            return ValueTask.FromResult(new DBusArray<DBusStruct>());
        }

        var children = new DBusStruct[_node.Children.Count];
        for (var i = 0; i < _node.Children.Count; i++)
        {
            children[i] = _server.GetReference(_node.Children[i]);
        }

        return ValueTask.FromResult(new DBusArray<DBusStruct>(children));
    }

    protected override ValueTask<int> OnGetIndexInParentAsync(DBusMessage request)
    {
        var index = _node.Parent == null ? -1 : _node.Parent.Children.IndexOf(_node);
        return ValueTask.FromResult(index);
    }

    protected override ValueTask<DBusArray<DBusStruct>> OnGetRelationSetAsync(DBusMessage request)
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

    protected override ValueTask<DBusArray<uint>> OnGetStateAsync(DBusMessage request)
    {
        return ValueTask.FromResult(BuildStateSet(_node.States));
    }

    protected override ValueTask<DBusDict<string, string>> OnGetAttributesAsync(DBusMessage request)
    {
        return ValueTask.FromResult(new DBusDict<string, string>());
    }

    protected override ValueTask<DBusStruct> OnGetApplicationAsync(DBusMessage request)
    {
        return ValueTask.FromResult(_server.GetReference(_server.Tree.Root));
    }

    protected override ValueTask<DBusArray<string>> OnGetInterfacesAsync(DBusMessage request)
    {
        if (_node.Interfaces.Count == 0)
        {
            return ValueTask.FromResult(new DBusArray<string>());
        }

        var interfaces = _node.Interfaces.OrderBy(static iface => iface, StringComparer.Ordinal).ToArray();
        return ValueTask.FromResult(new DBusArray<string>(interfaces));
    }
}

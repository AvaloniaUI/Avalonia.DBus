using System;
using System.Collections.Generic;
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

    public override Connection Connection => _server.A11yConnection;

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

    protected override ValueTask<(string, ObjectPath)> OnGetChildAtIndexAsync(Message request, int index)
    {
        var child = index >= 0 && index < _node.Children.Count ? _node.Children[index] : null;
        return ValueTask.FromResult(_server.GetReference(child));
    }

    protected override ValueTask<(string, ObjectPath)[]> OnGetChildrenAsync(Message request)
    {
        if (_node.Children.Count == 0)
        {
            return ValueTask.FromResult(Array.Empty<(string, ObjectPath)>());
        }

        var children = new (string, ObjectPath)[_node.Children.Count];
        for (var i = 0; i < _node.Children.Count; i++)
        {
            children[i] = _server.GetReference(_node.Children[i]);
        }

        return ValueTask.FromResult(children);
    }

    protected override ValueTask<int> OnGetIndexInParentAsync(Message request)
    {
        var index = _node.Parent == null ? -1 : _node.Parent.Children.IndexOf(_node);
        return ValueTask.FromResult(index);
    }

    protected override ValueTask<(uint, (string, ObjectPath)[])[]> OnGetRelationSetAsync(Message request)
    {
        return ValueTask.FromResult(AtspiServer.s_emptyRelations);
    }

    protected override ValueTask<uint> OnGetRoleAsync(Message request)
    {
        return ValueTask.FromResult((uint)_node.Role);
    }

    protected override ValueTask<string> OnGetRoleNameAsync(Message request)
    {
        return ValueTask.FromResult(_server.GetRoleName(_node.Role));
    }

    protected override ValueTask<string> OnGetLocalizedRoleNameAsync(Message request)
    {
        return ValueTask.FromResult(_server.GetRoleName(_node.Role));
    }

    protected override ValueTask<uint[]> OnGetStateAsync(Message request)
    {
        return ValueTask.FromResult(BuildStateSet(_node.States));
    }

    protected override ValueTask<Dictionary<string, string>> OnGetAttributesAsync(Message request)
    {
        return ValueTask.FromResult(new Dictionary<string, string>(StringComparer.Ordinal));
    }

    protected override ValueTask<(string, ObjectPath)> OnGetApplicationAsync(Message request)
    {
        return ValueTask.FromResult(_server.GetReference(_server.Tree.Root));
    }

    protected override ValueTask<string[]> OnGetInterfacesAsync(Message request)
    {
        if (_node.Interfaces.Count == 0)
        {
            return ValueTask.FromResult(Array.Empty<string>());
        }

        var interfaces = new string[_node.Interfaces.Count];
        var index = 0;
        foreach (var iface in _node.Interfaces)
        {
            interfaces[index++] = iface;
        }

        return ValueTask.FromResult(interfaces);
    }
}

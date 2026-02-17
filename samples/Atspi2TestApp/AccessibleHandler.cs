using Atspi2TestApp.DBusXml;
using static Atspi2TestApp.Program;

namespace Atspi2TestApp;

internal sealed class AccessibleHandler(AtspiServer server, AccessibleNode node) : IOrgA11yAtspiAccessible
{
    public uint Version => AccessibleVersion;

    public string Name => node.Name;

    public string Description => node.Description;

    public AtSpiObjectReference Parent => server.GetReference(node.Parent);

    public int ChildCount => node.Children.Count;

    public string Locale => node.Locale;

    public string AccessibleId => node.AccessibleId;

    public string HelpText => node.HelpText;

    public ValueTask<AtSpiObjectReference> GetChildAtIndexAsync(int index)
    {
        var child = index >= 0 && index < node.Children.Count ? node.Children[index] : null;
        return ValueTask.FromResult(server.GetReference(child));
    }

    public ValueTask<List<AtSpiObjectReference>> GetChildrenAsync()
    {
        if (node.Children.Count == 0)
            return ValueTask.FromResult(new List<AtSpiObjectReference>());

        var children = new List<AtSpiObjectReference>(node.Children.Count);
        for (var i = 0; i < node.Children.Count; i++)
            children.Add(server.GetReference(node.Children[i]));

        return ValueTask.FromResult(children);
    }

    public ValueTask<int> GetIndexInParentAsync()
    {
        var index = node.Parent == null ? -1 : node.Parent.Children.IndexOf(node);
        return ValueTask.FromResult(index);
    }

    public ValueTask<List<AtSpiRelationEntry>> GetRelationSetAsync()
    {
        return ValueTask.FromResult(AtspiServer.EmptyRelations);
    }

    public ValueTask<uint> GetRoleAsync() => ValueTask.FromResult((uint)node.Role);

    public ValueTask<string> GetRoleNameAsync() => ValueTask.FromResult(server.GetRoleName(node.Role));

    public ValueTask<string> GetLocalizedRoleNameAsync() => ValueTask.FromResult(server.GetRoleName(node.Role));

    public ValueTask<List<uint>> GetStateAsync() => ValueTask.FromResult(BuildStateSet(node.States));

    public ValueTask<AtSpiAttributeSet> GetAttributesAsync() => ValueTask.FromResult(new AtSpiAttributeSet());

    public ValueTask<AtSpiObjectReference> GetApplicationAsync()
    {
        return ValueTask.FromResult(server.GetReference(server.Tree.Root));
    }

    public ValueTask<List<string>> GetInterfacesAsync()
    {
        if (node.Interfaces.Count == 0)
            return ValueTask.FromResult(new List<string>());

        var interfaces = node.Interfaces.OrderBy(static iface => iface, StringComparer.Ordinal).ToArray();
        return ValueTask.FromResult(new List<string>(interfaces));
    }
}

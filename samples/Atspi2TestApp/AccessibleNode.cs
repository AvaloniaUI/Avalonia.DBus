using Atspi2TestApp.DBusXml;

namespace Atspi2TestApp;

internal sealed class AccessibleNode(string path, string name, int role)
{
    public string Path { get; } = path;
    public string Name { get; } = name;
    public string Description { get; set; } = string.Empty;
    public string Locale { get; set; } = "en_US";
    public string AccessibleId { get; set; } = string.Empty;
    public string HelpText { get; set; } = string.Empty;
    public int Role { get; } = role;
    public int? ApplicationId { get; set; }
    public AccessibleNode? Parent { get; set; }
    public List<AccessibleNode> Children { get; } = [];
    public HashSet<uint> States { get; } = [];
    public HashSet<string> Interfaces { get; } = [];
    public Rect Extents { get; set; } = new Rect(0, 0, 0, 0);
    public AtSpiAction? Action { get; set; }
    public ValueInfo? Value { get; set; }
    public NodeHandlers? Handlers { get; set; }
}

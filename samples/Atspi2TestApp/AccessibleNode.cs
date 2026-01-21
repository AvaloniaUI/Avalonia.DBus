namespace Atspi2TestApp;

internal static partial class Program
{
    private sealed class AccessibleNode
    {
        public AccessibleNode(string path, string name, int role)
        {
            Path = path;
            Name = name;
            Role = role;
        }

        public string Path { get; }
        public string Name { get; }
        public string Description { get; set; } = string.Empty;
        public string Locale { get; set; } = "en_US";
        public string AccessibleId { get; set; } = string.Empty;
        public string HelpText { get; set; } = string.Empty;
        public int Role { get; }
        public int? ApplicationId { get; set; }
        public AccessibleNode? Parent { get; set; }
        public List<AccessibleNode> Children { get; } = new();
        public HashSet<uint> States { get; } = new();
        public HashSet<string> Interfaces { get; } = new();
        public Rect Extents { get; set; } = new Rect(0, 0, 0, 0);
        public ActionInfo? Action { get; set; }
        public ValueInfo? Value { get; set; }
    }
}

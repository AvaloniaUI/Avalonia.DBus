namespace Atspi2TestApp;

internal sealed class ActionInfo
{
    public ActionInfo(string name, string localizedName, string description)
    {
        Name = name;
        LocalizedName = localizedName;
        Description = description;
    }

    public string Name { get; }
    public string LocalizedName { get; }
    public string Description { get; }
    public string KeyBinding { get; set; } = string.Empty;
}

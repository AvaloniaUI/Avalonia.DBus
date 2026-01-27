using System;
using System.Collections.Generic;
using static Atspi2TestApp.Program;

namespace Atspi2TestApp;

internal sealed class AtspiTree
{
    private readonly string _locale;

    public AtspiTree()
    {
        _locale = ResolveLocale();
        Root = BuildTree();
        IndexTree(Root, null);
    }

    public AccessibleNode Root { get; }
    public AccessibleNode StaticWindow { get; private set; } = null!;
    public AccessibleNode ToggleWindow { get; private set; } = null!;
    public Dictionary<string, AccessibleNode> NodesByPath { get; } = new(StringComparer.Ordinal);
    public bool IsToggleWindowAttached => Root.Children.Contains(ToggleWindow);

    public int GetToggleWindowIndex()
    {
        return Root.Children.IndexOf(ToggleWindow);
    }

    public void RemoveToggleWindow()
    {
        if (!Root.Children.Remove(ToggleWindow))
        {
            return;
        }

        ToggleWindow.Parent = null;
        RemoveFromIndex(ToggleWindow);
    }

    public void AddToggleWindow()
    {
        if (Root.Children.Contains(ToggleWindow))
        {
            return;
        }

        var insertIndex = Root.Children.Contains(StaticWindow)
            ? Root.Children.IndexOf(StaticWindow) + 1
            : Root.Children.Count;
        if (insertIndex < 0 || insertIndex > Root.Children.Count)
        {
            insertIndex = Root.Children.Count;
        }

        Root.Children.Insert(insertIndex, ToggleWindow);
        IndexTree(ToggleWindow, Root);
    }

    private AccessibleNode BuildTree()
    {
        var root = CreateNode("AT-SPI2 Test App", RoleApplication, isRoot: true);
        root.Description = "AT-SPI2 test application root";
        root.AccessibleId = "app-root";
        root.HelpText = "Root object for the AT-SPI2 test application";
        root.Extents = new Rect(0, 0, 0, 0);
        root.Interfaces.UnionWith(new[] { IfaceAccessible, IfaceApplication });
        root.States.UnionWith(new[] { StateEnabled, StateSensitive, StateVisible, StateShowing });

        var staticWindow = CreateNode("Test Window", RoleFrame);
        staticWindow.Description = "Main window";
        staticWindow.AccessibleId = "main-window";
        staticWindow.Extents = new Rect(100, 100, 480, 320);
        StaticWindow = staticWindow;
        staticWindow.Interfaces.UnionWith(new[] { IfaceAccessible, IfaceComponent });
        staticWindow.States.UnionWith(new[] { StateActive, StateEnabled, StateSensitive, StateVisible, StateShowing });

        var toggleWindow = CreateNode("Recurring Window", RoleFrame);
        toggleWindow.Description = "Recurring window (cycle 0)";
        toggleWindow.AccessibleId = "recurring-window";
        toggleWindow.Extents = new Rect(640, 120, 360, 220);
        ToggleWindow = toggleWindow;
        toggleWindow.Interfaces.UnionWith(new[] { IfaceAccessible, IfaceComponent });
        toggleWindow.States.UnionWith(new[] { StateEnabled, StateSensitive, StateVisible, StateShowing });

        var label = CreateNode("Name:", RoleLabel);
        label.Description = "Label for the name entry";
        label.AccessibleId = "name-label";
        label.Extents = new Rect(120, 140, 80, 24);
        label.Interfaces.UnionWith(new[] { IfaceAccessible, IfaceComponent });
        label.States.UnionWith(new[] { StateEnabled, StateSensitive, StateVisible, StateShowing });

        var entry = CreateNode("Name Entry", RoleEntry);
        entry.Description = "Editable text entry";
        entry.AccessibleId = "name-entry";
        entry.Extents = new Rect(210, 136, 240, 32);
        entry.Interfaces.UnionWith(new[] { IfaceAccessible, IfaceComponent });
        entry.States.UnionWith(new[]
        {
            StateEnabled,
            StateSensitive,
            StateVisible,
            StateShowing,
            StateFocusable,
            StateFocused,
            StateEditable
        });

        var checkBox = CreateNode("Enable Feature", RoleCheckBox);
        checkBox.Description = "Toggles the feature";
        checkBox.AccessibleId = "feature-checkbox";
        checkBox.Extents = new Rect(120, 190, 200, 28);
        checkBox.Interfaces.UnionWith(new[] { IfaceAccessible, IfaceComponent, IfaceAction });
        checkBox.States.UnionWith(new[]
        {
            StateEnabled,
            StateSensitive,
            StateVisible,
            StateShowing,
            StateFocusable,
            StateCheckable,
            StateChecked
        });
        checkBox.Action = new ActionInfo("toggle", "Toggle", "Toggles the checkbox state");

        var button = CreateNode("Submit", RoleButton);
        button.Description = "Submit button";
        button.AccessibleId = "submit-button";
        button.Extents = new Rect(120, 230, 120, 36);
        button.Interfaces.UnionWith(new[] { IfaceAccessible, IfaceComponent, IfaceAction });
        button.States.UnionWith(new[]
        {
            StateEnabled,
            StateSensitive,
            StateVisible,
            StateShowing,
            StateFocusable
        });
        button.Action = new ActionInfo("click", "Click", "Clicks the submit button");

        var slider = CreateNode("Volume", RoleSlider);
        slider.Description = "Volume slider";
        slider.AccessibleId = "volume-slider";
        slider.Extents = new Rect(120, 280, 240, 28);
        slider.Interfaces.UnionWith(new[] { IfaceAccessible, IfaceComponent, IfaceValue });
        slider.States.UnionWith(new[]
        {
            StateEnabled,
            StateSensitive,
            StateVisible,
            StateShowing,
            StateFocusable
        });
        slider.Value = new ValueInfo(0, 100, 65, 1, "65 percent");

        root.Children.Add(staticWindow);
        staticWindow.Parent = root;
        root.Children.Add(toggleWindow);
        toggleWindow.Parent = root;

        staticWindow.Children.Add(label);
        label.Parent = staticWindow;
        staticWindow.Children.Add(entry);
        entry.Parent = staticWindow;
        staticWindow.Children.Add(checkBox);
        checkBox.Parent = staticWindow;
        staticWindow.Children.Add(button);
        button.Parent = staticWindow;
        staticWindow.Children.Add(slider);
        slider.Parent = staticWindow;

        return root;
    }

    private AccessibleNode CreateNode(string name, int role, bool isRoot = false)
    {
        var path = isRoot ? RootPath : $"{AppPathPrefix}/{Guid.NewGuid().ToString(\"D\").Replace('-', '_')}";
        var node = new AccessibleNode(path, name, role)
        {
            Locale = _locale
        };
        return node;
    }

    private void IndexTree(AccessibleNode node, AccessibleNode? parent)
    {
        node.Parent = parent ?? node.Parent;
        NodesByPath[node.Path] = node;
        foreach (var child in node.Children)
        {
            IndexTree(child, node);
        }
    }

    private void RemoveFromIndex(AccessibleNode node)
    {
        NodesByPath.Remove(node.Path);
        foreach (var child in node.Children)
        {
            RemoveFromIndex(child);
        }
    }
}

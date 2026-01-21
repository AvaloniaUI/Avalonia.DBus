using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.DBus.AutoGen;
using Avalonia.DBus.SourceGen;
using Avalonia.DBus.Wire;

namespace Atspi2TestApp;

internal static partial class Program
{
    private static readonly bool s_verbose = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ATSPI_VERBOSE"));
    private static readonly Stopwatch s_uptime = Stopwatch.StartNew();

    private const string BasePath = "/org/a11y/atspi/accessible";
    private const string RootPath = BasePath + "/root";
    private const string NullPath = "/org/a11y/atspi/null";

    private const string IfaceAccessible = "org.a11y.atspi.Accessible";
    private const string IfaceApplication = "org.a11y.atspi.Application";
    private const string IfaceComponent = "org.a11y.atspi.Component";
    private const string IfaceAction = "org.a11y.atspi.Action";
    private const string IfaceValue = "org.a11y.atspi.Value";

    private const string BusNameRegistry = "org.a11y.atspi.Registry";

    private const string BusNameA11y = "org.a11y.Bus";
    private const string PathA11y = "/org/a11y/bus";

    private const uint AccessibleVersion = 1;
    private const uint ApplicationVersion = 1;
    private const uint ComponentVersion = 1;
    private const uint ActionVersion = 1;
    private const uint ValueVersion = 1;
    private const uint EventObjectVersion = 1;

    private const int RoleApplication = 75;
    private const int RoleFrame = 23;
    private const int RoleLabel = 29;
    private const int RoleEntry = 79;
    private const int RoleCheckBox = 7;
    private const int RoleButton = 43;
    private const int RoleSlider = 51;

    private const uint StateActive = 1;
    private const uint StateEnabled = 8;
    private const uint StateFocusable = 11;
    private const uint StateFocused = 12;
    private const uint StateEditable = 7;
    private const uint StateSensitive = 24;
    private const uint StateShowing = 25;
    private const uint StateVisible = 30;
    private const uint StateCheckable = 41;
    private const uint StateChecked = 4;

    private sealed class ActionInfo
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

    private sealed class ValueInfo
    {
        public ValueInfo(double minimum, double maximum, double current, double increment, string text)
        {
            Minimum = minimum;
            Maximum = maximum;
            Current = current;
            Increment = increment;
            Text = text;
        }

        public double Minimum { get; }
        public double Maximum { get; }
        public double Current { get; set; }
        public double Increment { get; }
        public string Text { get; }
    }

    private sealed class AtspiTree
    {
        public AtspiTree()
        {
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
            var root = new AccessibleNode(RootPath, "AT-SPI2 Test App", RoleApplication)
            {
                Description = "AT-SPI2 test application root",
                AccessibleId = "app-root",
                HelpText = "Root object for the AT-SPI2 test application",
                Extents = new Rect(0, 0, 0, 0)
            };
            root.Interfaces.UnionWith(new[] { IfaceAccessible, IfaceApplication });
            root.States.UnionWith(new[] { StateEnabled, StateSensitive, StateVisible, StateShowing });

            var staticWindow = new AccessibleNode("/org/a11y/atspi/accessible/window", "Test Window", RoleFrame)
            {
                Description = "Main window",
                AccessibleId = "main-window",
                Extents = new Rect(100, 100, 480, 320)
            };
            StaticWindow = staticWindow;
            staticWindow.Interfaces.UnionWith(new[] { IfaceAccessible, IfaceComponent });
            staticWindow.States.UnionWith(new[] { StateActive, StateEnabled, StateSensitive, StateVisible, StateShowing });

            var toggleWindow = new AccessibleNode("/org/a11y/atspi/accessible/recurring_window", "Recurring Window", RoleFrame)
            {
                Description = "Recurring window (cycle 0)",
                AccessibleId = "recurring-window",
                Extents = new Rect(640, 120, 360, 220)
            };
            ToggleWindow = toggleWindow;
            toggleWindow.Interfaces.UnionWith(new[] { IfaceAccessible, IfaceComponent });
            toggleWindow.States.UnionWith(new[] { StateEnabled, StateSensitive, StateVisible, StateShowing });

            var label = new AccessibleNode("/org/a11y/atspi/accessible/label", "Name:", RoleLabel)
            {
                Description = "Label for the name entry",
                AccessibleId = "name-label",
                Extents = new Rect(120, 140, 80, 24)
            };
            label.Interfaces.UnionWith(new[] { IfaceAccessible, IfaceComponent });
            label.States.UnionWith(new[] { StateEnabled, StateSensitive, StateVisible, StateShowing });

            var entry = new AccessibleNode("/org/a11y/atspi/accessible/entry", "Name Entry", RoleEntry)
            {
                Description = "Editable text entry",
                AccessibleId = "name-entry",
                Extents = new Rect(210, 136, 240, 32)
            };
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

            var checkBox = new AccessibleNode("/org/a11y/atspi/accessible/checkbox", "Enable Feature", RoleCheckBox)
            {
                Description = "Toggles the feature",
                AccessibleId = "feature-checkbox",
                Extents = new Rect(120, 190, 200, 28)
            };
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

            var button = new AccessibleNode("/org/a11y/atspi/accessible/button", "Submit", RoleButton)
            {
                Description = "Submit button",
                AccessibleId = "submit-button",
                Extents = new Rect(120, 230, 120, 36)
            };
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

            var slider = new AccessibleNode("/org/a11y/atspi/accessible/slider", "Volume", RoleSlider)
            {
                Description = "Volume slider",
                AccessibleId = "volume-slider",
                Extents = new Rect(120, 280, 240, 28)
            };
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

    private sealed class AtspiServer
    {
        private const int WindowToggleIntervalMs = 3000;

        private static readonly (uint, (string, ObjectPath)[])[] s_emptyRelations = Array.Empty<(uint, (string, ObjectPath)[])>();

        private readonly AtspiTree _tree;
        private readonly Dictionary<int, string> _roleNames = new();
        private readonly object _treeGate = new();
        private readonly Dictionary<string, NodeHandlers> _handlersByPath = new(StringComparer.Ordinal);
        private readonly PathTree _pathTree;

        private Connection? _a11yConnection;
        private string _a11yAddress = string.Empty;
        private string _uniqueName = string.Empty;
        private bool _running;
        private int _windowToggleCounter;
        private readonly System.Threading.ManualResetEventSlim _shutdownEvent = new(false);
        private System.Threading.Timer? _toggleTimer;
        private PosixSignalRegistration? _sigintRegistration;
        private PosixSignalRegistration? _sigtermRegistration;
        private System.Threading.Timer? _forceExitTimer;

        public AtspiServer(AtspiTree tree)
        {
            _tree = tree;
            _pathTree = new PathTree(BasePath);
            _roleNames[RoleApplication] = "application";
            _roleNames[RoleFrame] = "frame";
            _roleNames[RoleLabel] = "label";
            _roleNames[RoleEntry] = "entry";
            _roleNames[RoleCheckBox] = "check box";
            _roleNames[RoleButton] = "button";
            _roleNames[RoleSlider] = "slider";
        }

        public int Run()
        {
            _running = true;
            LogVerbose("Server starting");

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                RequestShutdown();
            };

            LogVerbose("Registering signal handlers");
            RegisterSignalHandlers();

            LogVerbose("Connecting to accessibility bus");
            if (!TryConnect())
            {
                Cleanup();
                return 1;
            }

            BuildHandlers();

            LogVerbose("Registering object paths");
            if (!RegisterObjectPaths())
            {
                Cleanup();
                return 1;
            }

            LogVerbose("Embedding application");
            if (!EmbedApplication())
            {
                Cleanup();
                return 1;
            }

            StartToggleLoop();
            Console.WriteLine($"AT-SPI2 test app registered on {_uniqueName}");
            Console.WriteLine("Press Ctrl+C to exit.");
            LogVerbose("Waiting for shutdown");

            _shutdownEvent.Wait();

            Cleanup();
            return 0;
        }

        private void BuildHandlers()
        {
            _handlersByPath.Clear();
            foreach (var node in _tree.NodesByPath.Values)
            {
                var handlers = new NodeHandlers(node, _pathTree.AddPath(node.Path));

                if (node.Interfaces.Contains(IfaceAccessible))
                {
                    var handler = new AccessibleHandler(this, node);
                    handlers.AccessibleHandler = handler;
                    handlers.Add(handler);
                }

                if (node.Interfaces.Contains(IfaceApplication))
                {
                    var handler = new ApplicationHandler(this, node);
                    handlers.ApplicationHandler = handler;
                    handlers.Add(handler);
                }

                if (node.Interfaces.Contains(IfaceComponent))
                {
                    var handler = new ComponentHandler(this, node);
                    handlers.ComponentHandler = handler;
                    handlers.Add(handler);
                }

                if (node.Interfaces.Contains(IfaceAction))
                {
                    var handler = new ActionHandler(this, node);
                    handlers.ActionHandler = handler;
                    handlers.Add(handler);
                }

                if (node.Interfaces.Contains(IfaceValue))
                {
                    var handler = new ValueHandler(this, node);
                    handlers.ValueHandler = handler;
                    handlers.Add(handler);
                }

                handlers.EventObjectHandler = new EventObjectHandler(this);
                handlers.Add(handlers.EventObjectHandler);

                _handlersByPath[node.Path] = handlers;
            }

            RefreshAllAccessibleHandlers();
        }

        private void RefreshAllAccessibleHandlers()
        {
            foreach (var handler in _handlersByPath.Values)
            {
                handler.AccessibleHandler?.RefreshProperties();
            }
        }

        private void RefreshAccessibleHandler(AccessibleNode node)
        {
            if (_handlersByPath.TryGetValue(node.Path, out var handler))
            {
                handler.AccessibleHandler?.RefreshProperties();
            }
        }

        private bool TryConnect()
        {
            var sw = Stopwatch.StartNew();
            LogVerbose("TryConnect start");
            var address = GetAccessibilityBusAddress();
            if (string.IsNullOrWhiteSpace(address))
            {
                Console.Error.WriteLine("Failed to resolve the accessibility bus address.");
                return false;
            }
            _a11yAddress = address;

            try
            {
                LogVerbose("Opening private accessibility bus connection");
                _a11yConnection = new Connection(address);
                _uniqueName = _a11yConnection.UniqueName;
                LogVerbose($"TryConnect end ({sw.ElapsedMilliseconds} ms)");
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to open accessibility bus: {ex.Message}");
                LogVerbose($"TryConnect failed after {sw.ElapsedMilliseconds} ms");
                _a11yConnection = null;
                return false;
            }
        }

        private string GetAccessibilityBusAddress()
        {
            var sw = Stopwatch.StartNew();
            LogVerbose("GetAddress start");
            try
            {
                LogVerbose("Connecting to session bus for org.a11y.Bus");
                using var connection = new Connection(DBusBusType.DBUS_BUS_SESSION);
                var proxy = new OrgA11yBusProxy(connection, BusNameA11y, PathA11y);
                LogVerbose("Calling org.a11y.Bus.GetAddress");
                return proxy.GetAddressAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"GetAddress call failed: {ex.Message}");
                return string.Empty;
            }
            finally
            {
                LogVerbose($"GetAddress end ({sw.ElapsedMilliseconds} ms)");
            }
        }

        private bool RegisterObjectPaths()
        {
            var sw = Stopwatch.StartNew();
            if (_a11yConnection == null)
            {
                Console.Error.WriteLine("Missing accessibility bus connection.");
                return false;
            }

            try
            {
                _a11yConnection.RegisterPathHandler(_pathTree);
                LogVerbose($"RegisterObjectPaths end ({sw.ElapsedMilliseconds} ms)");
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to register object path fallback: {ex.Message}");
                return false;
            }
        }

        private bool EmbedApplication()
        {
            if (string.IsNullOrWhiteSpace(_a11yAddress))
            {
                Console.Error.WriteLine("Missing accessibility bus address.");
                return false;
            }

            var sw = Stopwatch.StartNew();
            LogVerbose("EmbedApplication start");
            try
            {
                LogVerbose("Opening registry connection for Embed");
                using var connection = new Connection(_a11yAddress);
                var proxy = new OrgA11yAtspiSocketProxy(connection, BusNameRegistry, RootPath);
                LogVerbose("Calling org.a11y.atspi.Socket.Embed");
                var reply = proxy.EmbedAsync((_uniqueName, new ObjectPath(RootPath))).GetAwaiter().GetResult();
                Console.WriteLine($"Registry root: {reply.Item1} {reply.Item2}");
                LogVerbose($"EmbedApplication end ({sw.ElapsedMilliseconds} ms)");
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Embed call failed: {ex.Message}");
                LogVerbose($"EmbedApplication failed after {sw.ElapsedMilliseconds} ms");
                return false;
            }
        }

        private void Cleanup()
        {
            if (_toggleTimer != null)
            {
                _toggleTimer.Dispose();
                _toggleTimer = null;
            }

            if (_a11yConnection != null)
            {
                _a11yConnection.Dispose();
                _a11yConnection = null;
            }

            _a11yAddress = string.Empty;

            _sigintRegistration?.Dispose();
            _sigintRegistration = null;
            _sigtermRegistration?.Dispose();
            _sigtermRegistration = null;
            _forceExitTimer?.Dispose();
            _forceExitTimer = null;
        }

        private void RegisterSignalHandlers()
        {
            _sigintRegistration = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
            {
                context.Cancel = true;
                RequestShutdown();
            });
            _sigtermRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                RequestShutdown();
            });
        }

        private void RequestShutdown()
        {
            _running = false;
            _shutdownEvent.Set();
            _forceExitTimer ??= new System.Threading.Timer(
                _ => Environment.Exit(0),
                null,
                2000,
                System.Threading.Timeout.Infinite);
        }

        private void StartToggleLoop()
        {
            _toggleTimer = new System.Threading.Timer(
                _ => ToggleWindow(),
                null,
                WindowToggleIntervalMs,
                WindowToggleIntervalMs);
        }

        private void ToggleWindow()
        {
            if (!_running || _a11yConnection == null)
            {
                return;
            }

            lock (_treeGate)
            {
                if (_tree.IsToggleWindowAttached)
                {
                    var index = _tree.GetToggleWindowIndex();
                    _tree.RemoveToggleWindow();
                    _pathTree.RemovePath(_tree.ToggleWindow.Path);
                    RefreshAccessibleHandler(_tree.Root);
                    RefreshAccessibleHandler(_tree.ToggleWindow);
                    EmitChildrenChanged(_tree.Root, "remove", index < 0 ? 0 : index, _tree.ToggleWindow);
                }
                else
                {
                    _windowToggleCounter++;
                    _tree.ToggleWindow.Description = $"Recurring window (cycle {_windowToggleCounter})";
                    _tree.AddToggleWindow();
                    _pathTree.AddPath(_tree.ToggleWindow.Path);
                    RefreshAccessibleHandler(_tree.Root);
                    RefreshAccessibleHandler(_tree.ToggleWindow);
                    var index = _tree.GetToggleWindowIndex();
                    EmitChildrenChanged(_tree.Root, "add", index < 0 ? 0 : index, _tree.ToggleWindow);
                    EmitPropertyChange(_tree.ToggleWindow, "accessible-description", _tree.ToggleWindow.Description);
                }
            }
        }

        private (string busName, ObjectPath path) GetReference(AccessibleNode? node)
        {
            if (node == null)
            {
                return (string.Empty, new ObjectPath(NullPath));
            }

            return (_uniqueName, new ObjectPath(node.Path));
        }

        private void EmitChildrenChanged(AccessibleNode parent, string operation, int index, AccessibleNode child)
        {
            if (_a11yConnection == null)
            {
                return;
            }

            if (!_handlersByPath.TryGetValue(parent.Path, out var handlers) || handlers.EventObjectHandler == null)
            {
                return;
            }

            var reference = GetReference(child);
            var childVariant = VariantValue.FromSignature("(so)", (reference.busName, reference.path));
            handlers.EventObjectHandler.EmitChildrenChangedSignal(operation, index, childVariant);
        }

        private void EmitPropertyChange(AccessibleNode node, string propertyName, string value)
        {
            if (_a11yConnection == null)
            {
                return;
            }

            if (!_handlersByPath.TryGetValue(node.Path, out var handlers) || handlers.EventObjectHandler == null)
            {
                return;
            }

            handlers.EventObjectHandler.EmitPropertyChangeSignal(propertyName, VariantValue.FromSignature("s", value));
        }

        private void SetFocused(AccessibleNode node)
        {
            foreach (var item in _tree.NodesByPath.Values)
            {
                item.States.Remove(StateFocused);
            }

            node.States.Add(StateFocused);
        }

        private static bool ContainsPoint(Rect rect, int x, int y)
        {
            return x >= rect.X && y >= rect.Y && x < rect.X + rect.Width && y < rect.Y + rect.Height;
        }

        private (int x, int y) TranslatePoint(AccessibleNode node, int x, int y, uint coordType)
        {
            if (coordType == 0)
            {
                return (x, y);
            }

            if (coordType == 1)
            {
                var topLevel = GetTopLevelWindow(node);
                if (topLevel != null)
                {
                    return (x + topLevel.Extents.X, y + topLevel.Extents.Y);
                }
            }

            if (coordType == 2 && node.Parent != null)
            {
                return (x + node.Parent.Extents.X, y + node.Parent.Extents.Y);
            }

            return (x, y);
        }

        private Rect TranslateRect(AccessibleNode node, uint coordType)
        {
            var rect = node.Extents;
            if (coordType == 0)
            {
                return rect;
            }

            if (coordType == 1)
            {
                var topLevel = GetTopLevelWindow(node);
                if (topLevel != null)
                {
                    return new Rect(rect.X - topLevel.Extents.X, rect.Y - topLevel.Extents.Y, rect.Width, rect.Height);
                }
            }

            if (coordType == 2 && node.Parent != null)
            {
                return new Rect(rect.X - node.Parent.Extents.X, rect.Y - node.Parent.Extents.Y, rect.Width, rect.Height);
            }

            return rect;
        }

        private AccessibleNode? FindAtPoint(AccessibleNode node, int x, int y)
        {
            foreach (var child in node.Children)
            {
                if (ContainsPoint(child.Extents, x, y))
                {
                    return FindAtPoint(child, x, y) ?? child;
                }
            }

            return ContainsPoint(node.Extents, x, y) ? node : null;
        }

        private AccessibleNode? GetTopLevelWindow(AccessibleNode node)
        {
            var current = node;
            while (current.Parent != null && current.Parent != _tree.Root)
            {
                current = current.Parent;
            }

            return current.Parent == _tree.Root ? current : null;
        }

        private sealed class NodeHandlers
        {
            public NodeHandlers(AccessibleNode node, PathHandler pathHandler)
            {
                Node = node;
                PathHandler = pathHandler;
            }

            public AccessibleNode Node { get; }
            public PathHandler PathHandler { get; }

            public AccessibleHandler? AccessibleHandler { get; set; }
            public ApplicationHandler? ApplicationHandler { get; set; }
            public ComponentHandler? ComponentHandler { get; set; }
            public ActionHandler? ActionHandler { get; set; }
            public ValueHandler? ValueHandler { get; set; }
            public EventObjectHandler? EventObjectHandler { get; set; }

            public void Add(IDBusInterfaceHandler handler)
            {
                PathHandler.Add(handler);
            }
        }

        private sealed class AccessibleHandler : OrgA11yAtspiAccessibleHandler
        {
            private readonly AtspiServer _server;
            private readonly AccessibleNode _node;

            public AccessibleHandler(AtspiServer server, AccessibleNode node)
            {
                _server = server;
                _node = node;
                RefreshProperties();
            }

            public override Connection Connection => _server._a11yConnection ?? throw new InvalidOperationException("Connection not initialized.");

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
                return ValueTask.FromResult(s_emptyRelations);
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
                if (_node.States.Count == 0)
                {
                    return ValueTask.FromResult(Array.Empty<uint>());
                }

                var states = new uint[_node.States.Count];
                var index = 0;
                foreach (var state in _node.States)
                {
                    states[index++] = state;
                }

                return ValueTask.FromResult(states);
            }

            protected override ValueTask<Dictionary<string, string>> OnGetAttributesAsync(Message request)
            {
                return ValueTask.FromResult(new Dictionary<string, string>(StringComparer.Ordinal));
            }

            protected override ValueTask<(string, ObjectPath)> OnGetApplicationAsync(Message request)
            {
                return ValueTask.FromResult(_server.GetReference(_server._tree.Root));
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

        private sealed class EventObjectHandler : OrgA11yAtspiEventObjectHandler
        {
            private readonly AtspiServer _server;

            public EventObjectHandler(AtspiServer server)
            {
                _server = server;
                Version = EventObjectVersion;
            }

            public override Connection Connection => _server._a11yConnection ?? throw new InvalidOperationException("Connection not initialized.");

            public void EmitChildrenChangedSignal(string operation, int indexInParent, VariantValue child)
            {
                EmitChildrenChanged(operation, indexInParent, 0, child, null);
            }

            public void EmitPropertyChangeSignal(string propertyName, VariantValue value)
            {
                EmitPropertyChange(propertyName, 0, 0, value, null);
            }
        }

        private sealed class ApplicationHandler : OrgA11yAtspiApplicationHandler
        {
            private readonly AtspiServer _server;
            private readonly AccessibleNode _node;

            public ApplicationHandler(AtspiServer server, AccessibleNode node)
            {
                _server = server;
                _node = node;
                ToolkitName = "Avalonia.DBus";
                Version = "1.0";
                ToolkitVersion = "1.0";
                AtspiVersion = "2.1";
                InterfaceVersion = ApplicationVersion;
            }

            public override Connection Connection => _server._a11yConnection ?? throw new InvalidOperationException("Connection not initialized.");

            public override int Id
            {
                get => _node.ApplicationId ?? 0;
                set => _node.ApplicationId = value;
            }

            protected override ValueTask<string> OnGetLocaleAsync(Message request, uint lctype)
            {
                return ValueTask.FromResult(string.Empty);
            }

            protected override ValueTask<string> OnGetApplicationBusAddressAsync(Message request)
            {
                return ValueTask.FromResult(string.Empty);
            }
        }

        private sealed class ComponentHandler : OrgA11yAtspiComponentHandler
        {
            private readonly AtspiServer _server;
            private readonly AccessibleNode _node;

            public ComponentHandler(AtspiServer server, AccessibleNode node)
            {
                _server = server;
                _node = node;
                Version = ComponentVersion;
            }

            public override Connection Connection => _server._a11yConnection ?? throw new InvalidOperationException("Connection not initialized.");

            protected override ValueTask<bool> OnContainsAsync(Message request, int x, int y, uint coordType)
            {
                var screenPoint = _server.TranslatePoint(_node, x, y, coordType);
                var contains = ContainsPoint(_node.Extents, screenPoint.x, screenPoint.y);
                return ValueTask.FromResult(contains);
            }

            protected override ValueTask<(string, ObjectPath)> OnGetAccessibleAtPointAsync(Message request, int x, int y, uint coordType)
            {
                var screenPoint = _server.TranslatePoint(_node, x, y, coordType);
                var target = _server.FindAtPoint(_node, screenPoint.x, screenPoint.y);
                return ValueTask.FromResult(_server.GetReference(target));
            }
 
            protected override ValueTask<(int , int , int, int )> OnGetExtentsAsync(Message request, uint coordType)
            {
                var rect = _server.TranslateRect(_node, coordType);
                return ValueTask.FromResult((rect.X, rect.Y, rect.Width, rect.Height));
            }

            
            
            protected override ValueTask<(int X, int Y)> OnGetPositionAsync(Message request, uint coordType)
            {
                var rect = _server.TranslateRect(_node, coordType);
                return ValueTask.FromResult((rect.X, rect.Y));
            }

            protected override ValueTask<(int Width, int Height)> OnGetSizeAsync(Message request)
            {
                return ValueTask.FromResult((_node.Extents.Width, _node.Extents.Height));
            }

            protected override ValueTask<uint> OnGetLayerAsync(Message request)
            {
                var layer = _node.Role == RoleFrame ? 7u : 3u;
                return ValueTask.FromResult(layer);
            }

            protected override ValueTask<short> OnGetMDIZOrderAsync(Message request)
            {
                return ValueTask.FromResult((short)-1);
            }

            protected override ValueTask<bool> OnGrabFocusAsync(Message request)
            {
                _server.SetFocused(_node);
                return ValueTask.FromResult(true);
            }

            protected override ValueTask<double> OnGetAlphaAsync(Message request)
            {
                return ValueTask.FromResult(1.0);
            }

            protected override ValueTask<bool> OnSetExtentsAsync(Message request, int x, int y, int width, int height, uint coordType)
            {
                return ValueTask.FromResult(false);
            }

            protected override ValueTask<bool> OnSetPositionAsync(Message request, int x, int y, uint coordType)
            {
                return ValueTask.FromResult(false);
            }

            protected override ValueTask<bool> OnSetSizeAsync(Message request, int width, int height)
            {
                return ValueTask.FromResult(false);
            }

            protected override ValueTask<bool> OnScrollToAsync(Message request, uint type)
            {
                return ValueTask.FromResult(false);
            }

            protected override ValueTask<bool> OnScrollToPointAsync(Message request, uint type, int x, int y)
            {
                return ValueTask.FromResult(false);
            }
        }

        private sealed class ActionHandler : OrgA11yAtspiActionHandler
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

            public override Connection Connection => _server._a11yConnection ?? throw new InvalidOperationException("Connection not initialized.");

            protected override ValueTask<string> OnGetDescriptionAsync(Message request, int index)
            {
                return ValueTask.FromResult(_node.Action?.Description ?? string.Empty);
            }

            protected override ValueTask<string> OnGetNameAsync(Message request, int index)
            {
                return ValueTask.FromResult(_node.Action?.Name ?? string.Empty);
            }

            protected override ValueTask<string> OnGetLocalizedNameAsync(Message request, int index)
            {
                return ValueTask.FromResult(_node.Action?.LocalizedName ?? string.Empty);
            }

            protected override ValueTask<string> OnGetKeyBindingAsync(Message request, int index)
            {
                return ValueTask.FromResult(_node.Action?.KeyBinding ?? string.Empty);
            }

            protected override ValueTask<(string, string, string)[]> OnGetActionsAsync(Message request)
            {
                if (_node.Action == null)
                {
                    return ValueTask.FromResult(Array.Empty<(string, string, string)>());
                }

                return ValueTask.FromResult(new[]
                {
                    (_node.Action.LocalizedName, _node.Action.Description, _node.Action.KeyBinding)
                });
            }

            protected override ValueTask<bool> OnDoActionAsync(Message request, int index)
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

        private sealed class ValueHandler : OrgA11yAtspiValueHandler
        {
            private readonly AtspiServer _server;
            private readonly AccessibleNode _node;

            public ValueHandler(AtspiServer server, AccessibleNode node)
            {
                _server = server;
                _node = node;
                Version = ValueVersion;
                MinimumValue = node.Value?.Minimum ?? 0;
                MaximumValue = node.Value?.Maximum ?? 0;
                MinimumIncrement = node.Value?.Increment ?? 0;
                Text = node.Value?.Text ?? string.Empty;
            }

            public override Connection Connection => _server._a11yConnection ?? throw new InvalidOperationException("Connection not initialized.");

            public override double CurrentValue
            {
                get => _node.Value?.Current ?? 0;
                set
                {
                    if (_node.Value == null)
                    {
                        return;
                    }

                    var clamped = Math.Max(_node.Value.Minimum, Math.Min(_node.Value.Maximum, value));
                    _node.Value.Current = clamped;
                }
            }
        }

        private string GetRoleName(int role)
        {
            return _roleNames.TryGetValue(role, out var name) ? name : "unknown";
        }
    }


    public static int Main(string[] args)
    {
        if (s_verbose && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LIBDBUS_AUTOGEN_VERBOSE")))
        {
            Environment.SetEnvironmentVariable("LIBDBUS_AUTOGEN_VERBOSE", "1");
        }

        var tree = new AtspiTree();
        var server = new AtspiServer(tree);
        return server.Run();
    }

    private static void LogVerbose(string message)
    {
        if (!s_verbose)
        {
            return;
        }

        Console.Error.WriteLine($"[{s_uptime.Elapsed:hh\\:mm\\:ss\\.fff}] {message}");
    }
}

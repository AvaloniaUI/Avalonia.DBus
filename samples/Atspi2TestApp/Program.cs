using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia.DBus.AutoGen;
using Avalonia.DBus.SourceGen;
using Avalonia.DBus.Wire;

namespace Atspi2TestApp;

internal static class Program
{
    private static readonly bool s_verbose = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ATSPI_VERBOSE"));
    private static readonly Stopwatch s_uptime = Stopwatch.StartNew();

    private const string RootPath = "/org/a11y/atspi/accessible/root";
    private const string NullPath = "/org/a11y/atspi/null";

    private const string IfaceAccessible = "org.a11y.atspi.Accessible";
    private const string IfaceApplication = "org.a11y.atspi.Application";
    private const string IfaceComponent = "org.a11y.atspi.Component";
    private const string IfaceAction = "org.a11y.atspi.Action";
    private const string IfaceValue = "org.a11y.atspi.Value";
    private const string IfaceSocket = "org.a11y.atspi.Socket";
    private const string IfaceProperties = "org.freedesktop.DBus.Properties";
    private const string IfaceIntrospectable = "org.freedesktop.DBus.Introspectable";
    private const string IfacePeer = "org.freedesktop.DBus.Peer";
    private const string IfaceEventObject = "org.a11y.atspi.Event.Object";

    private const string BusNameRegistry = "org.a11y.atspi.Registry";

    private const string BusNameA11y = "org.a11y.Bus";
    private const string PathA11y = "/org/a11y/bus";
    private const string IfaceA11y = "org.a11y.Bus";

    private const uint AccessibleVersion = 1;
    private const uint ApplicationVersion = 1;
    private const uint ComponentVersion = 1;
    private const uint ActionVersion = 1;
    private const uint ValueVersion = 1;

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

    private readonly struct Rect
    {
        public Rect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
    }

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

    private sealed unsafe class Utf8String : IDisposable
    {
        private readonly byte[] _buffer;
        private readonly GCHandle _handle;

        public Utf8String(string value)
        {
            _buffer = Encoding.UTF8.GetBytes(value + "\0");
            _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
            Pointer = (byte*)_handle.AddrOfPinnedObject();
        }

        public byte* Pointer { get; }

        public void Dispose()
        {
            if (_handle.IsAllocated)
            {
                _handle.Free();
            }
        }
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

    private static unsafe string? PtrToString(byte* value)
    {
        return value == null ? null : Marshal.PtrToStringUTF8((IntPtr)value);
    }

    private sealed unsafe class AtspiServer
    {
        private const int WindowToggleIntervalMs = 3000;
        private unsafe delegate void IterAppender(DBusMessageIter* iter);

        private readonly AtspiTree _tree;
        private readonly Dictionary<int, string> _roleNames = new();
        private readonly object _treeGate = new();
        private GCHandle _selfHandle;
        private DBusObjectPathVTable _vtable;
        private DBusConnection* _a11yConnection;
        private string _a11yAddress = string.Empty;
        private string _uniqueName = string.Empty;
        private bool _running;
        private int _windowToggleCounter;
        private ConnectionEventLoop? _eventLoop;
        private readonly System.Threading.ManualResetEventSlim _shutdownEvent = new(false);
        private System.Threading.Timer? _toggleTimer;
        private PosixSignalRegistration? _sigintRegistration;
        private PosixSignalRegistration? _sigtermRegistration;
        private System.Threading.Timer? _forceExitTimer;

        private static readonly DBusObjectPathMessageFunction MessageHandler = HandleMessage;

        public AtspiServer(AtspiTree tree)
        {
            _tree = tree;
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
            _selfHandle = GCHandle.Alloc(this);
            _vtable = new DBusObjectPathVTable
            {
                unregister_function = IntPtr.Zero,
                message_function = Marshal.GetFunctionPointerForDelegate(MessageHandler)
            };

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

            LogVerbose("Registering object paths");
            if (!RegisterObjectPaths())
            {
                Cleanup();
                return 1;
            }

            _eventLoop = new ConnectionEventLoop(_a11yConnection);

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

        private static DBusHandlerResult HandleMessage(DBusConnection* connection, DBusMessage* message, void* userData)
        {
            var handle = GCHandle.FromIntPtr((IntPtr)userData);
            if (handle.Target is not AtspiServer server)
            {
                return DBusHandlerResult.DBUS_HANDLER_RESULT_NOT_YET_HANDLED;
            }

            return server.DispatchMessage(connection, message);
        }

        private DBusHandlerResult DispatchMessage(DBusConnection* connection, DBusMessage* message)
        {
            Stopwatch? sw = null;
            string? description = null;
            if (s_verbose)
            {
                sw = Stopwatch.StartNew();
                description = DescribeMessage(message);
                LogVerbose($"Incoming {description}");
            }

            DBusHandlerResult result;
            lock (_treeGate)
            {
                if (dbus.dbus_message_get_type(message) != dbus.DBUS_MESSAGE_TYPE_METHOD_CALL)
                {
                    result = DBusHandlerResult.DBUS_HANDLER_RESULT_NOT_YET_HANDLED;
                }
                else
                {
                    var path = PtrToString(dbus.dbus_message_get_path(message)) ?? string.Empty;
                    if (!_tree.NodesByPath.TryGetValue(path, out var node))
                    {
                        result = DBusHandlerResult.DBUS_HANDLER_RESULT_NOT_YET_HANDLED;
                    }
                    else
                    {
                        var iface = PtrToString(dbus.dbus_message_get_interface(message)) ?? string.Empty;
                        var member = PtrToString(dbus.dbus_message_get_member(message)) ?? string.Empty;

                        if (iface == IfaceIntrospectable && member == "Introspect")
                        {
                            result = SendStringReply(connection, message, BuildIntrospection(node));
                        }
                        else if (iface == IfacePeer)
                        {
                            if (member == "Ping")
                            {
                                result = SendEmptyReply(connection, message);
                            }
                            else if (member == "GetMachineId")
                            {
                                result = SendStringReply(connection, message, "unknown");
                            }
                            else
                            {
                                result = SendError(connection, message, "Unknown method");
                            }
                        }
                        else if (iface == IfaceProperties)
                        {
                            result = HandleProperties(connection, message, node, member);
                        }
                        else if (iface == IfaceAccessible)
                        {
                            result = HandleAccessible(connection, message, node, member);
                        }
                        else if (iface == IfaceApplication)
                        {
                            result = HandleApplication(connection, message, node, member);
                        }
                        else if (iface == IfaceComponent)
                        {
                            result = HandleComponent(connection, message, node, member);
                        }
                        else if (iface == IfaceAction)
                        {
                            result = HandleAction(connection, message, node, member);
                        }
                        else
                        {
                            result = SendError(connection, message, "Unknown method");
                        }
                    }
                }
            }

            if (s_verbose && sw != null)
            {
                LogVerbose($"Handled {description ?? "message"} result={result} ({sw.ElapsedMilliseconds} ms)");
            }

            return result;
        }

        private static DBusHandlerResult SendEmptyReply(DBusConnection* connection, DBusMessage* message)
        {
            var reply = dbus.dbus_message_new_method_return(message);
            if (reply == null)
            {
                return DBusHandlerResult.DBUS_HANDLER_RESULT_NEED_MEMORY;
            }

            dbus.dbus_connection_send(connection, reply, null);
            dbus.dbus_connection_flush(connection);
            dbus.dbus_message_unref(reply);
            return DBusHandlerResult.DBUS_HANDLER_RESULT_HANDLED;
        }

        private static DBusHandlerResult SendStringReply(DBusConnection* connection, DBusMessage* message, string value)
        {
            return SendReply(connection, message, iter => AppendString(iter, value));
        }

        private static DBusHandlerResult SendReply(DBusConnection* connection, DBusMessage* message, IterAppender appender)
        {
            var reply = dbus.dbus_message_new_method_return(message);
            if (reply == null)
            {
                return DBusHandlerResult.DBUS_HANDLER_RESULT_NEED_MEMORY;
            }

            DBusMessageIter iter;
            dbus.dbus_message_iter_init_append(reply, &iter);
            appender(&iter);
            dbus.dbus_connection_send(connection, reply, null);
            dbus.dbus_connection_flush(connection);
            dbus.dbus_message_unref(reply);
            return DBusHandlerResult.DBUS_HANDLER_RESULT_HANDLED;
        }

        private static DBusHandlerResult SendError(DBusConnection* connection, DBusMessage* message, string errorMessage)
        {
            using var errorName = new Utf8String("org.freedesktop.DBus.Error.UnknownMethod");
            using var errorText = new Utf8String(errorMessage);
            var reply = dbus.dbus_message_new_error(message, errorName.Pointer, errorText.Pointer);
            if (reply == null)
            {
                return DBusHandlerResult.DBUS_HANDLER_RESULT_NEED_MEMORY;
            }

            dbus.dbus_connection_send(connection, reply, null);
            dbus.dbus_connection_flush(connection);
            dbus.dbus_message_unref(reply);
            return DBusHandlerResult.DBUS_HANDLER_RESULT_HANDLED;
        }

        private string BuildIntrospection(AccessibleNode node)
        {
            var interfaces = new HashSet<string>(node.Interfaces, StringComparer.Ordinal)
            {
                IfaceProperties,
                IfaceIntrospectable,
                IfacePeer
            };

            var builder = new StringBuilder();
            builder.AppendLine("<node>");
            foreach (var iface in interfaces)
            {
                builder.AppendLine($"  <interface name=\"{iface}\"/>");
            }
            builder.AppendLine("</node>");
            return builder.ToString();
        }

        private DBusHandlerResult HandleProperties(DBusConnection* connection, DBusMessage* message, AccessibleNode node, string member)
        {
            if (member == "Get")
            {
                return HandlePropertiesGet(connection, message, node);
            }

            if (member == "GetAll")
            {
                return HandlePropertiesGetAll(connection, message, node);
            }

            if (member == "Set")
            {
                return HandlePropertiesSet(connection, message, node);
            }

            return SendError(connection, message, "Unknown properties call");
        }

        private DBusHandlerResult HandleAccessible(DBusConnection* connection, DBusMessage* message, AccessibleNode node, string member)
        {
            if (member == "GetChildAtIndex")
            {
                var index = ReadInt32Arg(message, 0);
                var child = index >= 0 && index < node.Children.Count ? node.Children[index] : null;
                return SendReply(connection, message, iter =>
                {
                    var reference = GetReference(child);
                    AppendObjectReference(iter, reference.busName, reference.path);
                });
            }

            if (member == "GetChildren")
            {
                return SendReply(connection, message, iter =>
                {
                    AppendObjectReferenceArray(iter, node.Children);
                });
            }

            if (member == "GetIndexInParent")
            {
                var index = node.Parent == null ? -1 : node.Parent.Children.IndexOf(node);
                return SendReply(connection, message, iter => AppendInt32(iter, index));
            }

            if (member == "GetRelationSet")
            {
                return SendReply(connection, message, AppendEmptyRelationArray);
            }

            if (member == "GetRole")
            {
                return SendReply(connection, message, iter => AppendUInt32(iter, (uint)node.Role));
            }

            if (member == "GetRoleName" || member == "GetLocalizedRoleName")
            {
                var roleName = _roleNames.TryGetValue(node.Role, out var name) ? name : "unknown";
                return SendStringReply(connection, message, roleName);
            }

            if (member == "GetState")
            {
                return SendReply(connection, message, iter => AppendUInt32Array(iter, node.States));
            }

            if (member == "GetAttributes")
            {
                return SendReply(connection, message, AppendEmptyAttributes);
            }

            if (member == "GetApplication")
            {
                var reference = GetReference(_tree.Root);
                return SendReply(connection, message, iter => AppendObjectReference(iter, reference.busName, reference.path));
            }

            if (member == "GetInterfaces")
            {
                return SendReply(connection, message, iter => AppendStringArray(iter, node.Interfaces));
            }

            return SendError(connection, message, "Unknown accessible call");
        }

        private DBusHandlerResult HandleApplication(DBusConnection* connection, DBusMessage* message, AccessibleNode node, string member)
        {
            if (member == "GetLocale")
            {
                return SendStringReply(connection, message, node.Locale);
            }

            if (member == "GetApplicationBusAddress")
            {
                return SendStringReply(connection, message, string.Empty);
            }

            return SendError(connection, message, "Unknown application call");
        }

        private DBusHandlerResult HandleComponent(DBusConnection* connection, DBusMessage* message, AccessibleNode node, string member)
        {
            if (member == "Contains")
            {
                var x = ReadInt32Arg(message, 0);
                var y = ReadInt32Arg(message, 1);
                var coordType = (uint)ReadInt32Arg(message, 2);
                var screenPoint = TranslatePoint(node, x, y, coordType);
                var contains = ContainsPoint(node.Extents, screenPoint.x, screenPoint.y);
                return SendReply(connection, message, iter => AppendBoolean(iter, contains));
            }

            if (member == "GetAccessibleAtPoint")
            {
                var x = ReadInt32Arg(message, 0);
                var y = ReadInt32Arg(message, 1);
                var coordType = (uint)ReadInt32Arg(message, 2);
                var screenPoint = TranslatePoint(node, x, y, coordType);
                var target = FindAtPoint(node, screenPoint.x, screenPoint.y);
                var reference = GetReference(target);
                return SendReply(connection, message, iter => AppendObjectReference(iter, reference.busName, reference.path));
            }

            if (member == "GetExtents")
            {
                var coordType = (uint)ReadInt32Arg(message, 0);
                var rect = TranslateRect(node, coordType);
                return SendReply(connection, message, iter => AppendRect(iter, rect));
            }

            if (member == "GetPosition")
            {
                var coordType = (uint)ReadInt32Arg(message, 0);
                var rect = TranslateRect(node, coordType);
                return SendReply(connection, message, iter =>
                {
                    AppendInt32(iter, rect.X);
                    AppendInt32(iter, rect.Y);
                });
            }

            if (member == "GetSize")
            {
                return SendReply(connection, message, iter =>
                {
                    AppendInt32(iter, node.Extents.Width);
                    AppendInt32(iter, node.Extents.Height);
                });
            }

            if (member == "GetLayer")
            {
                var layer = node.Role == RoleFrame ? 7u : 3u;
                return SendReply(connection, message, iter => AppendUInt32(iter, layer));
            }

            if (member == "GetMDIZOrder")
            {
                return SendReply(connection, message, iter => AppendInt16(iter, -1));
            }

            if (member == "GrabFocus")
            {
                SetFocused(node);
                return SendReply(connection, message, iter => AppendBoolean(iter, true));
            }

            if (member == "GetAlpha")
            {
                return SendReply(connection, message, iter => AppendDouble(iter, 1.0));
            }

            if (member is "SetExtents" or "SetPosition" or "SetSize" or "ScrollTo" or "ScrollToPoint")
            {
                return SendReply(connection, message, iter => AppendBoolean(iter, false));
            }

            return SendError(connection, message, "Unknown component call");
        }

        private DBusHandlerResult HandleAction(DBusConnection* connection, DBusMessage* message, AccessibleNode node, string member)
        {
            if (node.Action == null)
            {
                return SendError(connection, message, "Action not supported");
            }

            if (member == "GetDescription")
            {
                return SendStringReply(connection, message, node.Action.Description);
            }

            if (member == "GetName")
            {
                return SendStringReply(connection, message, node.Action.Name);
            }

            if (member == "GetLocalizedName")
            {
                return SendStringReply(connection, message, node.Action.LocalizedName);
            }

            if (member == "GetKeyBinding")
            {
                return SendStringReply(connection, message, node.Action.KeyBinding);
            }

            if (member == "GetActions")
            {
                return SendReply(connection, message, iter => AppendActionArray(iter, node.Action));
            }

            if (member == "DoAction")
            {
                if (node.Role == RoleCheckBox)
                {
                    if (node.States.Contains(StateChecked))
                    {
                        node.States.Remove(StateChecked);
                    }
                    else
                    {
                        node.States.Add(StateChecked);
                    }
                }

                return SendReply(connection, message, iter => AppendBoolean(iter, true));
            }

            return SendError(connection, message, "Unknown action call");
        }

        private DBusHandlerResult HandlePropertiesGet(DBusConnection* connection, DBusMessage* message, AccessibleNode node)
        {
            var iface = ReadStringArg(message, 0);
            var prop = ReadStringArg(message, 1);

            if (iface == IfaceAccessible)
            {
                return prop switch
                {
                    "version" => SendReply(connection, message, iter => AppendVariantUInt32(iter, AccessibleVersion)),
                    "Name" => SendReply(connection, message, iter => AppendVariantString(iter, node.Name)),
                    "Description" => SendReply(connection, message, iter => AppendVariantString(iter, node.Description)),
                    "Parent" => SendReply(connection, message, iter => AppendVariantObjectReference(iter, node.Parent)),
                    "ChildCount" => SendReply(connection, message, iter => AppendVariantInt32(iter, node.Children.Count)),
                    "Locale" => SendReply(connection, message, iter => AppendVariantString(iter, node.Locale)),
                    "AccessibleId" => SendReply(connection, message, iter => AppendVariantString(iter, node.AccessibleId)),
                    "HelpText" => SendReply(connection, message, iter => AppendVariantString(iter, node.HelpText)),
                    _ => SendError(connection, message, "Unknown accessible property")
                };
            }

            if (iface == IfaceApplication)
            {
                var idValue = node.ApplicationId ?? 0;
                return prop switch
                {
                    "ToolkitName" => SendReply(connection, message, iter => AppendVariantString(iter, "Avalonia.DBus")),
                    "Version" => SendReply(connection, message, iter => AppendVariantString(iter, "1.0")),
                    "ToolkitVersion" => SendReply(connection, message, iter => AppendVariantString(iter, "1.0")),
                    "AtspiVersion" => SendReply(connection, message, iter => AppendVariantString(iter, "2.1")),
                    "InterfaceVersion" => SendReply(connection, message, iter => AppendVariantUInt32(iter, ApplicationVersion)),
                    "Id" => SendReply(connection, message, iter => AppendVariantInt32(iter, idValue)),
                    _ => SendError(connection, message, "Unknown application property")
                };
            }

            if (iface == IfaceComponent)
            {
                return prop switch
                {
                    "version" => SendReply(connection, message, iter => AppendVariantUInt32(iter, ComponentVersion)),
                    _ => SendError(connection, message, "Unknown component property")
                };
            }

            if (iface == IfaceAction)
            {
                var nActions = node.Action == null ? 0 : 1;
                return prop switch
                {
                    "version" => SendReply(connection, message, iter => AppendVariantUInt32(iter, ActionVersion)),
                    "NActions" => SendReply(connection, message, iter => AppendVariantInt32(iter, nActions)),
                    _ => SendError(connection, message, "Unknown action property")
                };
            }

            if (iface == IfaceValue)
            {
                var value = node.Value;
                return prop switch
                {
                    "version" => SendReply(connection, message, iter => AppendVariantUInt32(iter, ValueVersion)),
                    "MinimumValue" => SendReply(connection, message, iter => AppendVariantDouble(iter, value?.Minimum ?? 0)),
                    "MaximumValue" => SendReply(connection, message, iter => AppendVariantDouble(iter, value?.Maximum ?? 0)),
                    "MinimumIncrement" => SendReply(connection, message, iter => AppendVariantDouble(iter, value?.Increment ?? 0)),
                    "CurrentValue" => SendReply(connection, message, iter => AppendVariantDouble(iter, value?.Current ?? 0)),
                    "Text" => SendReply(connection, message, iter => AppendVariantString(iter, value?.Text ?? string.Empty)),
                    _ => SendError(connection, message, "Unknown value property")
                };
            }

            return SendError(connection, message, "Unknown interface for property get");
        }

        private DBusHandlerResult HandlePropertiesGetAll(DBusConnection* connection, DBusMessage* message, AccessibleNode node)
        {
            var iface = ReadStringArg(message, 0);
            return SendReply(connection, message, iter =>
            {
                var dictIter = BeginDictionary(iter);
                if (iface == IfaceAccessible)
                {
                    AppendDictionaryEntry(&dictIter, "version", entry => AppendVariantUInt32(entry, AccessibleVersion));
                    AppendDictionaryEntry(&dictIter, "Name", entry => AppendVariantString(entry, node.Name));
                    AppendDictionaryEntry(&dictIter, "Description", entry => AppendVariantString(entry, node.Description));
                    AppendDictionaryEntry(&dictIter, "Parent", entry => AppendVariantObjectReference(entry, node.Parent));
                    AppendDictionaryEntry(&dictIter, "ChildCount", entry => AppendVariantInt32(entry, node.Children.Count));
                    AppendDictionaryEntry(&dictIter, "Locale", entry => AppendVariantString(entry, node.Locale));
                    AppendDictionaryEntry(&dictIter, "AccessibleId", entry => AppendVariantString(entry, node.AccessibleId));
                    AppendDictionaryEntry(&dictIter, "HelpText", entry => AppendVariantString(entry, node.HelpText));
                }
                else if (iface == IfaceApplication)
                {
                    var idValue = node.ApplicationId ?? 0;
                    AppendDictionaryEntry(&dictIter, "ToolkitName", entry => AppendVariantString(entry, "Avalonia.DBus"));
                    AppendDictionaryEntry(&dictIter, "Version", entry => AppendVariantString(entry, "1.0"));
                    AppendDictionaryEntry(&dictIter, "ToolkitVersion", entry => AppendVariantString(entry, "1.0"));
                    AppendDictionaryEntry(&dictIter, "AtspiVersion", entry => AppendVariantString(entry, "2.1"));
                    AppendDictionaryEntry(&dictIter, "InterfaceVersion", entry => AppendVariantUInt32(entry, ApplicationVersion));
                    AppendDictionaryEntry(&dictIter, "Id", entry => AppendVariantInt32(entry, idValue));
                }
                else if (iface == IfaceComponent)
                {
                    AppendDictionaryEntry(&dictIter, "version", entry => AppendVariantUInt32(entry, ComponentVersion));
                }
                else if (iface == IfaceAction)
                {
                    var nActions = node.Action == null ? 0 : 1;
                    AppendDictionaryEntry(&dictIter, "version", entry => AppendVariantUInt32(entry, ActionVersion));
                    AppendDictionaryEntry(&dictIter, "NActions", entry => AppendVariantInt32(entry, nActions));
                }
                else if (iface == IfaceValue)
                {
                    var value = node.Value;
                    AppendDictionaryEntry(&dictIter, "version", entry => AppendVariantUInt32(entry, ValueVersion));
                    AppendDictionaryEntry(&dictIter, "MinimumValue", entry => AppendVariantDouble(entry, value?.Minimum ?? 0));
                    AppendDictionaryEntry(&dictIter, "MaximumValue", entry => AppendVariantDouble(entry, value?.Maximum ?? 0));
                    AppendDictionaryEntry(&dictIter, "MinimumIncrement", entry => AppendVariantDouble(entry, value?.Increment ?? 0));
                    AppendDictionaryEntry(&dictIter, "CurrentValue", entry => AppendVariantDouble(entry, value?.Current ?? 0));
                    AppendDictionaryEntry(&dictIter, "Text", entry => AppendVariantString(entry, value?.Text ?? string.Empty));
                }
                EndDictionary(iter, dictIter);
            });
        }

        private DBusHandlerResult HandlePropertiesSet(DBusConnection* connection, DBusMessage* message, AccessibleNode node)
        {
            var iface = ReadStringArg(message, 0);
            var prop = ReadStringArg(message, 1);

            if (iface == IfaceApplication && prop == "Id")
            {
                if (TryReadVariantInt32(message, 2, out var idValue))
                {
                    node.ApplicationId = idValue;
                }

                return SendEmptyReply(connection, message);
            }

            if (iface == IfaceValue && prop == "CurrentValue")
            {
                if (node.Value != null && TryReadVariantDouble(message, 2, out var current))
                {
                    if (current < node.Value.Minimum)
                    {
                        current = node.Value.Minimum;
                    }
                    else if (current > node.Value.Maximum)
                    {
                        current = node.Value.Maximum;
                    }

                    node.Value.Current = current;
                }

                return SendEmptyReply(connection, message);
            }

            return SendError(connection, message, "Property is read-only or unsupported");
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

            DBusError error = default;
            dbus.dbus_error_init(&error);
            using var addressUtf8 = new Utf8String(address);
            LogVerbose("Opening private accessibility bus connection");
            _a11yConnection = dbus.dbus_connection_open_private(addressUtf8.Pointer, &error);

            if (_a11yConnection == null)
            {
                Console.Error.WriteLine($"Failed to open accessibility bus: {DescribeError(&error)}");
                dbus.dbus_error_free(&error);
                return false;
            }

            LogVerbose("Registering accessibility bus connection");
            if (dbus.dbus_bus_register(_a11yConnection, &error) == 0)
            {
                Console.Error.WriteLine($"Failed to register on accessibility bus: {DescribeError(&error)}");
                dbus.dbus_error_free(&error);
                return false;
            }

            var namePtr = dbus.dbus_bus_get_unique_name(_a11yConnection);
            _uniqueName = PtrToString(namePtr) ?? string.Empty;
            LogVerbose($"TryConnect end ({sw.ElapsedMilliseconds} ms)");
            return true;
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
            using var path = new Utf8String("/org/a11y/atspi/accessible");
            var handlePtr = GCHandle.ToIntPtr(_selfHandle);
            uint result;
            fixed (DBusObjectPathVTable* vtablePtr = &_vtable)
            {
                result = dbus.dbus_connection_register_fallback(_a11yConnection, path.Pointer, vtablePtr, (void*)handlePtr);
            }
            if (result == 0)
            {
                Console.Error.WriteLine("Failed to register object path fallback.");
                return false;
            }

            LogVerbose($"RegisterObjectPaths end ({sw.ElapsedMilliseconds} ms)");
            return true;
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

            if (_eventLoop != null)
            {
                _eventLoop.Dispose();
                _eventLoop = null;
            }

            if (_a11yConnection != null)
            {
                dbus.dbus_connection_close(_a11yConnection);
                dbus.dbus_connection_unref(_a11yConnection);
                _a11yConnection = null;
            }

            _a11yAddress = string.Empty;

            if (_selfHandle.IsAllocated)
            {
                _selfHandle.Free();
            }

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
                    EmitChildrenChanged(_tree.Root, "remove", index < 0 ? 0 : index, _tree.ToggleWindow);
                }
                else
                {
                    _windowToggleCounter++;
                    _tree.ToggleWindow.Description = $"Recurring window (cycle {_windowToggleCounter})";
                    _tree.AddToggleWindow();
                    var index = _tree.GetToggleWindowIndex();
                    EmitChildrenChanged(_tree.Root, "add", index < 0 ? 0 : index, _tree.ToggleWindow);
                    EmitPropertyChange(_tree.ToggleWindow, "accessible-description", _tree.ToggleWindow.Description);
                }
            }
        }

        private static string DescribeError(DBusError* error)
        {
            if (dbus.dbus_error_is_set(error) == 0)
            {
                return "Unknown DBus error.";
            }

            var name = PtrToString(error->name) ?? "DBus error";
            var message = PtrToString(error->message) ?? string.Empty;
            return string.IsNullOrWhiteSpace(message) ? name : $"{name}: {message}";
        }

        private static void AppendString(DBusMessageIter* iter, string value)
        {
            using var utf8 = new Utf8String(value);
            var ptr = utf8.Pointer;
            dbus.dbus_message_iter_append_basic(iter, dbus.DBUS_TYPE_STRING, &ptr);
        }

        private static void AppendObjectPath(DBusMessageIter* iter, string value)
        {
            using var utf8 = new Utf8String(value);
            var ptr = utf8.Pointer;
            dbus.dbus_message_iter_append_basic(iter, dbus.DBUS_TYPE_OBJECT_PATH, &ptr);
        }

        private static void AppendInt32(DBusMessageIter* iter, int value)
        {
            dbus.dbus_message_iter_append_basic(iter, dbus.DBUS_TYPE_INT32, &value);
        }

        private static void AppendUInt32(DBusMessageIter* iter, uint value)
        {
            dbus.dbus_message_iter_append_basic(iter, dbus.DBUS_TYPE_UINT32, &value);
        }

        private static void AppendBoolean(DBusMessageIter* iter, bool value)
        {
            uint dbusValue = value ? 1u : 0u;
            dbus.dbus_message_iter_append_basic(iter, dbus.DBUS_TYPE_BOOLEAN, &dbusValue);
        }

        private static void AppendDouble(DBusMessageIter* iter, double value)
        {
            dbus.dbus_message_iter_append_basic(iter, dbus.DBUS_TYPE_DOUBLE, &value);
        }

        private static string ReadSingleString(DBusMessage* message)
        {
            DBusMessageIter iter;
            if (dbus.dbus_message_iter_init(message, &iter) == 0)
            {
                return string.Empty;
            }

            byte* value;
            dbus.dbus_message_iter_get_basic(&iter, &value);
            return PtrToString(value) ?? string.Empty;
        }

        private static (string busName, string path)? ReadObjectReference(DBusMessage* message)
        {
            DBusMessageIter iter;
            if (dbus.dbus_message_iter_init(message, &iter) == 0)
            {
                return null;
            }

            DBusMessageIter structIter;
            dbus.dbus_message_iter_recurse(&iter, &structIter);

            byte* busNamePtr;
            dbus.dbus_message_iter_get_basic(&structIter, &busNamePtr);
            var busName = PtrToString(busNamePtr) ?? string.Empty;

            if (dbus.dbus_message_iter_next(&structIter) == 0)
            {
                return null;
            }

            byte* pathPtr;
            dbus.dbus_message_iter_get_basic(&structIter, &pathPtr);
            var path = PtrToString(pathPtr) ?? string.Empty;

            return (busName, path);
        }

        private static string ReadStringArg(DBusMessage* message, int index)
        {
            DBusMessageIter iter;
            if (dbus.dbus_message_iter_init(message, &iter) == 0)
            {
                return string.Empty;
            }

            for (var i = 0; i < index; i++)
            {
                if (dbus.dbus_message_iter_next(&iter) == 0)
                {
                    return string.Empty;
                }
            }

            byte* value;
            dbus.dbus_message_iter_get_basic(&iter, &value);
            return PtrToString(value) ?? string.Empty;
        }

        private static int ReadInt32Arg(DBusMessage* message, int index)
        {
            DBusMessageIter iter;
            if (dbus.dbus_message_iter_init(message, &iter) == 0)
            {
                return 0;
            }

            for (var i = 0; i < index; i++)
            {
                if (dbus.dbus_message_iter_next(&iter) == 0)
                {
                    return 0;
                }
            }

            var argType = dbus.dbus_message_iter_get_arg_type(&iter);
            if (argType == dbus.DBUS_TYPE_INT32)
            {
                int value;
                dbus.dbus_message_iter_get_basic(&iter, &value);
                return value;
            }

            if (argType == dbus.DBUS_TYPE_UINT32)
            {
                uint value;
                dbus.dbus_message_iter_get_basic(&iter, &value);
                return (int)value;
            }

            if (argType == dbus.DBUS_TYPE_INT16)
            {
                short value;
                dbus.dbus_message_iter_get_basic(&iter, &value);
                return value;
            }

            return 0;
        }

        private static bool TryReadVariantInt32(DBusMessage* message, int index, out int value)
        {
            value = 0;
            DBusMessageIter iter;
            if (dbus.dbus_message_iter_init(message, &iter) == 0)
            {
                return false;
            }

            for (var i = 0; i < index; i++)
            {
                if (dbus.dbus_message_iter_next(&iter) == 0)
                {
                    return false;
                }
            }

            if (dbus.dbus_message_iter_get_arg_type(&iter) != dbus.DBUS_TYPE_VARIANT)
            {
                return false;
            }

            DBusMessageIter variantIter;
            dbus.dbus_message_iter_recurse(&iter, &variantIter);
            var type = dbus.dbus_message_iter_get_arg_type(&variantIter);
            if (type == dbus.DBUS_TYPE_INT32)
            {
                int raw;
                dbus.dbus_message_iter_get_basic(&variantIter, &raw);
                value = raw;
                return true;
            }

            if (type == dbus.DBUS_TYPE_UINT32)
            {
                uint raw;
                dbus.dbus_message_iter_get_basic(&variantIter, &raw);
                value = (int)raw;
                return true;
            }

            return false;
        }

        private static bool TryReadVariantDouble(DBusMessage* message, int index, out double value)
        {
            value = 0;
            DBusMessageIter iter;
            if (dbus.dbus_message_iter_init(message, &iter) == 0)
            {
                return false;
            }

            for (var i = 0; i < index; i++)
            {
                if (dbus.dbus_message_iter_next(&iter) == 0)
                {
                    return false;
                }
            }

            if (dbus.dbus_message_iter_get_arg_type(&iter) != dbus.DBUS_TYPE_VARIANT)
            {
                return false;
            }

            DBusMessageIter variantIter;
            dbus.dbus_message_iter_recurse(&iter, &variantIter);
            if (dbus.dbus_message_iter_get_arg_type(&variantIter) != dbus.DBUS_TYPE_DOUBLE)
            {
                return false;
            }

            double raw;
            dbus.dbus_message_iter_get_basic(&variantIter, &raw);
            value = raw;
            return true;
        }

        private (string busName, string path) GetReference(AccessibleNode? node)
        {
            if (node == null)
            {
                return (string.Empty, NullPath);
            }

            return (_uniqueName, node.Path);
        }

        private static void AppendObjectReference(DBusMessageIter* iter, string busName, string path)
        {
            DBusMessageIter structIter;
            dbus.dbus_message_iter_open_container(iter, dbus.DBUS_TYPE_STRUCT, null, &structIter);
            AppendString(&structIter, busName);
            AppendObjectPath(&structIter, path);
            dbus.dbus_message_iter_close_container(iter, &structIter);
        }

        private void AppendObjectReferenceArray(DBusMessageIter* iter, IEnumerable<AccessibleNode> nodes)
        {
            using var signature = new Utf8String("(so)");
            DBusMessageIter arrayIter;
            dbus.dbus_message_iter_open_container(iter, dbus.DBUS_TYPE_ARRAY, signature.Pointer, &arrayIter);
            foreach (var node in nodes)
            {
                AppendObjectReference(&arrayIter, _uniqueName, node.Path);
            }
            dbus.dbus_message_iter_close_container(iter, &arrayIter);
        }

        private static void AppendStringArray(DBusMessageIter* iter, IEnumerable<string> values)
        {
            using var signature = new Utf8String("s");
            DBusMessageIter arrayIter;
            dbus.dbus_message_iter_open_container(iter, dbus.DBUS_TYPE_ARRAY, signature.Pointer, &arrayIter);
            foreach (var value in values)
            {
                AppendString(&arrayIter, value);
            }
            dbus.dbus_message_iter_close_container(iter, &arrayIter);
        }

        private static void AppendUInt32Array(DBusMessageIter* iter, IEnumerable<uint> values)
        {
            using var signature = new Utf8String("u");
            DBusMessageIter arrayIter;
            dbus.dbus_message_iter_open_container(iter, dbus.DBUS_TYPE_ARRAY, signature.Pointer, &arrayIter);
            foreach (var value in values)
            {
                AppendUInt32(&arrayIter, value);
            }
            dbus.dbus_message_iter_close_container(iter, &arrayIter);
        }

        private static void AppendEmptyRelationArray(DBusMessageIter* iter)
        {
            using var signature = new Utf8String("(ua(so))");
            DBusMessageIter arrayIter;
            dbus.dbus_message_iter_open_container(iter, dbus.DBUS_TYPE_ARRAY, signature.Pointer, &arrayIter);
            dbus.dbus_message_iter_close_container(iter, &arrayIter);
        }

        private static void AppendEmptyAttributes(DBusMessageIter* iter)
        {
            using var signature = new Utf8String("{ss}");
            DBusMessageIter arrayIter;
            dbus.dbus_message_iter_open_container(iter, dbus.DBUS_TYPE_ARRAY, signature.Pointer, &arrayIter);
            dbus.dbus_message_iter_close_container(iter, &arrayIter);
        }

        private static void AppendRect(DBusMessageIter* iter, Rect rect)
        {
            DBusMessageIter structIter;
            dbus.dbus_message_iter_open_container(iter, dbus.DBUS_TYPE_STRUCT, null, &structIter);
            AppendInt32(&structIter, rect.X);
            AppendInt32(&structIter, rect.Y);
            AppendInt32(&structIter, rect.Width);
            AppendInt32(&structIter, rect.Height);
            dbus.dbus_message_iter_close_container(iter, &structIter);
        }

        private static void AppendInt16(DBusMessageIter* iter, short value)
        {
            dbus.dbus_message_iter_append_basic(iter, dbus.DBUS_TYPE_INT16, &value);
        }

        private static void AppendActionArray(DBusMessageIter* iter, ActionInfo action)
        {
            using var signature = new Utf8String("(sss)");
            DBusMessageIter arrayIter;
            dbus.dbus_message_iter_open_container(iter, dbus.DBUS_TYPE_ARRAY, signature.Pointer, &arrayIter);
            DBusMessageIter structIter;
            dbus.dbus_message_iter_open_container(&arrayIter, dbus.DBUS_TYPE_STRUCT, null, &structIter);
            AppendString(&structIter, action.LocalizedName);
            AppendString(&structIter, action.Description);
            AppendString(&structIter, action.KeyBinding);
            dbus.dbus_message_iter_close_container(&arrayIter, &structIter);
            dbus.dbus_message_iter_close_container(iter, &arrayIter);
        }

        private static void AppendVariantString(DBusMessageIter* iter, string value)
        {
            using var signature = new Utf8String("s");
            DBusMessageIter variantIter;
            dbus.dbus_message_iter_open_container(iter, dbus.DBUS_TYPE_VARIANT, signature.Pointer, &variantIter);
            AppendString(&variantIter, value);
            dbus.dbus_message_iter_close_container(iter, &variantIter);
        }

        private static void AppendVariantInt32(DBusMessageIter* iter, int value)
        {
            using var signature = new Utf8String("i");
            DBusMessageIter variantIter;
            dbus.dbus_message_iter_open_container(iter, dbus.DBUS_TYPE_VARIANT, signature.Pointer, &variantIter);
            AppendInt32(&variantIter, value);
            dbus.dbus_message_iter_close_container(iter, &variantIter);
        }

        private static void AppendVariantUInt32(DBusMessageIter* iter, uint value)
        {
            using var signature = new Utf8String("u");
            DBusMessageIter variantIter;
            dbus.dbus_message_iter_open_container(iter, dbus.DBUS_TYPE_VARIANT, signature.Pointer, &variantIter);
            AppendUInt32(&variantIter, value);
            dbus.dbus_message_iter_close_container(iter, &variantIter);
        }

        private static void AppendVariantDouble(DBusMessageIter* iter, double value)
        {
            using var signature = new Utf8String("d");
            DBusMessageIter variantIter;
            dbus.dbus_message_iter_open_container(iter, dbus.DBUS_TYPE_VARIANT, signature.Pointer, &variantIter);
            AppendDouble(&variantIter, value);
            dbus.dbus_message_iter_close_container(iter, &variantIter);
        }

        private void AppendVariantObjectReference(DBusMessageIter* iter, AccessibleNode? node)
        {
            var reference = GetReference(node);
            using var signature = new Utf8String("(so)");
            DBusMessageIter variantIter;
            dbus.dbus_message_iter_open_container(iter, dbus.DBUS_TYPE_VARIANT, signature.Pointer, &variantIter);
            AppendObjectReference(&variantIter, reference.busName, reference.path);
            dbus.dbus_message_iter_close_container(iter, &variantIter);
        }

        private static void AppendVariantBoolean(DBusMessageIter* iter, bool value)
        {
            using var signature = new Utf8String("b");
            DBusMessageIter variantIter;
            dbus.dbus_message_iter_open_container(iter, dbus.DBUS_TYPE_VARIANT, signature.Pointer, &variantIter);
            AppendBoolean(&variantIter, value);
            dbus.dbus_message_iter_close_container(iter, &variantIter);
        }

        private static void AppendDictionaryEntry(DBusMessageIter* iter, string key, IterAppender appendVariant)
        {
            DBusMessageIter entryIter;
            dbus.dbus_message_iter_open_container(iter, dbus.DBUS_TYPE_DICT_ENTRY, null, &entryIter);
            AppendString(&entryIter, key);
            appendVariant(&entryIter);
            dbus.dbus_message_iter_close_container(iter, &entryIter);
        }

        private static DBusMessageIter BeginDictionary(DBusMessageIter* iter)
        {
            using var signature = new Utf8String("{sv}");
            DBusMessageIter dictIter;
            dbus.dbus_message_iter_open_container(iter, dbus.DBUS_TYPE_ARRAY, signature.Pointer, &dictIter);
            return dictIter;
        }

        private static void EndDictionary(DBusMessageIter* iter, DBusMessageIter dictIter)
        {
            dbus.dbus_message_iter_close_container(iter, &dictIter);
        }

        private static void AppendEmptyProperties(DBusMessageIter* iter)
        {
            var dictIter = BeginDictionary(iter);
            EndDictionary(iter, dictIter);
        }

        private void EmitChildrenChanged(AccessibleNode parent, string operation, int index, AccessibleNode child)
        {
            using var path = new Utf8String(parent.Path);
            using var iface = new Utf8String(IfaceEventObject);
            using var member = new Utf8String("ChildrenChanged");
            var signal = dbus.dbus_message_new_signal(path.Pointer, iface.Pointer, member.Pointer);
            if (signal == null)
            {
                return;
            }

            DBusMessageIter iter;
            dbus.dbus_message_iter_init_append(signal, &iter);
            AppendString(&iter, operation);
            AppendInt32(&iter, index);
            AppendInt32(&iter, 0);
            AppendVariantObjectReference(&iter, child);
            AppendEmptyProperties(&iter);

            dbus.dbus_connection_send(_a11yConnection, signal, null);
            dbus.dbus_connection_flush(_a11yConnection);
            dbus.dbus_message_unref(signal);
        }

        private void EmitPropertyChange(AccessibleNode node, string propertyName, string value)
        {
            using var path = new Utf8String(node.Path);
            using var iface = new Utf8String(IfaceEventObject);
            using var member = new Utf8String("PropertyChange");
            var signal = dbus.dbus_message_new_signal(path.Pointer, iface.Pointer, member.Pointer);
            if (signal == null)
            {
                return;
            }

            DBusMessageIter iter;
            dbus.dbus_message_iter_init_append(signal, &iter);
            AppendString(&iter, propertyName);
            AppendInt32(&iter, 0);
            AppendInt32(&iter, 0);
            AppendVariantString(&iter, value);
            AppendEmptyProperties(&iter);

            dbus.dbus_connection_send(_a11yConnection, signal, null);
            dbus.dbus_connection_flush(_a11yConnection);
            dbus.dbus_message_unref(signal);
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

    private static unsafe string DescribeMessage(DBusMessage* message)
    {
        if (message == null)
        {
            return "(null message)";
        }

        string sender = PtrToString(dbus.dbus_message_get_sender(message)) ?? string.Empty;
        string dest = PtrToString(dbus.dbus_message_get_destination(message)) ?? string.Empty;
        string path = PtrToString(dbus.dbus_message_get_path(message)) ?? string.Empty;
        string iface = PtrToString(dbus.dbus_message_get_interface(message)) ?? string.Empty;
        string member = PtrToString(dbus.dbus_message_get_member(message)) ?? string.Empty;
        string signature = PtrToString(dbus.dbus_message_get_signature(message)) ?? string.Empty;
        int type = dbus.dbus_message_get_type(message);
        string typeName = DescribeMessageType(type);

        if (type == dbus.DBUS_MESSAGE_TYPE_ERROR)
        {
            string errorName = PtrToString(dbus.dbus_message_get_error_name(message)) ?? string.Empty;
            return $"{typeName} sender='{sender}' dest='{dest}' path='{path}' iface='{iface}' member='{member}' sig='{signature}' error='{errorName}'";
        }

        return $"{typeName} sender='{sender}' dest='{dest}' path='{path}' iface='{iface}' member='{member}' sig='{signature}'";
    }

    private static string DescribeMessageType(int type)
    {
        return type switch
        {
            dbus.DBUS_MESSAGE_TYPE_METHOD_CALL => "method_call",
            dbus.DBUS_MESSAGE_TYPE_METHOD_RETURN => "method_return",
            dbus.DBUS_MESSAGE_TYPE_ERROR => "error",
            dbus.DBUS_MESSAGE_TYPE_SIGNAL => "signal",
            _ => $"type_{type}"
        };
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

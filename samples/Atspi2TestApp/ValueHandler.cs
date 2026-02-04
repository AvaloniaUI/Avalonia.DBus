using Avalonia.DBus;
using Avalonia.DBus.SourceGen;
using static Atspi2TestApp.Program;

namespace Atspi2TestApp;

internal sealed class ValueHandler : OrgA11yAtspiValueHandler
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

    public override DBusConnection Connection => _server.A11yConnection;

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

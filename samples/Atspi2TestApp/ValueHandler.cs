using Atspi2TestApp.DBusXml;
using static Atspi2TestApp.Program;

namespace Atspi2TestApp;

internal sealed class ValueHandler : IOrgA11yAtspiValue
{
    private readonly AccessibleNode _node;

    public ValueHandler(AtspiServer server, AccessibleNode node)
    {
        _ = server;
        _node = node;
    }

    public uint Version => ValueVersion;

    public double MinimumValue => _node.Value?.Minimum ?? 0;

    public double MaximumValue => _node.Value?.Maximum ?? 0;

    public double MinimumIncrement => _node.Value?.Increment ?? 0;

    public string Text => _node.Value?.Text ?? string.Empty;

    public double CurrentValue
    {
        get => _node.Value?.Current ?? 0;
        set
        {
            if (_node.Value == null)
                return;

            var clamped = Math.Max(_node.Value.Minimum, Math.Min(_node.Value.Maximum, value));
            _node.Value = _node.Value with { Current = clamped };
        }
    }
}

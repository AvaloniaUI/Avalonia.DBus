using System.Text;

namespace Avalonia.DBus.Wire;

public delegate T MessageValueReader<T>(Message message, object? state);

public sealed class MatchRule
{
    public MessageType Type { get; set; }
    public string? Sender { get; set; }
    public string? Path { get; set; }
    public string? Interface { get; set; }
    public string? Member { get; set; }
    public string? Arg0 { get; set; }

    public string ToMatchString()
    {
        var builder = new StringBuilder();
        Append(builder, "type", Type switch
        {
            MessageType.Signal => "signal",
            MessageType.MethodCall => "method_call",
            MessageType.MethodReturn => "method_return",
            MessageType.Error => "error",
            _ => null
        });
        Append(builder, "sender", Sender);
        Append(builder, "path", Path);
        Append(builder, "interface", Interface);
        Append(builder, "member", Member);
        Append(builder, "arg0", Arg0);
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string key, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append(',');
        }

        builder.Append(key);
        builder.Append("='");
        builder.Append(value.Replace("'", "\\'"));
        builder.Append("'");
    }
}

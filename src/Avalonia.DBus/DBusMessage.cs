using System;
using System.Collections.Generic;

namespace Avalonia.DBus.Wire;

/// <summary>
/// A pure managed class representing a D-Bus message.
/// </summary>
public sealed class DBusMessage
{
    private IReadOnlyList<object> _body = Array.Empty<object>();
    private DBusSignature _signature = new(string.Empty);

    /// <summary>
    /// The message type.
    /// </summary>
    public DBusMessageType Type { get; init; }

    /// <summary>
    /// Message flags.
    /// </summary>
    public DBusMessageFlags Flags { get; init; }

    /// <summary>
    /// Message serial number. Assigned by the connection when sent.
    /// </summary>
    public uint Serial { get; internal set; }

    /// <summary>
    /// For METHOD_RETURN and ERROR: the serial of the METHOD_CALL being replied to.
    /// </summary>
    public uint ReplySerial { get; init; }

    /// <summary>
    /// Object path (for METHOD_CALL and SIGNAL).
    /// </summary>
    public DBusObjectPath? Path { get; init; }

    /// <summary>
    /// Interface name.
    /// </summary>
    public string? Interface { get; init; }

    /// <summary>
    /// Method or signal name.
    /// </summary>
    public string? Member { get; init; }

    /// <summary>
    /// Error name (for ERROR messages).
    /// </summary>
    public string? ErrorName { get; init; }

    /// <summary>
    /// Destination bus name.
    /// </summary>
    public string? Destination { get; init; }

    /// <summary>
    /// Sender's unique bus name. Set by the message bus.
    /// </summary>
    public string? Sender { get; internal set; }

    /// <summary>
    /// Type signature of the message body. Computed from Body contents.
    /// </summary>
    public DBusSignature Signature => _signature;

    /// <summary>
    /// Message body as a list of typed values.
    /// </summary>
    public IReadOnlyList<object> Body
    {
        get => _body;
        init
        {
            _body = value ?? Array.Empty<object>();
            _signature = new DBusSignature(DBusSignatureInference.InferBodySignature(_body));
        }
    }

    /// <summary>
    /// Creates a METHOD_CALL message.
    /// </summary>
    public static DBusMessage CreateMethodCall(
        string destination,
        DBusObjectPath path,
        string iface,
        string member,
        params object[] body)
    {
        if (string.IsNullOrEmpty(destination))
        {
            throw new ArgumentException("Destination is required.", nameof(destination));
        }
        if (string.IsNullOrEmpty(iface))
        {
            throw new ArgumentException("Interface is required.", nameof(iface));
        }
        if (string.IsNullOrEmpty(member))
        {
            throw new ArgumentException("Member is required.", nameof(member));
        }

        return new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Destination = destination,
            Path = path,
            Interface = iface,
            Member = member,
            Body = body ?? Array.Empty<object>()
        };
    }

    /// <summary>
    /// Creates a SIGNAL message.
    /// </summary>
    public static DBusMessage CreateSignal(
        DBusObjectPath path,
        string iface,
        string member,
        params object[] body)
    {
        if (string.IsNullOrEmpty(iface))
        {
            throw new ArgumentException("Interface is required.", nameof(iface));
        }
        if (string.IsNullOrEmpty(member))
        {
            throw new ArgumentException("Member is required.", nameof(member));
        }

        return new DBusMessage
        {
            Type = DBusMessageType.Signal,
            Path = path,
            Interface = iface,
            Member = member,
            Body = body ?? Array.Empty<object>()
        };
    }

    /// <summary>
    /// Creates a METHOD_RETURN message in reply to this message.
    /// </summary>
    public DBusMessage CreateReply(params object[] body)
    {
        return new DBusMessage
        {
            Type = DBusMessageType.MethodReturn,
            ReplySerial = Serial,
            Destination = Sender,
            Body = body ?? Array.Empty<object>()
        };
    }

    /// <summary>
    /// Creates an ERROR message in reply to this message.
    /// </summary>
    public DBusMessage CreateError(string errorName, string? errorMessage = null)
    {
        if (string.IsNullOrEmpty(errorName))
        {
            throw new ArgumentException("Error name is required.", nameof(errorName));
        }

        if (errorMessage == null)
        {
            return new DBusMessage
            {
                Type = DBusMessageType.Error,
                ReplySerial = Serial,
                Destination = Sender,
                ErrorName = errorName,
                Body = Array.Empty<object>()
            };
        }

        return new DBusMessage
        {
            Type = DBusMessageType.Error,
            ReplySerial = Serial,
            Destination = Sender,
            ErrorName = errorName,
            Body = new object[] { errorMessage }
        };
    }

    /// <summary>
    /// Returns true if this is a METHOD_CALL for the specified interface and member.
    /// </summary>
    public bool IsMethodCall(string iface, string member)
        => Type == DBusMessageType.MethodCall
           && string.Equals(Interface, iface, StringComparison.Ordinal)
           && string.Equals(Member, member, StringComparison.Ordinal);

    /// <summary>
    /// Returns true if this is a SIGNAL for the specified interface and member.
    /// </summary>
    public bool IsSignal(string iface, string member)
        => Type == DBusMessageType.Signal
           && string.Equals(Interface, iface, StringComparison.Ordinal)
           && string.Equals(Member, member, StringComparison.Ordinal);

    /// <summary>
    /// Returns true if this is an ERROR with the specified error name.
    /// </summary>
    public bool IsError(string errorName)
        => Type == DBusMessageType.Error
           && string.Equals(ErrorName, errorName, StringComparison.Ordinal);
}

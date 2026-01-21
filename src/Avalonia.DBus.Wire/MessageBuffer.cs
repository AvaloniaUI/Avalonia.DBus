using System;
using Avalonia.DBus.AutoGen;

namespace Avalonia.DBus.Wire;

public sealed unsafe class MessageBuffer : IDisposable
{
    private DBusMessage* _message;

    internal MessageBuffer(DBusMessage* message)
    {
        _message = message;
    }

    internal DBusMessage* Detach()
    {
        var message = _message;
        _message = null;
        return message;
    }

    public Message ToMessage() => new Message(_message);

    public void Dispose()
    {
        if (_message != null)
        {
            dbus.dbus_message_unref(_message);
            _message = null;
        }
    }
}

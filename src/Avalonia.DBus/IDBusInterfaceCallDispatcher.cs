using System.Threading.Tasks;

namespace Avalonia.DBus;

public interface IDBusInterfaceCallDispatcher
{
    Task<DBusMessage> Handle(DBusMessage message, DBusConnection connection, object target);
}

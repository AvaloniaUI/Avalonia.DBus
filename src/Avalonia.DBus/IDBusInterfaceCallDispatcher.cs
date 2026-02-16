using System.Threading.Tasks;

namespace Avalonia.DBus;

public interface IDBusInterfaceCallDispatcher
{
    Task<DBusMessage> Handle(IDBusConnection connection, object? target, DBusMessage message);
}
 

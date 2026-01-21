using System.Threading.Tasks;

namespace Avalonia.DBus.Wire;

public interface IPathMethodHandler
{
    string Path { get; }

    bool HandlesChildPaths { get; }

    ValueTask HandleMethodAsync(MethodContext context);
}

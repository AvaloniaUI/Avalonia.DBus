using System.Runtime.InteropServices;

namespace Avalonia.DBus;

[StructLayout(LayoutKind.Sequential)]
internal struct PollFd
{
    public int fd;
    public PollEvents events;
    public PollEvents revents;
}
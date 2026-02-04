using System;

namespace Avalonia.DBus;

[Flags]
internal enum PollEvents : short
{
    None = 0,
    POLLIN = 0x0001,
    POLLPRI = 0x0002,
    POLLOUT = 0x0004,
    POLLERR = 0x0008,
    POLLHUP = 0x0010,
    POLLNVAL = 0x0020,
    POLLRDHUP = 0x2000
}
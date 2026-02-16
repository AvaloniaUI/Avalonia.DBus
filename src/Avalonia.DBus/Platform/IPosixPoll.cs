using System;

namespace Avalonia.DBus.Platform;

internal unsafe interface IPosixPoll
{
    int Poll(PollFd* fds, int nfds);
    int CreatePipe(out int readFd, out int writeFd);
    long Read(int fd, void* buf, IntPtr count);
    long Write(int fd, void* buf, IntPtr count);
    int Close(int fd);
    int Eintr { get; }
    int Eagain { get; }
    PollEvents PollErrorMask { get; }
}

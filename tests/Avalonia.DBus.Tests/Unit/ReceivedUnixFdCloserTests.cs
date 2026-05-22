using System.Runtime.InteropServices;
using Avalonia.DBus.Platform;
using Xunit;

namespace Avalonia.DBus.Tests.Unit;

/// <summary>
/// Verifies that Unix file descriptors carried by a received D-Bus message body are closed.
/// </summary>
public class ReceivedUnixFdCloserTests
{
    private static bool IsUnix =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    [Fact]
    public void CloseAll_ClosesTopLevelDescriptor()
    {
        if (!IsUnix)
            return;

        var poll = PosixPollFactory.Create();
        Assert.Equal(0, poll.CreatePipe(out var readFd, out var writeFd));
        try
        {
            ReceivedUnixFdCloser.CloseAll([new DBusUnixFd(readFd)]);

            // The descriptor is already closed; closing it a second time must fail.
            Assert.NotEqual(0, poll.Close(readFd));
        }
        finally
        {
            poll.Close(writeFd);
        }
    }

    [Fact]
    public void CloseAll_ClosesDescriptorNestedInStruct()
    {
        if (!IsUnix)
            return;

        var poll = PosixPollFactory.Create();
        Assert.Equal(0, poll.CreatePipe(out var readFd, out var writeFd));
        try
        {
            ReceivedUnixFdCloser.CloseAll(
                [new DBusStruct(new DBusUnixFd(readFd))]);

            Assert.NotEqual(0, poll.Close(readFd));
        }
        finally
        {
            poll.Close(writeFd);
        }
    }
}

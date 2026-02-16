using System.Runtime.InteropServices;
using Avalonia.DBus.Platform;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Unit;

public class PlatformAbstractionTests
{
    [Fact]
    public void PosixPollFactory_Create_ReturnsCorrectType()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Assert.Throws<System.PlatformNotSupportedException>(PosixPollFactory.Create);
            return;
        }

        var poll = PosixPollFactory.Create();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Assert.IsType<LinuxPosixPoll>(poll);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Assert.IsType<MacOSPosixPoll>(poll);
    }

    [SkipUnlessLinux]
    public void LinuxPosixPoll_Eagain_Is11()
    {
        var poll = new LinuxPosixPoll();

        Assert.Equal(11, poll.Eagain);
    }

    [SkipUnlessLinux]
    public void LinuxPosixPoll_Eintr_Is4()
    {
        var poll = new LinuxPosixPoll();

        Assert.Equal(4, poll.Eintr);
    }

    [SkipUnlessLinux]
    public void LinuxPosixPoll_ErrorMask_IncludesPollRdhup()
    {
        var poll = new LinuxPosixPoll();

        Assert.True(poll.PollErrorMask.HasFlag(PollEvents.POLLRDHUP));
        Assert.True(poll.PollErrorMask.HasFlag(PollEvents.POLLERR));
        Assert.True(poll.PollErrorMask.HasFlag(PollEvents.POLLHUP));
        Assert.True(poll.PollErrorMask.HasFlag(PollEvents.POLLNVAL));
    }

    [SkipUnlessMacOs]
    public void MacOSPosixPoll_Eagain_Is35()
    {
        var poll = new MacOSPosixPoll();

        Assert.Equal(35, poll.Eagain);
    }

    [SkipUnlessMacOs]
    public void MacOSPosixPoll_Eintr_Is4()
    {
        var poll = new MacOSPosixPoll();

        Assert.Equal(4, poll.Eintr);
    }

    [SkipUnlessMacOs]
    public void MacOSPosixPoll_ErrorMask_ExcludesPollRdhup()
    {
        var poll = new MacOSPosixPoll();

        Assert.False(poll.PollErrorMask.HasFlag(PollEvents.POLLRDHUP));
        Assert.True(poll.PollErrorMask.HasFlag(PollEvents.POLLERR));
        Assert.True(poll.PollErrorMask.HasFlag(PollEvents.POLLHUP));
        Assert.True(poll.PollErrorMask.HasFlag(PollEvents.POLLNVAL));
    }
}

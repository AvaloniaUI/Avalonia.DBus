using System.Runtime.InteropServices;
using Avalonia.DBus.Platform;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Interop;

[Trait("Category", "Interop")]
public class CrossPlatformTests
{
    [Fact]
    public void PollImplementation_Available_OnCurrentPlatform()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Assert.Throws<System.PlatformNotSupportedException>(PosixPollFactory.Create);
            return;
        }

        var poll = PosixPollFactory.Create();
        Assert.NotNull(poll);

        // Verify common EINTR value
        Assert.Equal(4, poll.Eintr);

        // Verify error mask includes core error events
        Assert.True(poll.PollErrorMask.HasFlag(PollEvents.POLLERR));
        Assert.True(poll.PollErrorMask.HasFlag(PollEvents.POLLHUP));
        Assert.True(poll.PollErrorMask.HasFlag(PollEvents.POLLNVAL));
    }

    [SkipUnlessLinux]
    public void LinuxPoll_HasCorrectConstants()
    {
        var poll = new LinuxPosixPoll();

        Assert.Equal(4, poll.Eintr);
        Assert.Equal(11, poll.Eagain);
        Assert.True(poll.PollErrorMask.HasFlag(PollEvents.POLLRDHUP));
    }

    [SkipUnlessMacOs]
    public void MacOSPoll_HasCorrectConstants()
    {
        var poll = new MacOSPosixPoll();

        Assert.Equal(4, poll.Eintr);
        Assert.Equal(35, poll.Eagain);
        Assert.False(poll.PollErrorMask.HasFlag(PollEvents.POLLRDHUP));
    }

    [Fact]
    public void SignatureInference_ConsistentAcrossPlatforms()
    {
        // These should produce identical results regardless of platform
        Assert.Equal("i", DBusSignatureInference.InferSignatureFromValue(42));
        Assert.Equal("s", DBusSignatureInference.InferSignatureFromValue("hello"));
        Assert.Equal("b", DBusSignatureInference.InferSignatureFromValue(true));
        Assert.Equal("d", DBusSignatureInference.InferSignatureFromValue(3.14));
        Assert.Equal("y", DBusSignatureInference.InferSignatureFromValue((byte)0xFF));
        Assert.Equal("x", DBusSignatureInference.InferSignatureFromValue(long.MaxValue));
        Assert.Equal("t", DBusSignatureInference.InferSignatureFromValue(ulong.MaxValue));
    }

    [Fact]
    public void MessageCreation_ConsistentAcrossPlatforms()
    {
        var msg = DBusMessage.CreateMethodCall(
            "org.test.Service",
            (DBusObjectPath)"/org/test/Object",
            "org.test.Interface",
            "TestMethod",
            "arg1", 42, true);

        Assert.Equal(DBusMessageType.MethodCall, msg.Type);
        Assert.Equal("sib", msg.Signature.Value);
        Assert.Equal(3, msg.Body.Count);
    }
}

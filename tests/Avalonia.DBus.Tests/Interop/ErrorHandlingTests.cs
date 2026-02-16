using System;
using System.Threading.Tasks;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Interop;

[Trait("Category", "Interop")]
public class ErrorHandlingTests
{
    [IntegrationFact]
    public async Task CallAfterDisposal_ThrowsObjectDisposedException()
    {
        var connection = await DBusConnection.ConnectSessionAsync();
        await connection.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await connection.CallMethodAsync(
                "org.freedesktop.DBus",
                (DBusObjectPath)"/org/freedesktop/DBus",
                "org.freedesktop.DBus",
                "ListNames"));
    }

    [IntegrationFact]
    public async Task SubscribeAfterDisposal_ThrowsObjectDisposedException()
    {
        var connection = await DBusConnection.ConnectSessionAsync();
        await connection.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await connection.SubscribeAsync(
                null, null,
                "org.freedesktop.DBus",
                "NameOwnerChanged",
                _ => Task.CompletedTask));
    }

    [IntegrationFact]
    [Trait("Category", "Interop")]
    public async Task DBusException_PreservesErrorDetails()
    {
        await using var connection = await DBusConnection.ConnectSessionAsync();

        var ex = await Assert.ThrowsAsync<DBusException>(async () =>
            await connection.CallMethodAsync(
                "org.nonexistent.service.test",
                (DBusObjectPath)"/test",
                "org.test.Iface",
                "Method"));

        Assert.NotNull(ex.ErrorName);
        Assert.NotEmpty(ex.ErrorName);
        Assert.NotNull(ex.ErrorReply);
        Assert.Equal(DBusMessageType.Error, ex.ErrorReply!.Type);
    }

    [Fact]
    [Trait("Category", "Interop")]
    public void CreateMethodCall_VeryLongPath_DoesNotThrow()
    {
        var longPath = "/" + new string('a', 500);
        var msg = DBusMessage.CreateMethodCall("org.test", (DBusObjectPath)longPath, "org.test", "Method");

        Assert.Equal(longPath, msg.Path!.Value.Value);
    }

    [Fact]
    [Trait("Category", "Interop")]
    public void CreateMethodCall_VeryLongInterfaceName_DoesNotThrow()
    {
        var longIface = "org." + new string('a', 200) + ".test";
        var msg = DBusMessage.CreateMethodCall("org.test", (DBusObjectPath)"/test", longIface, "Method");

        Assert.Equal(longIface, msg.Interface);
    }
}

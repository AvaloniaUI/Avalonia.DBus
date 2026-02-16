using System;
using System.Threading.Tasks;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Interop;

[Collection(DbusTestCollection.Name)]
[Trait("Category", "Interop")]
public class ErrorHandlingTests(BusFixture fixture)
{
    [IntegrationFact]
    public async Task CallAfterDisposal_ThrowsObjectDisposedException()
    {
        var connection = await fixture.CreateConnectionAsync();
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
        var connection = await fixture.CreateConnectionAsync();
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
        await using var connection = await fixture.CreateConnectionAsync();

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

}

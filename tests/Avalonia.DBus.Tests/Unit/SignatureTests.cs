using Xunit;

namespace Avalonia.DBus.Tests.Unit;

public class SignatureTests
{
    [Fact]
    public void NullValue_CoalescesToEmptyString()
    {
        var sig = new DBusSignature(null!);

        Assert.Equal(string.Empty, sig.Value);
    }
}

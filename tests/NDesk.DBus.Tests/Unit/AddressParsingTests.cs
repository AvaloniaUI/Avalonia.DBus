using System;
using Xunit;

namespace NDesk.DBus.Tests.Unit;

public class AddressParsingTests
{
    [Fact]
    public void AddressEntry_Parse_UnixPath()
    {
        var entry = AddressEntry.Parse("unix:path=/tmp/dbus-test");

        Assert.Equal("unix", entry.Method);
        Assert.Single(entry.Properties);
        Assert.Equal("/tmp/dbus-test", entry.Properties["path"]);
    }

    [Fact]
    public void AddressEntry_Parse_TcpAddress()
    {
        var entry = AddressEntry.Parse("tcp:host=localhost,port=1234");

        Assert.Equal("tcp", entry.Method);
        Assert.Equal(2, entry.Properties.Count);
        Assert.Equal("localhost", entry.Properties["host"]);
        Assert.Equal("1234", entry.Properties["port"]);
    }

    [Fact]
    public void AddressEntry_Parse_NoColon_ThrowsBadAddressException()
    {
        var ex = Assert.Throws<BadAddressException>(() =>
            AddressEntry.Parse("no-colon-here"));

        Assert.Contains("No colon found", ex.Message);
    }

    [Fact]
    public void Address_Parse_MultipleSemicolonSeparated()
    {
        var entries = Address.Parse("unix:path=/a;tcp:host=b,port=1");

        Assert.Equal(2, entries.Length);

        Assert.Equal("unix", entries[0].Method);
        Assert.Equal("/a", entries[0].Properties["path"]);

        Assert.Equal("tcp", entries[1].Method);
        Assert.Equal("b", entries[1].Properties["host"]);
        Assert.Equal("1", entries[1].Properties["port"]);
    }

    [Fact]
    public void Address_Parse_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Address.Parse(null));
    }

    [Fact]
    public void AddressEntry_ToString_EscapesSpecialCharacters()
    {
        var entry = new AddressEntry
        {
            Method = "test",
            Properties =
            {
                ["key"] = "value,with,commas"
            }
        };

        var serialized = entry.ToString();

        Assert.Contains("%2C", serialized);

        var reparsed = AddressEntry.Parse(serialized);
        Assert.Equal("value,with,commas", reparsed.Properties["key"]);
    }

    [Fact]
    public void AddressEntry_Escape_Unescape_RoundTrip_Colon()
    {
        var entry = new AddressEntry
        {
            Method = "test",
            Properties =
            {
                ["key"] = "host:port"
            }
        };

        var serialized = entry.ToString();

        Assert.Contains("%3A", serialized);

        var reparsed = AddressEntry.Parse(serialized);
        Assert.Equal("host:port", reparsed.Properties["key"]);
    }

    [Theory]
    [InlineData("unix:path=/tmp/x", "unix")]
    [InlineData("tcp:host=127.0.0.1,port=9999", "tcp")]
    [InlineData("nonce-tcp:host=localhost,port=1234,noncefile=/tmp/n", "nonce-tcp")]
    public void AddressEntry_Parse_MethodField(string address, string expectedMethod)
    {
        var entry = AddressEntry.Parse(address);

        Assert.Equal(expectedMethod, entry.Method);
    }
}

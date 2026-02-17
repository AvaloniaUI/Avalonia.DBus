using Xunit;

namespace NDesk.DBus.Tests.Unit;

public class MatchRuleToStringTests
{
    [Fact]
    public void ToString_AllFieldsSet_ProducesCompleteRule()
    {
        var rule = new MatchRule
        {
            MessageType = MessageType.Signal,
            Interface = "org.freedesktop.DBus",
            Member = "NameOwnerChanged",
            Path = new ObjectPath("/org/freedesktop/DBus"),
            Sender = "org.freedesktop.DBus",
            Destination = ":1.42"
        };

        var result = rule.ToString();

        Assert.Contains("type='signal'", result);
        Assert.Contains("interface='org.freedesktop.DBus'", result);
        Assert.Contains("member='NameOwnerChanged'", result);
        Assert.Contains("path='/org/freedesktop/DBus'", result);
        Assert.Contains("sender='org.freedesktop.DBus'", result);
        Assert.Contains("destination=':1.42'", result);
    }
}

public class MatchRuleParseTests
{
    [Fact]
    public void Parse_MultipleFields_SetsAll()
    {
        var rule = MatchRule.Parse(
            "type='signal',interface='org.freedesktop.DBus',member='NameOwnerChanged',path='/org/freedesktop/DBus'");

        Assert.NotNull(rule);
        Assert.Equal(MessageType.Signal, rule.MessageType);
        Assert.Equal("org.freedesktop.DBus", rule.Interface);
        Assert.Equal("NameOwnerChanged", rule.Member);
        Assert.Equal("/org/freedesktop/DBus", rule.Path.Value);
    }

    [Fact]
    public void Parse_Roundtrip_WithArgs()
    {
        var original = "type='signal',interface='org.example',arg0='test',arg5='data'";
        var rule = MatchRule.Parse(original);

        Assert.NotNull(rule);
        Assert.Equal(original, rule.ToString());
    }

    [Fact]
    public void Parse_DuplicateType_ReturnsNull()
    {
        var rule = MatchRule.Parse("type='signal',type='error'");

        Assert.Null(rule);
    }
}

public class MatchRuleMatchesTests
{
    [Fact]
    public void Matches_MultipleFieldsAllMatch_ReturnsTrue()
    {
        var rule = new MatchRule
        {
            MessageType = MessageType.Signal,
            Interface = "org.freedesktop.DBus",
            Member = "NameOwnerChanged",
            Path = new ObjectPath("/org/freedesktop/DBus")
        };

        var msg = new Message();
        msg.Header.MessageType = MessageType.Signal;
        msg.Header.Fields[FieldCode.Interface] = "org.freedesktop.DBus";
        msg.Header.Fields[FieldCode.Member] = "NameOwnerChanged";
        msg.Header.Fields[FieldCode.Path] = new ObjectPath("/org/freedesktop/DBus");

        Assert.True(rule.Matches(msg));
    }

    [Fact]
    public void Matches_MultipleFieldsOneMismatch_ReturnsFalse()
    {
        var rule = new MatchRule
        {
            MessageType = MessageType.Signal,
            Interface = "org.freedesktop.DBus",
            Member = "NameOwnerChanged"
        };

        var msg = new Message();
        msg.Header.MessageType = MessageType.Signal;
        msg.Header.Fields[FieldCode.Interface] = "org.freedesktop.DBus";
        msg.Header.Fields[FieldCode.Member] = "NameLost";

        Assert.False(rule.Matches(msg));
    }

    [Fact]
    public void Matches_RuleHasFieldButMessageDoesNot_StillReturnsTrue()
    {
        // When the message does not have the field at all, Matches does not reject
        // because TryGetValue returns false and the inner comparison is skipped.
        var rule = new MatchRule { Interface = "org.example.Iface" };
        var msg = new Message();

        Assert.True(rule.Matches(msg));
    }
}

public class MessageFilterHelperTests
{
    [Theory]
    [InlineData(1, "method_call")]
    [InlineData(2, "method_return")]
    [InlineData(3, "error")]
    [InlineData(4, "signal")]
    [InlineData(0, "invalid")]
    public void MessageTypeToString_CorrectMapping(byte mtypeByte, string expected)
    {
        var mtype = (MessageType)mtypeByte;
        Assert.Equal(expected, MessageFilter.MessageTypeToString(mtype));
    }
}

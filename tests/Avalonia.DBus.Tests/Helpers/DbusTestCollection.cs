using Xunit;

namespace Avalonia.DBus.Tests.Helpers;

[CollectionDefinition(Name)]
public class DbusTestCollection : ICollectionFixture<BusFixture>
{
    public const string Name = "DBus";
}

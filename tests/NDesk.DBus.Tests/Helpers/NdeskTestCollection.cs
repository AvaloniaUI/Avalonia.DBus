using Xunit;

namespace NDesk.DBus.Tests.Helpers;

[CollectionDefinition(Name)]
public class NdeskTestCollection : ICollectionFixture<NdeskBusFixture>
{
    public const string Name = "NDesk.DBus";
}

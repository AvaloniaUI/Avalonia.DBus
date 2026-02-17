namespace Avalonia.DBus.SourceGen.Tests.Helpers;

[CollectionDefinition(Name)]
public class DbusTestCollection : ICollectionFixture<DbusDaemonFixture>
{
    public const string Name = "DBus";
}

using Xunit;

namespace Avalonia.DBus.Tmds.Tests.Helpers;

[CollectionDefinition(Name)]
public class TmdsInteropTestCollection : ICollectionFixture<TmdsInteropFixture>
{
    public const string Name = "TmdsInterop";
}

using Xunit;

namespace Avalonia.DBus.Interop.Tests.Helpers;

[CollectionDefinition(Name)]
public class InteropTestCollection : ICollectionFixture<InteropFixture>
{
    public const string Name = "Interop";
}

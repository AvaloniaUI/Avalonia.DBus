using System.Collections.Generic;
using System.Linq;

namespace Avalonia.DBus.Interop.Tests.Contracts;

public class TypeTestService : ITypeTestService
{
    private readonly Dictionary<string, string> _map = new()
    {
        ["name"] = "test",
        ["version"] = "1.0"
    };

    public event TypeTestNotifyHandler? Notify;

    public string[] GetStringArray() => ["alpha", "beta", "gamma"];

    public int[] GetIntArray() => [10, 20, 30, 40];

    public int SumArray(int[] values) => values.Sum();

    public string JoinStrings(string[] parts, string separator) => string.Join(separator, parts);

    public Dictionary<string, string> GetStringMap() => new(_map);

    public string LookupInMap(string key) => _map.GetValueOrDefault(key, "");

    public MyTuple GetTuple() => new() { A = "hello", B = "world" };

    public object GetVariantString() => "variant-string";

    public object GetVariantInt() => 42;

    public Dictionary<string, object> GetMixedMap() => new()
    {
        ["count"] = 3,
        ["label"] = "mixed"
    };

    public void FireNotify(string tag, object payload)
    {
        Notify?.Invoke(tag, payload);
    }
}

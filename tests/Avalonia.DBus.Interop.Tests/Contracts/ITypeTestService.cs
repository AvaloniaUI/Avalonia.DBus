using System.Collections.Generic;
using NDesk.DBus;

namespace Avalonia.DBus.Interop.Tests.Contracts;

public delegate void TypeTestNotifyHandler(string tag, object payload);

public struct MyTuple
{
    public string A;
    public string B;
}

[Interface("org.avalonia.dbus.interop.TypeTest")]
public interface ITypeTestService
{
    string[] GetStringArray();
    int[] GetIntArray();
    int SumArray(int[] values);
    string JoinStrings(string[] parts, string separator);
    Dictionary<string, string> GetStringMap();
    string LookupInMap(string key);
    MyTuple GetTuple();
    object GetVariantString();
    object GetVariantInt();
    Dictionary<string, object> GetMixedMap();
    void FireNotify(string tag, object payload);
    event TypeTestNotifyHandler Notify;
}

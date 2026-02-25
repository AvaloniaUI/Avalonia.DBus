using Avalonia.DBus.WireMarshalling.Tests.Abstractions;
using NDesk.DBus;

namespace Avalonia.DBus.WireMarshalling.Tests.NDesk;

public sealed class NDeskSignatureBuilder : ISignatureBuilder
{
    public string GetSignature(Type type)
    {
        return Signature.GetSig(type).Value;
    }

    public string MakeArraySignature(string elementSig)
    {
        return "a" + elementSig;
    }

    public string MakeStructSignature(params string[] fieldSigs)
    {
        return "(" + string.Join("", fieldSigs) + ")";
    }

    public string MakeDictSignature(string keySig, string valueSig)
    {
        return "a{" + keySig + valueSig + "}";
    }
}

namespace Avalonia.DBus.WireMarshalling.Tests.Abstractions;

public interface ISignatureBuilder
{
    string GetSignature(Type type);
    string MakeArraySignature(string elementSig);
    string MakeStructSignature(params string[] fieldSigs);
    string MakeDictSignature(string keySig, string valueSig);
}

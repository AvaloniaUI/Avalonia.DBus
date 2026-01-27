using System;
using System.Collections.Generic;

namespace Avalonia.DBus.Wire;

internal static class DBusSignatureParser
{
    internal static string ReadSingleType(string signature, ref int index)
    {
        if (signature == null)
        {
            throw new ArgumentNullException(nameof(signature));
        }

        if (index < 0 || index >= signature.Length)
        {
            throw new ArgumentException("Signature index is out of range.", nameof(index));
        }

        int start = index;
        char token = signature[index++];
        switch (token)
        {
            case 'a':
                ReadSingleType(signature, ref index);
                break;
            case '(':
                while (index < signature.Length && signature[index] != ')')
                {
                    ReadSingleType(signature, ref index);
                }
                if (index >= signature.Length || signature[index] != ')')
                {
                    throw new ArgumentException("Struct signature is not closed.", nameof(signature));
                }
                index++;
                break;
            case '{':
                ReadSingleType(signature, ref index);
                ReadSingleType(signature, ref index);
                if (index >= signature.Length || signature[index] != '}')
                {
                    throw new ArgumentException("Dict entry signature is not closed.", nameof(signature));
                }
                index++;
                break;
        }

        return signature.Substring(start, index - start);
    }

    internal static IReadOnlyList<string> ParseStructSignatures(string signature)
    {
        if (string.IsNullOrEmpty(signature) || signature[0] != '(')
        {
            throw new ArgumentException("Struct signature is invalid.", nameof(signature));
        }

        int index = 1;
        List<string> parts = new();
        while (index < signature.Length && signature[index] != ')')
        {
            parts.Add(ReadSingleType(signature, ref index));
        }

        if (index >= signature.Length || signature[index] != ')')
        {
            throw new ArgumentException("Struct signature is not closed.", nameof(signature));
        }

        index++;
        if (index != signature.Length)
        {
            throw new ArgumentException("Struct signature contains trailing data.", nameof(signature));
        }

        return parts;
    }

    internal static (string KeySignature, string ValueSignature) ParseDictEntrySignatures(string signature)
    {
        if (string.IsNullOrEmpty(signature) || signature[0] != '{')
        {
            throw new ArgumentException("Dict entry signature is invalid.", nameof(signature));
        }

        int index = 1;
        string keySignature = ReadSingleType(signature, ref index);
        string valueSignature = ReadSingleType(signature, ref index);
        if (index >= signature.Length || signature[index] != '}')
        {
            throw new ArgumentException("Dict entry signature is not closed.", nameof(signature));
        }

        index++;
        if (index != signature.Length)
        {
            throw new ArgumentException("Dict entry signature contains trailing data.", nameof(signature));
        }

        return (keySignature, valueSignature);
    }
}

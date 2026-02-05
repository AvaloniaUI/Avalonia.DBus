using System;
using System.Diagnostics;

namespace Avalonia.DBus.Native;

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
[Conditional("DEBUG")]
internal sealed class NativeTypeNameAttribute : Attribute
{
    internal string Name { get; }

    internal NativeTypeNameAttribute(string name)
    {
        Name = name;
    }
}
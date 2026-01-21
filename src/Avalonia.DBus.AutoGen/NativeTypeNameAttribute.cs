using System;
using System.Diagnostics;

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
[Conditional("DEBUG")]
internal sealed class NativeTypeNameAttribute : Attribute
{
	public string Name { get; }

	public NativeTypeNameAttribute(string name)
	{
		Name = name;
	}
}

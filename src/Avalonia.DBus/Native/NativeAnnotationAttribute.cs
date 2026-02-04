using System;
using System.Diagnostics;

namespace Avalonia.DBus.Native;

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue, AllowMultiple = true)]
[Conditional("DEBUG")]
internal sealed class NativeAnnotationAttribute : Attribute
{
	internal string Annotation { get; }

	internal NativeAnnotationAttribute(string annotation)
	{
		Annotation = annotation;
	}
}
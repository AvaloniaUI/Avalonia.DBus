using System.Runtime.InteropServices;
using Xunit;

namespace Avalonia.DBus.Tests.Helpers;

public sealed class SkipUnlessMacOsAttribute : FactAttribute
{
    public SkipUnlessMacOsAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Skip = "This test requires macOS.";
    }
}
using System.Runtime.InteropServices;
using Xunit;

namespace Avalonia.DBus.Tests.Helpers;

public sealed class SkipUnlessLinuxAttribute : FactAttribute
{
    public SkipUnlessLinuxAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Skip = "This test requires Linux.";
    }
}
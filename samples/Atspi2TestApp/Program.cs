using System.Diagnostics;
using System.Globalization;

namespace Atspi2TestApp;

internal static class Program
{
    private static readonly Stopwatch s_uptime = Stopwatch.StartNew();

    internal const string RootPath = "/org/a11y/atspi/accessible/root";
    internal const string CachePath = "/org/a11y/atspi/cache";
    internal const string NullPath = "/org/a11y/atspi/null";
    internal const string AppPathPrefix = "/org/avalonia/Atspi2TestApp/a11y";
    internal const string RegistryPath = "/org/a11y/atspi/registry";

    internal const string IfaceAccessible = "org.a11y.atspi.Accessible";
    internal const string IfaceApplication = "org.a11y.atspi.Application";
    internal const string IfaceComponent = "org.a11y.atspi.Component";
    internal const string IfaceAction = "org.a11y.atspi.Action";
    internal const string IfaceValue = "org.a11y.atspi.Value";
    internal const string IfaceEventObject = "org.a11y.atspi.Event.Object";
    internal const string IfaceCache = "org.a11y.atspi.Cache";
    internal const string IfaceImage = "org.a11y.atspi.Image";

    internal const string BusNameRegistry = "org.a11y.atspi.Registry";
    internal const string BusNameA11y = "org.a11y.Bus";
    internal const string PathA11y = "/org/a11y/bus";

    internal const uint AccessibleVersion = 1;
    internal const uint ApplicationVersion = 1;
    internal const uint ComponentVersion = 1;
    internal const uint ActionVersion = 1;
    internal const uint ValueVersion = 1;
    internal const uint EventObjectVersion = 1;
    internal const uint CacheVersion = 1;
    internal const uint ImageVersion = 1;

    internal const int RoleApplication = 75;
    internal const int RoleFrame = 23;
    internal const int RoleLabel = 29;
    internal const int RoleEntry = 79;
    internal const int RoleCheckBox = 7;
    internal const int RoleButton = 43;
    internal const int RoleSlider = 51;

    internal const uint StateActive = 1;
    internal const uint StateEnabled = 8;
    internal const uint StateFocusable = 11;
    internal const uint StateFocused = 12;
    internal const uint StateEditable = 7;
    internal const uint StateSensitive = 24;
    internal const uint StateShowing = 25;
    internal const uint StateVisible = 30;
    internal const uint StateCheckable = 41;
    internal const uint StateChecked = 4;

    internal static List<uint> BuildStateSet(IReadOnlyCollection<uint> states)
    {
        if (states == null || states.Count == 0)
        {
            return [0u, 0u];
        }

        uint low = 0;
        uint high = 0;
        foreach (var state in states)
        {
            if (state < 32)
            {
                low |= 1u << (int)state;
            }
            else if (state < 64)
            {
                high |= 1u << (int)(state - 32);
            }
        }

        return [low, high];
    }

    internal static string ResolveLocale()
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        if (string.IsNullOrWhiteSpace(culture))
        {
            culture = "en_US";
        }

        return culture.Replace('-', '_');
    }

    internal static string ResolveToolkitVersion()
    {
        return typeof(Program).Assembly.GetName().Version?.ToString() ?? "0";
    }

    public static async Task Main(string[] args)
    {
        var diagnostics = new DBusConsoleDiagnostics();
        var tree = new AtspiTree();
        var server = new AtspiServer(tree, diagnostics);
        Environment.ExitCode = await server.RunAsync();
    }

    internal static void LogVerbose(string message)
    {
#if DEBUG
        Console.Error.WriteLine($@"[{s_uptime.Elapsed:hh\:mm\:ss\.fff}] {message}");
#endif
    }
}
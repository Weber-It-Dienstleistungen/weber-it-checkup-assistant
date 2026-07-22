using System.Diagnostics;

namespace WeberIT.Checkup.App.Services.Cleanup;

internal static class BrowserCacheRuntimeGuard
{
    private static readonly IReadOnlyList<
        BrowserProcessDefinition>
        SupportedProcesses =
            new List<BrowserProcessDefinition>
            {
                new(
                    "msedge",
                    "Microsoft Edge"),

                new(
                    "chrome",
                    "Google Chrome"),

                new(
                    "firefox",
                    "Mozilla Firefox"),

                new(
                    "MicrosoftEdge",
                    "Microsoft Edge"),

                new(
                    "MicrosoftEdgeCP",
                    "Microsoft Edge")
            };

    public static BrowserCacheProcessState Evaluate()
    {
        if (!OperatingSystem.IsWindows())
        {
            return BrowserCacheProcessState.Failed(
                "Der Status laufender Browser kann nur "
                + "unter Windows sicher geprüft werden.");
        }

        var runningBrowsers =
            new HashSet<string>(
                StringComparer.CurrentCultureIgnoreCase);

        try
        {
            foreach (var definition
                     in SupportedProcesses)
            {
                var processes =
                    Process.GetProcessesByName(
                        definition.ProcessName);

                try
                {
                    foreach (var process
                             in processes)
                    {
                        try
                        {
                            if (!process.HasExited)
                            {
                                runningBrowsers.Add(
                                    definition.DisplayName);
                            }
                        }
                        catch
                        {
                            runningBrowsers.Add(
                                definition.DisplayName);
                        }
                    }
                }
                finally
                {
                    foreach (var process
                             in processes)
                    {
                        process.Dispose();
                    }
                }
            }
        }
        catch (Exception exception)
        {
            return BrowserCacheProcessState.Failed(
                "Der Status laufender Browserprozesse "
                + "konnte nicht zuverlässig geprüft werden. "
                + exception.Message);
        }

        return new BrowserCacheProcessState
        {
            RunningBrowserNames =
                runningBrowsers
                    .OrderBy(
                        name =>
                            name,
                        StringComparer.CurrentCultureIgnoreCase)
                    .ToList()
        };
    }

    private sealed record BrowserProcessDefinition(
        string ProcessName,
        string DisplayName);
}

internal sealed class BrowserCacheProcessState
{
    public IReadOnlyList<string> RunningBrowserNames
    {
        get;
        init;
    } = Array.Empty<string>();

    public string ErrorMessage { get; init; } =
        string.Empty;

    public bool HasRunningBrowsers =>
        RunningBrowserNames.Count > 0;

    public bool CanProceed =>
        string.IsNullOrWhiteSpace(
            ErrorMessage)
        && !HasRunningBrowsers;

    public string BlockingMessage
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(
                    ErrorMessage))
            {
                return ErrorMessage;
            }

            if (!HasRunningBrowsers)
            {
                return string.Empty;
            }

            var browserText =
                string.Join(
                    ", ",
                    RunningBrowserNames);

            return
                "Die Browsercache-Bereinigung wurde nicht "
                + "gestartet, weil noch unterstützte "
                + "Browserprozesse laufen:"
                + Environment.NewLine
                + Environment.NewLine
                + browserText
                + Environment.NewLine
                + Environment.NewLine
                + "Schließe die Browser vollständig und beende "
                + "gegebenenfalls deren Hintergrundbetrieb. "
                + "Das Checkup-Tool beendet keine Prozesse selbst.";
        }
    }

    public static BrowserCacheProcessState Failed(
        string errorMessage)
    {
        return new BrowserCacheProcessState
        {
            ErrorMessage =
                errorMessage
        };
    }
}
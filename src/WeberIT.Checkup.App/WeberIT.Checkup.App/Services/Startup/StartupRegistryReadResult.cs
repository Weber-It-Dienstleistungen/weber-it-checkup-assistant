using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Startup;

public class StartupRegistryReadResult
{
    public List<StartupEntryInformation> Entries { get; set; } =
        new();

    public int SuccessfulSourceCount { get; set; }

    public int FailedSourceCount { get; set; }

    public bool WasTimedOut { get; set; }

    public bool HasErrors =>
        FailedSourceCount > 0;
}
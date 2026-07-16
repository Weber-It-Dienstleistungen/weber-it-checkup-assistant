using System.Diagnostics;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Startup;

public class StartupInformationProvider :
    IStartupInformationProvider
{
    private static readonly TimeSpan AnalysisTimeLimit =
        TimeSpan.FromSeconds(5);

    private readonly StartupRegistrySourceReader
        _registrySourceReader;

    private readonly StartupFolderSourceReader
        _folderSourceReader;

    public StartupInformationProvider(
        StartupRegistrySourceReader registrySourceReader,
        StartupFolderSourceReader folderSourceReader)
    {
        _registrySourceReader =
            registrySourceReader;

        _folderSourceReader =
            folderSourceReader;
    }

    public StartupInformation Analyze()
    {
        var stopwatch =
            Stopwatch.StartNew();

        var information =
            new StartupInformation
            {
                AnalysisDate =
                    DateTime.Now
            };

        var deadline =
            DateTime.UtcNow
                .Add(
                    AnalysisTimeLimit);

        var registryResult =
            _registrySourceReader.Read(
                deadline);

        StartupFolderReadResult folderResult;

        if (DateTime.UtcNow
            >= deadline)
        {
            folderResult =
                new StartupFolderReadResult
                {
                    WasTimedOut =
                        true
                };
        }
        else
        {
            folderResult =
                _folderSourceReader.Read(
                    deadline);
        }

        information.Entries.AddRange(
            registryResult.Entries);

        information.Entries.AddRange(
            folderResult.Entries);

        information.Entries =
            OrderEntries(
                information.Entries);

        ApplyAnalysisResult(
            information,
            registryResult,
            folderResult);

        stopwatch.Stop();

        information.AnalysisDurationMilliseconds =
            stopwatch.ElapsedMilliseconds;

        return information;
    }

    private static void ApplyAnalysisResult(
        StartupInformation information,
        StartupRegistryReadResult registryResult,
        StartupFolderReadResult folderResult)
    {
        var wasTimedOut =
            registryResult.WasTimedOut
            || folderResult.WasTimedOut;

        var successfulSourceCount =
            registryResult.SuccessfulSourceCount
            + folderResult.SuccessfulSourceCount;

        var failedSourceCount =
            registryResult.FailedSourceCount
            + folderResult.FailedSourceCount;

        if (wasTimedOut)
        {
            information.AnalysisStatus =
                StartupAnalysisStatus.TimedOut;

            information.AnalysisMessage =
                "Das Zeitlimit der Autostartanalyse wurde erreicht. "
                + "Die bis dahin zuverlässig ermittelten Einträge "
                + "werden angezeigt; das Ergebnis kann unvollständig sein.";

            return;
        }

        if (successfulSourceCount == 0
            && failedSourceCount > 0)
        {
            information.AnalysisStatus =
                StartupAnalysisStatus.NotEvaluable;

            information.AnalysisMessage =
                "Die lokalen Autostartquellen konnten nicht "
                + "zuverlässig ausgewertet werden.";

            return;
        }

        if (failedSourceCount > 0)
        {
            information.AnalysisStatus =
                StartupAnalysisStatus.PartiallyAnalyzed;

            information.AnalysisMessage =
                "Die Autostartsituation wurde teilweise analysiert. "
                + "Mindestens eine lokale Quelle konnte nicht "
                + "zuverlässig ausgewertet werden.";

            return;
        }

        information.AnalysisStatus =
            StartupAnalysisStatus.Analyzed;

        information.AnalysisMessage =
            information.Entries.Count == 0
                ? "In den unterstützten lokalen Quellen wurden "
                  + "keine Autostarteinträge erkannt."
                : $"{information.Entries.Count} Autostarteinträge "
                  + "wurden in den unterstützten lokalen Quellen erkannt.";
    }

    private static List<StartupEntryInformation> OrderEntries(
        IEnumerable<StartupEntryInformation> entries)
    {
        return entries
            .OrderByDescending(
                entry =>
                    entry.State
                    == StartupEntryState.Enabled)
            .ThenByDescending(
                entry =>
                    entry.Classification
                    == StartupClassification.Conspicuous)
            .ThenBy(
                entry =>
                    entry.DisplayName,
                StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(
                entry =>
                    entry.SourceType)
            .ThenBy(
                entry =>
                    entry.Context)
            .ThenBy(
                entry =>
                    entry.RegistryView)
            .ToList();
    }
}
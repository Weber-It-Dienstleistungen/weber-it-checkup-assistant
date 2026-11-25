using Microsoft.Win32;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Startup;

public class StartupRegistrySourceReader
{
    private const string RunPath =
        @"Software\Microsoft\Windows\CurrentVersion\Run";

    private const string RunOncePath =
        @"Software\Microsoft\Windows\CurrentVersion\RunOnce";

    private readonly StartupCommandAnalyzer
        _commandAnalyzer;

    public StartupRegistrySourceReader(
        StartupCommandAnalyzer commandAnalyzer)
    {
        _commandAnalyzer =
            commandAnalyzer;
    }

    public StartupRegistryReadResult Read(
        DateTime deadline)
    {
        var result =
            new StartupRegistryReadResult();

        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        {
            var context = hive == RegistryHive.CurrentUser
                ? StartupEntryContext.CurrentUser : StartupEntryContext.AllUsers;

            foreach (var view in StartupApprovalReader.GetSourceViews(
                         hive, Environment.Is64BitOperatingSystem))
            {
                ReadRegistrySource(hive, view, RunPath, StartupSourceType.RegistryRun,
                    context, result, deadline);
                ReadRegistrySource(hive, view, RunOncePath, StartupSourceType.RegistryRunOnce,
                    context, result, deadline);
            }
        }

        result.Entries =
            RemoveExactSourceDuplicates(
                result.Entries);

        return result;
    }

    private void ReadRegistrySource(
        RegistryHive registryHive,
        RegistryView registryView,
        string registryPath,
        StartupSourceType sourceType,
        StartupEntryContext context,
        StartupRegistryReadResult result,
        DateTime deadline)
    {
        if (DateTime.UtcNow
            >= deadline)
        {
            result.WasTimedOut =
                true;

            return;
        }

        try
        {
            using var baseKey =
                RegistryKey.OpenBaseKey(
                    registryHive,
                    registryView);

            using var startupKey =
                baseKey.OpenSubKey(
                    registryPath,
                    writable: false);

            if (startupKey is null)
            {
                result.SuccessfulSourceCount++;
                return;
            }

            var valueNames =
                startupKey.GetValueNames();

            foreach (var valueName
                     in valueNames)
            {
                if (DateTime.UtcNow
                    >= deadline)
                {
                    result.WasTimedOut =
                        true;

                    return;
                }

                ReadRegistryEntry(
                    registryHive,
                    startupKey,
                    valueName,
                    registryView,
                    sourceType,
                    context,
                    result);
            }

            result.SuccessfulSourceCount++;
        }
        catch
        {
            result.FailedSourceCount++;
        }
    }

    private void ReadRegistryEntry(
        RegistryHive registryHive,
        RegistryKey startupKey,
        string valueName,
        RegistryView registryView,
        StartupSourceType sourceType,
        StartupEntryContext context,
        StartupRegistryReadResult result)
    {
        try
        {
            var command =
                startupKey.GetValue(
                    valueName,
                    null,
                    RegistryValueOptions
                        .DoNotExpandEnvironmentNames)
                ?.ToString()
                ?? string.Empty;

            var state =
                StartupApprovalReader.Read(
                    registryHive, registryView, sourceType, valueName);

            var entry =
                _commandAnalyzer.Analyze(
                    valueName,
                    command,
                    sourceType,
                    context,
                    ConvertRegistryView(registryView, context),
                    state);

            result.Entries.Add(
                entry);
        }
        catch
        {
            result.Entries.Add(
                CreateFailedEntry(
                    valueName,
                    sourceType,
                    context,
                    registryView));
        }
    }

    private static StartupEntryInformation CreateFailedEntry(
        string valueName,
        StartupSourceType sourceType,
        StartupEntryContext context,
        RegistryView registryView)
    {
        return new StartupEntryInformation
        {
            DisplayName =
                string.IsNullOrWhiteSpace(
                    valueName)
                    ? "Unbekannter Autostarteintrag"
                    : valueName.Trim(),

            NormalizedProgramName =
                string.IsNullOrWhiteSpace(
                    valueName)
                    ? string.Empty
                    : valueName.Trim(),

            SourceType =
                sourceType,

            Context =
                context,

            RegistryView =
                ConvertRegistryView(registryView, context),

            State =
                StartupEntryState.Unknown,

            TargetType =
                StartupTargetType.Unknown,

            TargetExists =
                null,

            Classification =
                StartupClassification.NotEvaluable,

            Description =
                "Der Registry-Autostarteintrag konnte nicht "
                + "vollständig ausgewertet werden."
        };
    }

    private static StartupRegistryView ConvertRegistryView(
        RegistryView registryView, StartupEntryContext context)
    {
        if (context == StartupEntryContext.CurrentUser)
            return StartupRegistryView.Shared;

        return registryView switch
        {
            RegistryView.Registry32 =>
                StartupRegistryView.Registry32,

            RegistryView.Registry64 =>
                StartupRegistryView.Registry64,

            _ =>
                StartupRegistryView.NotApplicable
        };
    }

    private static List<StartupEntryInformation>
        RemoveExactSourceDuplicates(
            IEnumerable<StartupEntryInformation> entries)
    {
        var uniqueEntries =
            new List<StartupEntryInformation>();

        var identifiers =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(
                    entry.SourceIdentifier))
            {
                uniqueEntries.Add(
                    entry);

                continue;
            }

            if (identifiers.Add(
                    entry.SourceIdentifier))
            {
                uniqueEntries.Add(
                    entry);
            }
        }

        return uniqueEntries;
    }
}
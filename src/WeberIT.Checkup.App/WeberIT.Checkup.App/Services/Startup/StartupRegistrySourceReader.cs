using Microsoft.Win32;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Startup;

public class StartupRegistrySourceReader
{
    private const string RunPath =
        @"Software\Microsoft\Windows\CurrentVersion\Run";

    private const string RunOncePath =
        @"Software\Microsoft\Windows\CurrentVersion\RunOnce";

    private const string StartupApprovedRunPath =
        @"Software\Microsoft\Windows\CurrentVersion"
        + @"\Explorer\StartupApproved\Run";

    private const string StartupApprovedRun32Path =
        @"Software\Microsoft\Windows\CurrentVersion"
        + @"\Explorer\StartupApproved\Run32";

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

        ReadRegistrySource(
            RegistryHive.CurrentUser,
            RegistryView.Registry64,
            RunPath,
            StartupSourceType.RegistryRun,
            StartupEntryContext.CurrentUser,
            result,
            deadline);

        ReadRegistrySource(
            RegistryHive.CurrentUser,
            RegistryView.Registry32,
            RunPath,
            StartupSourceType.RegistryRun,
            StartupEntryContext.CurrentUser,
            result,
            deadline);

        ReadRegistrySource(
            RegistryHive.CurrentUser,
            RegistryView.Registry64,
            RunOncePath,
            StartupSourceType.RegistryRunOnce,
            StartupEntryContext.CurrentUser,
            result,
            deadline);

        ReadRegistrySource(
            RegistryHive.CurrentUser,
            RegistryView.Registry32,
            RunOncePath,
            StartupSourceType.RegistryRunOnce,
            StartupEntryContext.CurrentUser,
            result,
            deadline);

        ReadRegistrySource(
            RegistryHive.LocalMachine,
            RegistryView.Registry64,
            RunPath,
            StartupSourceType.RegistryRun,
            StartupEntryContext.AllUsers,
            result,
            deadline);

        ReadRegistrySource(
            RegistryHive.LocalMachine,
            RegistryView.Registry32,
            RunPath,
            StartupSourceType.RegistryRun,
            StartupEntryContext.AllUsers,
            result,
            deadline);

        ReadRegistrySource(
            RegistryHive.LocalMachine,
            RegistryView.Registry64,
            RunOncePath,
            StartupSourceType.RegistryRunOnce,
            StartupEntryContext.AllUsers,
            result,
            deadline);

        ReadRegistrySource(
            RegistryHive.LocalMachine,
            RegistryView.Registry32,
            RunOncePath,
            StartupSourceType.RegistryRunOnce,
            StartupEntryContext.AllUsers,
            result,
            deadline);

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
                    baseKey,
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
        RegistryKey baseKey,
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
                DetermineEntryState(
                    baseKey,
                    valueName,
                    registryView,
                    sourceType);

            var entry =
                _commandAnalyzer.Analyze(
                    valueName,
                    command,
                    sourceType,
                    context,
                    ConvertRegistryView(
                        registryView),
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

    private static StartupEntryState DetermineEntryState(
        RegistryKey baseKey,
        string valueName,
        RegistryView registryView,
        StartupSourceType sourceType)
    {
        if (sourceType
            == StartupSourceType.RegistryRunOnce)
        {
            return StartupEntryState.Enabled;
        }

        var startupApprovedPath =
            registryView == RegistryView.Registry32
                ? StartupApprovedRun32Path
                : StartupApprovedRunPath;

        try
        {
            using var startupApprovedKey =
                baseKey.OpenSubKey(
                    startupApprovedPath,
                    writable: false);

            if (startupApprovedKey is null)
            {
                return StartupEntryState.Enabled;
            }

            var stateValue =
                startupApprovedKey.GetValue(
                    valueName);

            if (stateValue is not byte[] stateBytes
                || stateBytes.Length == 0)
            {
                return StartupEntryState.Enabled;
            }

            return stateBytes[0] switch
            {
                0x02 =>
                    StartupEntryState.Enabled,

                0x03 =>
                    StartupEntryState.Disabled,

                _ =>
                    StartupEntryState.Unknown
            };
        }
        catch
        {
            return StartupEntryState.Unknown;
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
                ConvertRegistryView(
                    registryView),

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
        RegistryView registryView)
    {
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
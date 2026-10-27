
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Reports;

/// <summary>
/// Fachlich vorsichtige Auswertung der bereits
/// gespeicherten WinGet-Einzelbefehle.
///
/// Die ursprünglichen Aktionsdaten bleiben
/// unverändert.
///
/// Es werden ausschließlich ausdrücklich
/// protokollierte Befehlsergebnisse ausgewertet.
///
/// Exitcode 0 bedeutet einen technisch
/// erfolgreichen WinGet-Aufruf, jedoch keinen
/// durch einen Nachscan bestätigten
/// Installationszustand.
/// </summary>
internal static class CustomerCheckupProgramUpdateExecutionReport
{
    private const string SupportedActionCode =
        "action.program-updates.selected-upgrades";

    private static readonly Regex SelectedPackagePattern =
        new(
            @"^\s*•\s*(?<name>.+)\s+\((?<id>[^()]+)\)(?=\s*:|\s*→|\s*$)",
            RegexOptions.Compiled
            | RegexOptions.CultureInvariant);

    private static readonly Regex CommandPattern =
        new(
            "^\\s*winget\\.exe\\s+upgrade\\s+--id\\s+"
            + "(?:\"(?<quoted>[^\"]+)\"|(?<plain>\\S+))"
            + "\\s+--version\\s+",
            RegexOptions.Compiled
            | RegexOptions.IgnoreCase
            | RegexOptions.CultureInvariant);

    public static CustomerCheckupProgramUpdateExecutionOverview
        Create(
            IEnumerable<CustomerCheckupActionSummary> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var packages =
            new Dictionary<string, MutablePackage>(
                StringComparer.OrdinalIgnoreCase);

        var relevantActions =
            actions
                .Where(action =>
                    action is not null
                    && string.Equals(
                        action.ActionCode,
                        SupportedActionCode,
                        StringComparison.Ordinal))
                .OrderBy(action =>
                    action.StartedAt ?? DateTimeOffset.MaxValue)
                .ToList();

        foreach (var action in relevantActions)
        {
            RegisterSelectedPackages(
                packages,
                action.TargetDescription);

            RegisterRecordedCommands(
                packages,
                action.Details);
        }

        var entries =
            packages.Values
                .Select(CreateEntry)
                .OrderBy(entry =>
                    GetDisplayOrder(entry.Status))
                .ThenBy(
                    entry => entry.PackageName,
                    StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(
                    entry => entry.PackageId,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        return new CustomerCheckupProgramUpdateExecutionOverview
        {
            ActionCount = relevantActions.Count,
            Entries = entries
        };
    }

    private static void RegisterSelectedPackages(
        IDictionary<string, MutablePackage> packages,
        string? description)
    {
        foreach (var line in ReadLines(description))
        {
            var match =
                SelectedPackagePattern.Match(line);

            if (!match.Success)
            {
                continue;
            }

            var packageId =
                match.Groups["id"].Value.Trim();

            var packageName =
                match.Groups["name"].Value.Trim();

            if (packageId.Length == 0)
            {
                continue;
            }

            var package =
                GetOrCreatePackage(
                    packages,
                    packageId);

            package.WasSelected = true;

            if (packageName.Length > 0)
            {
                package.PackageName =
                    packageName;
            }
        }
    }

    private static void RegisterRecordedCommands(
        IDictionary<string, MutablePackage> packages,
        string? details)
    {
        string? currentPackageId = null;

        bool? wasStarted = null;

        int? exitCode = null;

        void CompleteCurrentCommand()
        {
            if (string.IsNullOrWhiteSpace(
                    currentPackageId))
            {
                return;
            }

            var package =
                GetOrCreatePackage(
                    packages,
                    currentPackageId);

            package.HasRecordedCommand = true;
            package.RecordedCommandCount++;

            if (wasStarted == false)
            {
                package.HasNotStartedCommand = true;
            }
            else if (wasStarted == true
                     && exitCode == 0)
            {
                package.HasSuccessfulCommand = true;
                package.SuccessfulCommandCount++;
            }
            else if (wasStarted == true
                     && exitCode.HasValue)
            {
                package.HasFailedCommand = true;
                package.FailedCommandCount++;
                package.LastFailureExitCode =
                    exitCode.Value;
            }
            else
            {
                package.HasUnclearCommand = true;
            }

            currentPackageId = null;
            wasStarted = null;
            exitCode = null;
        }

        foreach (var line in ReadLines(details))
        {
            var commandMatch =
                CommandPattern.Match(line);

            if (commandMatch.Success)
            {
                CompleteCurrentCommand();

                var packageId =
                    commandMatch.Groups["quoted"].Success
                        ? commandMatch.Groups["quoted"]
                            .Value.Trim()
                        : commandMatch.Groups["plain"]
                            .Value.Trim();

                if (packageId.Length > 0)
                {
                    currentPackageId =
                        packageId;
                }

                continue;
            }

            if (currentPackageId is null)
            {
                continue;
            }

            var trimmedLine =
                line.Trim();

            if (trimmedLine.StartsWith(
                    "Gestartet:",
                    StringComparison.OrdinalIgnoreCase))
            {
                var value =
                    trimmedLine["Gestartet:".Length..]
                        .Trim();

                wasStarted =
                    value.Equals(
                        "Ja",
                        StringComparison.OrdinalIgnoreCase)
                        ? true
                        : value.Equals(
                            "Nein",
                            StringComparison.OrdinalIgnoreCase)
                            ? false
                            : null;

                continue;
            }

            if (trimmedLine.StartsWith(
                    "Exitcode:",
                    StringComparison.OrdinalIgnoreCase))
            {
                var value =
                    trimmedLine["Exitcode:".Length..]
                        .Trim();

                if (int.TryParse(
                        value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var parsedExitCode))
                {
                    exitCode = parsedExitCode;
                }
            }
        }

        CompleteCurrentCommand();
    }

    private static CustomerCheckupProgramUpdateExecutionEntry
        CreateEntry(
            MutablePackage package)
    {
        /*
         * Ein dokumentierter technischer Erfolg
         * wird auch dann nicht unterschlagen,
         * wenn andere Versuche mit demselben
         * Paket zuvor fehlgeschlagen sind.
         *
         * Der Status bezieht sich ausdrücklich
         * nur auf den protokollierten Befehl,
         * nicht auf den endgültigen
         * Installationszustand.
         */
        var status =
            package.HasSuccessfulCommand
                ? CustomerCheckupProgramUpdateExecutionStatus
                    .TechnicallySuccessful
                : package.HasFailedCommand
                    ? CustomerCheckupProgramUpdateExecutionStatus
                        .Failed
                    : package.HasNotStartedCommand
                        ? CustomerCheckupProgramUpdateExecutionStatus
                            .NotStarted
                        : package.HasUnclearCommand
                            ? CustomerCheckupProgramUpdateExecutionStatus
                                .NotVerifiable
                            : package.WasSelected
                                && !package.HasRecordedCommand
                                    ? CustomerCheckupProgramUpdateExecutionStatus
                                        .NotExecutedOrUnrecorded
                                    : CustomerCheckupProgramUpdateExecutionStatus
                                        .NotVerifiable;

        return new CustomerCheckupProgramUpdateExecutionEntry
        {
            PackageId =
                package.PackageId,

            PackageName =
                string.IsNullOrWhiteSpace(
                    package.PackageName)
                    ? package.PackageId
                    : package.PackageName,

            Status =
                status,

            SuccessfulCommandCount =
                package.SuccessfulCommandCount,

            FailedCommandCount =
                package.FailedCommandCount,

            RecordedCommandCount =
                package.RecordedCommandCount,

            LastFailureExitCode =
                package.LastFailureExitCode
        };
    }

    private static MutablePackage GetOrCreatePackage(
        IDictionary<string, MutablePackage> packages,
        string packageId)
    {
        if (packages.TryGetValue(
                packageId,
                out var existing))
        {
            return existing;
        }

        var package =
            new MutablePackage
            {
                PackageId =
                    packageId,

                PackageName =
                    packageId
            };

        packages.Add(
            packageId,
            package);

        return package;
    }

    private static IEnumerable<string> ReadLines(
        string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        using var reader =
            new StringReader(text);

        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    private static int GetDisplayOrder(
        CustomerCheckupProgramUpdateExecutionStatus status)
    {
        return status switch
        {
            CustomerCheckupProgramUpdateExecutionStatus
                .TechnicallySuccessful => 0,

            CustomerCheckupProgramUpdateExecutionStatus
                .Failed => 1,

            CustomerCheckupProgramUpdateExecutionStatus
                .NotStarted => 2,

            CustomerCheckupProgramUpdateExecutionStatus
                .NotExecutedOrUnrecorded => 3,

            _ => 4
        };
    }

    private sealed class MutablePackage
    {
        public string PackageId { get; init; } =
            string.Empty;

        public string PackageName { get; set; } =
            string.Empty;

        public bool WasSelected { get; set; }

        public bool HasRecordedCommand { get; set; }

        public bool HasSuccessfulCommand { get; set; }

        public bool HasFailedCommand { get; set; }

        public bool HasNotStartedCommand { get; set; }

        public bool HasUnclearCommand { get; set; }

        public int RecordedCommandCount { get; set; }

        public int SuccessfulCommandCount { get; set; }

        public int FailedCommandCount { get; set; }

        public int? LastFailureExitCode { get; set; }
    }
}

internal enum CustomerCheckupProgramUpdateExecutionStatus
{
    TechnicallySuccessful,
    Failed,
    NotStarted,
    NotExecutedOrUnrecorded,
    NotVerifiable
}

internal sealed class CustomerCheckupProgramUpdateExecutionEntry
{
    public string PackageId { get; init; } =
        string.Empty;

    public string PackageName { get; init; } =
        string.Empty;

    public CustomerCheckupProgramUpdateExecutionStatus Status
    {
        get;
        init;
    }

    public int RecordedCommandCount { get; init; }

    public int SuccessfulCommandCount { get; init; }

    public int FailedCommandCount { get; init; }

    public int? LastFailureExitCode { get; init; }
}

internal sealed class CustomerCheckupProgramUpdateExecutionOverview
{
    public int ActionCount { get; init; }

    public IReadOnlyList<CustomerCheckupProgramUpdateExecutionEntry>
        Entries
    { get; init; } =
            Array.Empty<CustomerCheckupProgramUpdateExecutionEntry>();

    public int TechnicallySuccessfulPackageCount =>
        Entries.Count(entry =>
            entry.Status ==
            CustomerCheckupProgramUpdateExecutionStatus
                .TechnicallySuccessful);

    public int FailedPackageCount =>
        Entries.Count(entry =>
            entry.Status ==
            CustomerCheckupProgramUpdateExecutionStatus
                .Failed);

    public int NotStartedPackageCount =>
        Entries.Count(entry =>
            entry.Status ==
            CustomerCheckupProgramUpdateExecutionStatus
                .NotStarted);

    public int NotExecutedOrUnrecordedPackageCount =>
        Entries.Count(entry =>
            entry.Status ==
            CustomerCheckupProgramUpdateExecutionStatus
                .NotExecutedOrUnrecorded);

    public int NotVerifiablePackageCount =>
        Entries.Count(entry =>
            entry.Status ==
            CustomerCheckupProgramUpdateExecutionStatus
                .NotVerifiable);

    public bool HasEntries =>
        Entries.Count > 0;
}

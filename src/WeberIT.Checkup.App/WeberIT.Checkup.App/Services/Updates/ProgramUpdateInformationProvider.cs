using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Updates;

public class ProgramUpdateInformationProvider :
    IProgramUpdateInformationProvider
{
    private const int MaximumStoredUpdates = 50;
    private const int VersionTimeoutMilliseconds = 15000;
    private const int AnalysisTimeoutMilliseconds = 120000;

    private static readonly Regex AnsiEscapeSequenceRegex =
        new(
            @"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])",
            RegexOptions.Compiled);

    private static readonly Regex HeaderColumnRegex =
        new(
            @"\S+",
            RegexOptions.Compiled);

    private static readonly Regex ReportedUpdateCountRegex =
        new(
            @"^\s*(?<count>\d+)\s+.*(?:Aktualisierungen?|Upgrades?)\s+(?:verfügbar|available)",
            RegexOptions.Compiled
            | RegexOptions.IgnoreCase
            | RegexOptions.Multiline
            | RegexOptions.CultureInvariant);

    public ProgramUpdateInformation GetProgramUpdateInformation()
    {
        var information =
            new ProgramUpdateInformation();

        var versionResult =
            RunWinget(
                ["--version"],
                VersionTimeoutMilliseconds);

        if (!versionResult.WasStarted)
        {
            information.IsWingetAvailable = false;
            information.AnalysisDetails =
                BuildUnavailableDetails(
                    versionResult);

            return information;
        }

        if (versionResult.TimedOut)
        {
            information.IsWingetAvailable = null;
            information.AnalysisDetails =
                "Die Verfügbarkeit von WinGet konnte nicht rechtzeitig "
                + "ermittelt werden. Der Versionsaufruf hat das Zeitlimit "
                + "überschritten.";

            return information;
        }

        if (versionResult.ExitCode != 0)
        {
            information.IsWingetAvailable = null;
            information.AnalysisDetails =
                BuildFailureDetails(
                    "WinGet wurde gefunden, der Versionsaufruf wurde jedoch "
                    + "mit einem Fehler beendet.",
                    versionResult);

            return information;
        }

        information.IsWingetAvailable = true;
        information.WingetVersion =
            CleanOutput(versionResult.StandardOutput)
                .Trim();

        information.IsAnalysisPerformed = true;
        information.AnalysisDate = DateTime.Now;

        var analysisResult =
            RunWinget(
                [
                    "upgrade",
                    "--source",
                    "winget",
                    "--disable-interactivity"
                ],
                AnalysisTimeoutMilliseconds);

        if (!analysisResult.WasStarted)
        {
            information.IsAnalysisSuccessful = false;
            information.AnalysisDetails =
                BuildFailureDetails(
                    "Die WinGet-Programupdate-Analyse konnte nicht gestartet werden.",
                    analysisResult);

            return information;
        }

        if (analysisResult.TimedOut)
        {
            information.IsAnalysisSuccessful = false;
            information.AnalysisDetails =
                "Die WinGet-Programupdate-Analyse wurde nach zwei Minuten "
                + "beendet, weil sie nicht rechtzeitig abgeschlossen wurde. "
                + "Möglicherweise waren die WinGet-Quelle oder die "
                + "Internetverbindung nicht erreichbar.";

            return information;
        }

        if (analysisResult.ExitCode != 0)
        {
            information.IsAnalysisSuccessful = false;
            information.AnalysisDetails =
                BuildFailureDetails(
                    "WinGet konnte die verfügbaren Programmaktualisierungen "
                    + "aus der öffentlichen WinGet-Quelle nicht zuverlässig "
                    + "ermitteln.",
                    analysisResult);

            return information;
        }

        var parseResult =
            ParseAvailableUpdates(
                analysisResult.StandardOutput);

        if (!parseResult.IsRecognized)
        {
            information.IsAnalysisSuccessful = false;
            information.AnalysisDetails =
                "WinGet wurde erfolgreich ausgeführt, das zurückgegebene "
                + "Ausgabeformat konnte jedoch nicht zuverlässig ausgewertet "
                + "werden. Deshalb wird nicht behauptet, dass keine "
                + "Programmaktualisierungen verfügbar sind. "
                + "Technische Ausgabe: "
                + LimitTechnicalText(
                    CleanOutput(
                        analysisResult.StandardOutput));

            return information;
        }

        if (parseResult.ReportedUpdateCount.HasValue
            && parseResult.ReportedUpdateCount.Value
            != parseResult.Updates.Count)
        {
            information.IsAnalysisSuccessful = false;
            information.AnalysisDetails =
                "WinGet meldet "
                + parseResult.ReportedUpdateCount.Value
                + " verfügbare Programmaktualisierungen, es konnten jedoch "
                + "nur "
                + parseResult.Updates.Count
                + " Tabelleneinträge zuverlässig ausgewertet werden. "
                + "Das Ergebnis wird deshalb nicht als vollständig bewertet.";

            return information;
        }

        information.IsAnalysisSuccessful = true;
        information.AvailableUpdateCount =
            parseResult.Updates.Count;

        information.IsResultTruncated =
            parseResult.Updates.Count
            > MaximumStoredUpdates;

        information.AvailableUpdates =
            parseResult.Updates
                .Take(MaximumStoredUpdates)
                .ToList();

        information.AnalysisDetails =
            BuildSuccessDetails(
                information.AvailableUpdateCount,
                information.IsResultTruncated);

        return information;
    }

    private static WingetExecutionResult RunWinget(
        IReadOnlyCollection<string> arguments,
        int timeoutMilliseconds)
    {
        try
        {
            var startInfo =
                new ProcessStartInfo
                {
                    FileName = "winget.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process =
                new Process
                {
                    StartInfo = startInfo
                };

            if (!process.Start())
            {
                return new WingetExecutionResult
                {
                    ErrorMessage =
                        "Der WinGet-Prozess konnte nicht gestartet werden."
                };
            }

            var standardOutputTask =
                process.StandardOutput.ReadToEndAsync();

            var standardErrorTask =
                process.StandardError.ReadToEndAsync();

            var completed =
                process.WaitForExit(
                    timeoutMilliseconds);

            if (!completed)
            {
                TryTerminateProcess(process);

                return new WingetExecutionResult
                {
                    WasStarted = true,
                    TimedOut = true
                };
            }

            var standardOutput =
                standardOutputTask
                    .GetAwaiter()
                    .GetResult();

            var standardError =
                standardErrorTask
                    .GetAwaiter()
                    .GetResult();

            return new WingetExecutionResult
            {
                WasStarted = true,
                ExitCode = process.ExitCode,
                StandardOutput = standardOutput,
                StandardError = standardError
            };
        }
        catch (Win32Exception exception)
        {
            return new WingetExecutionResult
            {
                ErrorMessage = exception.Message
            };
        }
        catch (Exception exception)
        {
            return new WingetExecutionResult
            {
                ErrorMessage = exception.Message
            };
        }
    }

    private static ProgramUpdateParseResult ParseAvailableUpdates(
        string output)
    {
        var cleanedOutput =
            CleanOutput(output);

        var lines =
            cleanedOutput
                .Split(
                    [
                        "\r\n",
                        "\n"
                    ],
                    StringSplitOptions.None)
                .Select(line => line.TrimEnd())
                .ToList();

        var separatorLineIndex =
            FindTableSeparatorLine(lines);

        if (separatorLineIndex < 1)
        {
            if (ContainsNoUpdatesMessage(
                cleanedOutput))
            {
                return new ProgramUpdateParseResult
                {
                    IsRecognized = true,
                    Updates = new List<AvailableProgramUpdate>(),
                    ReportedUpdateCount = 0
                };
            }

            return new ProgramUpdateParseResult
            {
                IsRecognized = false
            };
        }

        var headerLine =
            lines[separatorLineIndex - 1];

        var columnStarts =
            GetColumnStartsFromHeader(
                headerLine);

        if (columnStarts.Count < 4)
        {
            return new ProgramUpdateParseResult
            {
                IsRecognized = false
            };
        }

        var updates =
            new List<AvailableProgramUpdate>();

        for (var lineIndex = separatorLineIndex + 1;
             lineIndex < lines.Count;
             lineIndex++)
        {
            var line =
                lines[lineIndex];

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (IsProgressLine(line))
            {
                continue;
            }

            var values =
                ReadColumns(
                    line,
                    columnStarts);

            if (values.Count < 4)
            {
                continue;
            }

            var name =
                values[0];

            var packageId =
                values[1];

            var installedVersion =
                values[2];

            var availableVersion =
                values[3];

            if (string.IsNullOrWhiteSpace(name)
                || string.IsNullOrWhiteSpace(packageId)
                || string.IsNullOrWhiteSpace(installedVersion)
                || string.IsNullOrWhiteSpace(availableVersion))
            {
                continue;
            }

            updates.Add(
                new AvailableProgramUpdate
                {
                    Name = name,
                    PackageId = packageId,
                    InstalledVersion = installedVersion,
                    AvailableVersion = availableVersion,
                    Source = "winget"
                });
        }

        return new ProgramUpdateParseResult
        {
            IsRecognized = true,
            Updates = updates,
            ReportedUpdateCount =
                TryReadReportedUpdateCount(
                    cleanedOutput)
        };
    }

    private static int FindTableSeparatorLine(
        IReadOnlyList<string> lines)
    {
        for (var index = 1;
             index < lines.Count;
             index++)
        {
            var trimmedLine =
                lines[index].Trim();

            if (trimmedLine.Length < 10)
            {
                continue;
            }

            if (!trimmedLine.All(character =>
                    character == '-'))
            {
                continue;
            }

            var headerColumnCount =
                HeaderColumnRegex
                    .Matches(lines[index - 1])
                    .Count;

            if (headerColumnCount >= 4)
            {
                return index;
            }
        }

        return -1;
    }

    private static List<int> GetColumnStartsFromHeader(
        string headerLine)
    {
        return HeaderColumnRegex
            .Matches(headerLine)
            .Select(match => match.Index)
            .ToList();
    }

    private static List<string> ReadColumns(
        string line,
        IReadOnlyList<int> columnStarts)
    {
        var values =
            new List<string>();

        for (var columnIndex = 0;
             columnIndex < columnStarts.Count;
             columnIndex++)
        {
            var start =
                columnStarts[columnIndex];

            if (start >= line.Length)
            {
                values.Add(string.Empty);
                continue;
            }

            var end =
                columnIndex + 1 < columnStarts.Count
                    ? Math.Min(
                        columnStarts[columnIndex + 1],
                        line.Length)
                    : line.Length;

            var length =
                Math.Max(
                    0,
                    end - start);

            values.Add(
                line
                    .Substring(
                        start,
                        length)
                    .Trim());
        }

        return values;
    }

    private static int? TryReadReportedUpdateCount(
        string output)
    {
        var match =
            ReportedUpdateCountRegex.Match(
                output);

        if (!match.Success)
        {
            return null;
        }

        var countText =
            match.Groups["count"].Value;

        if (!int.TryParse(
                countText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var count))
        {
            return null;
        }

        return count;
    }

    private static bool ContainsNoUpdatesMessage(
        string output)
    {
        var normalized =
            output
                .ToLowerInvariant()
                .Trim();

        return normalized.Contains(
                   "keine anwendbare aktualisierung gefunden",
                   StringComparison.Ordinal)
               || normalized.Contains(
                   "keine aktualisierungen verfügbar",
                   StringComparison.Ordinal)
               || normalized.Contains(
                   "keine verfügbaren aktualisierungen gefunden",
                   StringComparison.Ordinal)
               || normalized.Contains(
                   "no applicable upgrade found",
                   StringComparison.Ordinal)
               || normalized.Contains(
                   "no upgrades available",
                   StringComparison.Ordinal)
               || normalized.Contains(
                   "no available upgrade found",
                   StringComparison.Ordinal);
    }

    private static bool IsProgressLine(
        string line)
    {
        var trimmedLine =
            line.Trim();

        if (trimmedLine.Length == 0)
        {
            return true;
        }

        return trimmedLine.All(character =>
            character is '-'
                or '\\'
                or '|'
                or '/'
                or '█'
                or '▒'
                or '░'
                or ' ');
    }

    private static string BuildUnavailableDetails(
        WingetExecutionResult result)
    {
        var details =
            "WinGet ist für den aktuell angemeldeten Windows-Benutzer "
            + "nicht verfügbar. Möglicherweise ist der Windows-Paketmanager "
            + "nicht installiert oder noch nicht für dieses Benutzerkonto "
            + "registriert.";

        if (!string.IsNullOrWhiteSpace(
            result.ErrorMessage))
        {
            details +=
                " Technische Details: "
                + LimitTechnicalText(
                    result.ErrorMessage);
        }

        return details;
    }

    private static string BuildFailureDetails(
        string summary,
        WingetExecutionResult result)
    {
        var technicalDetails =
            !string.IsNullOrWhiteSpace(
                result.StandardError)
                ? result.StandardError
                : !string.IsNullOrWhiteSpace(
                    result.StandardOutput)
                    ? result.StandardOutput
                    : result.ErrorMessage;

        if (string.IsNullOrWhiteSpace(
            technicalDetails))
        {
            return summary;
        }

        return summary
               + " Technische Details: "
               + LimitTechnicalText(
                   CleanOutput(technicalDetails));
    }

    private static string BuildSuccessDetails(
        int availableUpdateCount,
        bool isResultTruncated)
    {
        if (availableUpdateCount == 0)
        {
            return "WinGet hat in der öffentlichen WinGet-Quelle "
                   + "keine verfügbaren Aktualisierungen für erkannte "
                   + "Programme gemeldet.";
        }

        var details =
            availableUpdateCount == 1
                ? "WinGet hat in der öffentlichen WinGet-Quelle eine "
                  + "verfügbare Programmaktualisierung erkannt."
                : $"WinGet hat in der öffentlichen WinGet-Quelle "
                  + $"{availableUpdateCount} verfügbare "
                  + "Programmaktualisierungen erkannt.";

        if (isResultTruncated)
        {
            details +=
                $" Aus Speicher- und Darstellungsgründen werden nur die ersten "
                + $"{MaximumStoredUpdates} Einträge dauerhaft gespeichert.";
        }

        return details;
    }

    private static string CleanOutput(
        string output)
    {
        var withoutNullCharacters =
            output.Replace(
                "\0",
                string.Empty,
                StringComparison.Ordinal);

        return AnsiEscapeSequenceRegex.Replace(
            withoutNullCharacters,
            string.Empty);
    }

    private static string LimitTechnicalText(
        string text)
    {
        const int maximumLength = 1000;

        var normalized =
            text.Trim();

        if (normalized.Length <= maximumLength)
        {
            return normalized;
        }

        return normalized[..maximumLength]
               + " …";
    }

    private static void TryTerminateProcess(
        Process process)
    {
        try
        {
            process.Kill(
                entireProcessTree: true);

            process.WaitForExit();
        }
        catch
        {
            // Ein fehlgeschlagener Abbruch darf die
            // eigentliche Fehlerbehandlung nicht ersetzen.
        }
    }

    private sealed class WingetExecutionResult
    {
        public bool WasStarted { get; init; }

        public bool TimedOut { get; init; }

        public int? ExitCode { get; init; }

        public string StandardOutput { get; init; } =
            string.Empty;

        public string StandardError { get; init; } =
            string.Empty;

        public string ErrorMessage { get; init; } =
            string.Empty;
    }

    private sealed class ProgramUpdateParseResult
    {
        public bool IsRecognized { get; init; }

        public List<AvailableProgramUpdate> Updates { get; init; } =
            new();

        public int? ReportedUpdateCount { get; init; }
    }
}
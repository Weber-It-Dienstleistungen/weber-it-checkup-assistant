
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

    private const string WingetSourceName = "winget";

    private const uint WingetNoApplicableUpdateCode =
        0x8A15002B;

    private static readonly Regex AnsiEscapeSequenceRegex =
        new(
            @"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])",
            RegexOptions.Compiled);

    // WinGet richtet die Tabellenspalten normalerweise
    // durch mindestens zwei Leerzeichen aus.
    //
    // Anders als \S+ behandelt dieser Ausdruck
    // auch mehrteilige Spaltennamen als eine Spalte.
    private static readonly Regex HeaderColumnRegex =
        new(
            @"\S(?:.*?\S)?(?=\s{2,}|$)",
            RegexOptions.Compiled);

    // SCHRITT 40:
    // Unterstützt sowohl:
    //
    // 1 Aktualisierung verfügbar.
    // 17 Aktualisierungen verfügbar.
    // 1 upgrade available.
    // 17 upgrades available.
    // 15 packages have upgrades available.
    //
    // Die bisherige Schreibweise
    // "Aktualisierungen?" erkannte die deutsche
    // Einzahl "Aktualisierung" nicht korrekt.
    private static readonly Regex ReportedUpdateCountRegex =
        new(
            @"^\s*(?<count>\d+)\s+"
            + @"(?:(?:Aktualisierung(?:en)?|Upgrades?|Updates?)"
            + @"\s+(?:verfügbar|available)"
            + @"|packages?\s+have\s+"
            + @"(?:upgrades?|updates?)\s+available)\b",
            RegexOptions.Compiled
            | RegexOptions.IgnoreCase
            | RegexOptions.Multiline
            | RegexOptions.CultureInvariant);

    public ProgramUpdateInformation GetProgramUpdateInformation()
    {
        var information = new ProgramUpdateInformation();

        // -------------------------------------------------
        // 1. WinGet-Verfügbarkeit prüfen
        // -------------------------------------------------

        var versionResult =
            RunWinget(
                ["--version"],
                VersionTimeoutMilliseconds);

        if (!versionResult.WasStarted)
        {
            information.IsWingetAvailable = false;

            information.AnalysisDetails =
                BuildUnavailableDetails(versionResult);

            return information;
        }

        if (versionResult.TimedOut)
        {
            information.IsWingetAvailable = null;

            information.AnalysisDetails =
                "Die Verfügbarkeit von WinGet konnte nicht "
                + "rechtzeitig ermittelt werden. Der "
                + "Versionsaufruf hat das Zeitlimit "
                + "überschritten.";

            return information;
        }

        if (versionResult.ExitCode != 0)
        {
            information.IsWingetAvailable = null;

            information.AnalysisDetails =
                BuildFailureDetails(
                    "WinGet wurde gefunden, der Versionsaufruf "
                    + "wurde jedoch mit einem Fehler beendet.",
                    versionResult);

            return information;
        }

        information.IsWingetAvailable = true;

        information.WingetVersion =
            CleanOutput(versionResult.StandardOutput)
                .Trim();

        // -------------------------------------------------
        // 2. Update-Analyse ausführen
        // -------------------------------------------------

        information.IsAnalysisPerformed = true;
        information.AnalysisDate = DateTime.Now;

        var analysisResult =
            RunWinget(
                [
                    "upgrade",
                    "--source",
                    WingetSourceName,
                    "--disable-interactivity",
                    "--accept-source-agreements"
                ],
                AnalysisTimeoutMilliseconds);

        if (!analysisResult.WasStarted)
        {
            information.IsAnalysisSuccessful = false;

            information.AnalysisDetails =
                BuildFailureDetails(
                    "Die WinGet-Programupdate-Analyse "
                    + "konnte nicht gestartet werden.",
                    analysisResult);

            return information;
        }

        if (analysisResult.TimedOut)
        {
            information.IsAnalysisSuccessful = false;

            information.AnalysisDetails =
                "Die WinGet-Programupdate-Analyse wurde "
                + "nach zwei Minuten beendet, weil sie "
                + "nicht rechtzeitig abgeschlossen wurde. "
                + "Möglicherweise waren die WinGet-Quelle "
                + "oder die Internetverbindung nicht erreichbar.";

            return information;
        }

        // Ein von null abweichender Rückgabecode
        // bleibt zunächst ein nicht erfolgreich
        // bestätigter Analysevorgang.
        //
        // Auch 0x8A15002B wird nicht pauschal
        // als "keine Updates verfügbar" gewertet.
        if (analysisResult.ExitCode != 0)
        {
            information.IsAnalysisSuccessful = false;

            information.AnalysisDetails =
                BuildFailureDetails(
                    "WinGet konnte die verfügbaren "
                    + "Programmaktualisierungen aus der "
                    + "öffentlichen WinGet-Quelle nicht "
                    + "zuverlässig ermitteln.",
                    analysisResult);

            return information;
        }

        // -------------------------------------------------
        // 3. Ausgabe vollständig und sicher auswerten
        // -------------------------------------------------

        var parseResult =
            ParseAvailableUpdates(
                analysisResult.StandardOutput);

        if (!parseResult.IsRecognized)
        {
            information.IsAnalysisSuccessful = false;

            var reason =
                string.IsNullOrWhiteSpace(
                    parseResult.FailureReason)
                    ? "Das Ausgabeformat konnte nicht "
                      + "zuverlässig ausgewertet werden."
                    : parseResult.FailureReason;

            information.AnalysisDetails =
                "WinGet wurde ausgeführt, aber die "
                + "Programmliste konnte nicht vollständig "
                + "überprüft werden. "
                + reason
                + " Deshalb wird keine Aussage über "
                + "die Anzahl verfügbarer Updates getroffen. "
                + BuildFailureDetails(
                    "Technische Prozessdaten:",
                    analysisResult);

            return information;
        }

        // Erfolgreiche Tabellen müssen eine passende
        // WinGet-Gesamtzahl besitzen.
        if (parseResult.ReportedUpdateCount.HasValue
            && parseResult.ReportedUpdateCount.Value
                != parseResult.Updates.Count)
        {
            information.IsAnalysisSuccessful = false;

            information.AnalysisDetails =
                "Die von WinGet gemeldete Gesamtzahl "
                + "stimmt nicht mit den vollständig "
                + "ausgewerteten Tabelleneinträgen überein. "
                + BuildFailureDetails(
                    "Technische Prozessdaten:",
                    analysisResult);

            return information;
        }

        // -------------------------------------------------
        // 4. Nur bestätigte Ergebnisse übernehmen
        // -------------------------------------------------

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

    // =====================================================
    // WINGET-PROZESSAUSFÜHRUNG
    // =====================================================

    private static WingetExecutionResult RunWinget(
        IReadOnlyCollection<string> arguments,
        int timeoutMilliseconds)
    {
        try
        {
            var startInfo = new ProcessStartInfo
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

            using var process = new Process
            {
                StartInfo = startInfo
            };

            if (!process.Start())
            {
                return new WingetExecutionResult
                {
                    ErrorMessage =
                        "Der WinGet-Prozess konnte nicht "
                        + "gestartet werden."
                };
            }

            // Beide Streams parallel lesen.
            // Dadurch vermeiden wir einen möglichen
            // Deadlock bei umfangreicher Konsolenausgabe.
            var standardOutputTask =
                process.StandardOutput.ReadToEndAsync();

            var standardErrorTask =
                process.StandardError.ReadToEndAsync();

            var completed =
                process.WaitForExit(timeoutMilliseconds);

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

    // =====================================================
    // ROBUSTE TABELLENAUSWERTUNG
    // =====================================================

    private static ProgramUpdateParseResult ParseAvailableUpdates(
        string output)
    {
        var cleanedOutput = CleanOutput(output);

        var lines =
            cleanedOutput
                .Split(
                    '\n',
                    StringSplitOptions.None)
                .Select(line => line.TrimEnd())
                .ToList();

        var separatorLineIndex =
            FindTableSeparatorLine(
                lines,
                out var tableLayout);

        // -------------------------------------------------
        // Keine Tabelle vorhanden
        // -------------------------------------------------

        if (separatorLineIndex < 1
            || tableLayout is null)
        {
            // Eine eindeutige "keine Updates"-Meldung
            // darf als erfolgreiches Nullergebnis gelten.
            //
            // Fehlt diese Meldung, sind leere oder
            // unbekannte Ausgaben nicht zuverlässig.
            if (ContainsNoUpdatesMessage(cleanedOutput))
            {
                var reportedCount =
                    TryReadReportedUpdateCount(
                        cleanedOutput);

                if (reportedCount.HasValue
                    && reportedCount.Value != 0)
                {
                    return ParseFailure(
                        "Die WinGet-Ausgabe enthält "
                        + "widersprüchliche Angaben "
                        + "zur Anzahl der Updates.");
                }

                return new ProgramUpdateParseResult
                {
                    IsRecognized = true,

                    Updates =
                        new List<AvailableProgramUpdate>(),

                    ReportedUpdateCount = 0
                };
            }

            return ParseFailure(
                "Es wurde weder eine eindeutig "
                + "erkennbare Update-Tabelle noch "
                + "eine bestätigte Meldung über "
                + "fehlende Updates gefunden.");
        }

        // -------------------------------------------------
        // Eindeutige Tabellenstruktur gefunden
        // -------------------------------------------------

        var updates = new List<AvailableProgramUpdate>();

        var packageIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        int? reportedUpdateCount = null;

        var summaryFound = false;

        for (var lineIndex = separatorLineIndex + 1;
             lineIndex < lines.Count;
             lineIndex++)
        {
            var line = lines[lineIndex];

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (IsProgressLine(line))
            {
                continue;
            }

            // Die WinGet-Zusammenfassung beendet
            // die Haupttabelle.
            //
            // Zusätzliche Paketgruppen nach dieser
            // Zusammenfassung gehören nicht mehr
            // zur normalen Updateliste.
            if (TryReadReportedUpdateCount(
                    line,
                    out var count))
            {
                reportedUpdateCount = count;
                summaryFound = true;
                break;
            }

            // Anders als früher wird KEINE
            // nicht erkennbare Tabellenzeile
            // stillschweigend übersprungen.
            if (!TryCreateUpdateFromTableRow(
                    line,
                    tableLayout,
                    out var update))
            {
                return ParseFailure(
                    "Mindestens eine Tabellenzeile "
                    + "konnte nicht vollständig oder "
                    + "eindeutig ausgewertet werden: "
                    + LimitTechnicalText(line, 250));
            }

            // Ein Paket darf nicht mehrfach
            // unter derselben Kennung auftreten.
            //
            // Andernfalls wäre die spätere
            // Update-Auswahl möglicherweise
            // mehrdeutig.
            if (!packageIds.Add(update.PackageId))
            {
                return ParseFailure(
                    "Die Paketkennung "
                    + update.PackageId
                    + " kommt in der WinGet-Tabelle "
                    + "mehrfach vor.");
            }

            updates.Add(update);
        }

        // -------------------------------------------------
        // Vollständigkeit nachweisen
        // -------------------------------------------------

        if (!summaryFound
            || !reportedUpdateCount.HasValue)
        {
            return ParseFailure(
                "Die Update-Tabelle enthält keine "
                + "eindeutig erkennbare WinGet-"
                + "Zusammenfassung mit Gesamtzahl. "
                + "Eine vollständige Auswertung "
                + "kann deshalb nicht bestätigt werden.");
        }

        if (reportedUpdateCount.Value != updates.Count)
        {
            return ParseFailure(
                "WinGet meldet "
                + reportedUpdateCount.Value
                + " Aktualisierungen, es konnten "
                + "jedoch nur "
                + updates.Count
                + " vollständige Tabelleneinträge "
                + "ausgewertet werden.");
        }

        return new ProgramUpdateParseResult
        {
            IsRecognized = true,
            Updates = updates,
            ReportedUpdateCount = reportedUpdateCount
        };
    }

    // =====================================================
    // SPALTENSTRUKTUR
    // =====================================================

    private static int FindTableSeparatorLine(
        IReadOnlyList<string> lines,
        out WingetTableLayout? tableLayout)
    {
        tableLayout = null;

        for (var index = 1;
             index < lines.Count;
             index++)
        {
            var trimmedLine = lines[index].Trim();

            if (trimmedLine.Length < 10)
            {
                continue;
            }

            // Eine Tabellen-Trennlinie besteht
            // ausschließlich aus Bindestrichen.
            if (!trimmedLine.All(
                    character => character == '-'))
            {
                continue;
            }

            // Wir akzeptieren die Trennlinie nur,
            // wenn die darüberstehende Kopfzeile
            // eine eindeutig bekannte Struktur hat.
            if (!TryCreateTableLayout(
                    lines[index - 1],
                    out var detectedLayout))
            {
                continue;
            }

            tableLayout = detectedLayout;
            return index;
        }

        return -1;
    }

    private static bool TryCreateTableLayout(
        string headerLine,
        out WingetTableLayout? tableLayout)
    {
        tableLayout = null;

        var headerMatches =
            HeaderColumnRegex
                .Matches(headerLine)
                .Cast<Match>()
                .ToList();

        // Unterstützt werden vier Spalten ohne
        // Quellenangabe oder fünf mit Quelle.
        //
        // Unbekannte Zusatzspalten führen bewusst
        // nicht zu geratenen Zuordnungen.
        if (headerMatches.Count is not (4 or 5))
        {
            return false;
        }

        var names =
            headerMatches
                .Select(match => match.Value.Trim())
                .ToList();

        if (!IsColumnName(
                names[0],
                "Name",
                "Paketname")
            || !IsColumnName(
                names[1],
                "Id",
                "ID",
                "Paket-ID")
            || !IsColumnName(
                names[2],
                "Version",
                "Installed",
                "Installiert")
            || !IsColumnName(
                names[3],
                "Available",
                "Available Version",
                "Verfügbar",
                "Verfügbare Version"))
        {
            return false;
        }

        var sourceColumnIndex = -1;

        if (names.Count == 5)
        {
            if (!IsColumnName(
                    names[4],
                    "Source",
                    "Quelle"))
            {
                return false;
            }

            sourceColumnIndex = 4;
        }

        tableLayout = new WingetTableLayout(
            headerMatches
                .Select(match => match.Index)
                .ToList(),
            sourceColumnIndex);

        return true;
    }

    private static bool IsColumnName(
        string name,
        params string[] acceptedNames)
    {
        return acceptedNames.Any(
            acceptedName =>
                string.Equals(
                    name,
                    acceptedName,
                    StringComparison.OrdinalIgnoreCase));
    }

    // =====================================================
    // TABELLENZEILEN
    // =====================================================

    private static bool TryCreateUpdateFromTableRow(
        string line,
        WingetTableLayout layout,
        out AvailableProgramUpdate update)
    {
        update = new AvailableProgramUpdate();

        var values =
            ReadColumns(
                line,
                layout.ColumnStarts);

        if (values.Count < 4)
        {
            return false;
        }

        var name = values[0];

        var packageId = values[1];

        var installedVersion = values[2];

        var availableVersion = values[3];

        if (string.IsNullOrWhiteSpace(name)
            || string.IsNullOrWhiteSpace(packageId)
            || string.IsNullOrWhiteSpace(installedVersion)
            || string.IsNullOrWhiteSpace(availableVersion))
        {
            return false;
        }

        if (!IsPlausiblePackageIdentifier(packageId))
        {
            return false;
        }

        // Abgeschnittene Versionsnummern können
        // keine zuverlässige Updateentscheidung
        // ermöglichen.
        if (HasTruncationMarker(installedVersion)
            || HasTruncationMarker(availableVersion))
        {
            return false;
        }

        // Die Versionsspalten dürfen keine
        // zusätzlichen, durch falsche Spalten-
        // positionen übernommenen Leerzeichen
        // enthalten.
        if (installedVersion.Any(char.IsWhiteSpace)
            || availableVersion.Any(char.IsWhiteSpace))
        {
            return false;
        }

        if (layout.SourceColumnIndex >= 0)
        {
            if (layout.SourceColumnIndex >= values.Count)
            {
                return false;
            }

            var source =
                values[layout.SourceColumnIndex];

            // Die Analyse ist auf die öffentliche
            // WinGet-Quelle eingeschränkt.
            //
            // Eine andere oder fehlende Quelle wird
            // nicht stillschweigend übernommen.
            if (!string.Equals(
                    source,
                    WingetSourceName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        update = new AvailableProgramUpdate
        {
            Name = name,
            PackageId = packageId,
            InstalledVersion = installedVersion,
            AvailableVersion = availableVersion,
            Source = WingetSourceName
        };

        return true;
    }

    private static bool IsPlausiblePackageIdentifier(
        string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return false;
        }

        if (!string.Equals(
                packageId,
                packageId.Trim(),
                StringComparison.Ordinal))
        {
            return false;
        }

        if (packageId.Any(
                character =>
                    char.IsWhiteSpace(character)
                    || char.IsControl(character)))
        {
            return false;
        }

        // Ein durch die Konsolenbreite abgeschnittener
        // Paketname ist unschön, aber die Paketkennung
        // muss zwingend vollständig sein.
        if (HasTruncationMarker(packageId))
        {
            return false;
        }

        return true;
    }

    private static bool HasTruncationMarker(
        string value)
    {
        return value.Contains(
                   '…')
               || value.EndsWith(
                    "...",
                    StringComparison.Ordinal);
    }

    private static List<string> ReadColumns(
        string line,
        IReadOnlyList<int> columnStarts)
    {
        var values = new List<string>();

        for (var columnIndex = 0;
             columnIndex < columnStarts.Count;
             columnIndex++)
        {
            var start = columnStarts[columnIndex];

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
                Math.Max(0, end - start);

            values.Add(
                line.Substring(
                        start,
                        length)
                    .Trim());
        }

        return values;
    }

    // =====================================================
    // ZUSAMMENFASSUNGEN UND NULLERGEBNISSE
    // =====================================================

    private static bool TryReadReportedUpdateCount(
        string line,
        out int count)
    {
        count = 0;

        var match =
            ReportedUpdateCountRegex.Match(line);

        if (!match.Success)
        {
            return false;
        }

        return int.TryParse(
            match.Groups["count"].Value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out count);
    }

    private static int? TryReadReportedUpdateCount(
        string output)
    {
        var match =
            ReportedUpdateCountRegex.Match(output);

        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(
            match.Groups["count"].Value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var count)
            ? count
            : null;
    }

    private static bool ContainsNoUpdatesMessage(
        string output)
    {
        var normalized =
            output.ToLowerInvariant().Trim();

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
                   "keine aktualisierung verfügbar",
                   StringComparison.Ordinal)
               || normalized.Contains(
                   "no applicable upgrade found",
                   StringComparison.Ordinal)
               || normalized.Contains(
                   "no upgrades available",
                   StringComparison.Ordinal)
               || normalized.Contains(
                   "no available upgrade found",
                   StringComparison.Ordinal)
               || normalized.Contains(
                   "no available upgrades",
                   StringComparison.Ordinal);
    }

    private static bool IsProgressLine(string line)
    {
        var trimmedLine = line.Trim();

        if (trimmedLine.Length == 0)
        {
            return true;
        }

        return trimmedLine.All(
            character =>
                character is '-'
                    or '\\'
                    or '|'
                    or '/'
                    or '█'
                    or '▒'
                    or '░'
                    or ' ');
    }

    private static ProgramUpdateParseResult ParseFailure(
        string reason)
    {
        return new ProgramUpdateParseResult
        {
            IsRecognized = false,
            FailureReason = reason
        };
    }

    // =====================================================
    // FEHLER- UND ERFOLGSMELDUNGEN
    // =====================================================

    private static string BuildUnavailableDetails(
        WingetExecutionResult result)
    {
        var details =
            "WinGet ist für den aktuell angemeldeten "
            + "Windows-Benutzer nicht verfügbar. "
            + "Möglicherweise ist der Windows-Paketmanager "
            + "nicht installiert oder noch nicht für "
            + "dieses Benutzerkonto registriert.";

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

    /// <summary>
    /// Erstellt eine nachvollziehbare Diagnose für
    /// fehlgeschlagene WinGet-Aufrufe.
    ///
    /// Rückgabecode, Standardfehler und Standardausgabe
    /// werden getrennt dokumentiert. Beide Ausgaben
    /// werden aus Platzgründen begrenzt.
    ///
    /// Ein Fehler wird dadurch nicht zu einem
    /// erfolgreichen Analyseergebnis umgedeutet.
    /// </summary>
    private static string BuildFailureDetails(
        string summary,
        WingetExecutionResult result)
    {
        var details = new List<string>();

        if (!string.IsNullOrWhiteSpace(summary))
        {
            details.Add(summary.Trim());
        }

        if (result.ExitCode.HasValue)
        {
            var exitCode = result.ExitCode.Value;

            var unsignedExitCode =
                unchecked((uint)exitCode);

            details.Add(
                "WinGet-Rückgabecode: "
                + exitCode.ToString(
                    CultureInfo.InvariantCulture)
                + " (0x"
                + unsignedExitCode.ToString(
                    "X8",
                    CultureInfo.InvariantCulture)
                + ").");

            if (unsignedExitCode ==
                WingetNoApplicableUpdateCode)
            {
                details.Add(
                    "WinGet meldete den Rückgabecode "
                    + "für eine nicht anwendbare "
                    + "Aktualisierung. Daraus allein "
                    + "lässt sich kein vollständig "
                    + "erfolgreicher Update-Scan "
                    + "mit null verfügbaren "
                    + "Aktualisierungen ableiten.");
            }
        }

        if (!string.IsNullOrWhiteSpace(
                result.ErrorMessage))
        {
            details.Add(
                "Prozessfehler: "
                + LimitTechnicalText(
                    result.ErrorMessage));
        }

        if (!string.IsNullOrWhiteSpace(
                result.StandardError))
        {
            details.Add(
                "WinGet-Fehlerausgabe: "
                + LimitTechnicalText(
                    CleanOutput(
                        result.StandardError)));
        }

        if (!string.IsNullOrWhiteSpace(
                result.StandardOutput))
        {
            details.Add(
                "WinGet-Standardausgabe: "
                + LimitTechnicalText(
                    CleanOutput(
                        result.StandardOutput)));
        }

        return string.Join(" ", details);
    }

    private static string BuildSuccessDetails(
        int availableUpdateCount,
        bool isResultTruncated)
    {
        if (availableUpdateCount == 0)
        {
            return
                "WinGet hat in der öffentlichen "
                + "WinGet-Quelle keine verfügbaren "
                + "Aktualisierungen für erkannte "
                + "Programme gemeldet.";
        }

        var details =
            availableUpdateCount == 1
                ? "WinGet hat in der öffentlichen "
                  + "WinGet-Quelle eine verfügbare "
                  + "Programmaktualisierung erkannt."
                : "WinGet hat in der öffentlichen "
                  + "WinGet-Quelle "
                  + availableUpdateCount
                  + " verfügbare Programmaktualisierungen "
                  + "erkannt.";

        if (isResultTruncated)
        {
            details +=
                " Aus Speicher- und Darstellungsgründen "
                + "werden nur die ersten "
                + MaximumStoredUpdates
                + " Einträge dauerhaft gespeichert.";
        }

        return details;
    }

    // =====================================================
    // TEXTBEREINIGUNG
    // =====================================================

    private static string CleanOutput(string output)
    {
        var withoutNullCharacters =
            (output ?? string.Empty)
                .Replace(
                    "\0",
                    string.Empty,
                    StringComparison.Ordinal);

        var withoutAnsiSequences =
            AnsiEscapeSequenceRegex.Replace(
                withoutNullCharacters,
                string.Empty);

        // WinGet verwendet teilweise Wagenrückläufe
        // für Fortschrittsanzeigen.
        //
        // Wir behandeln sie als eigene Zeilen,
        // damit sie nicht unbemerkt mit Paketdaten
        // verschmelzen.
        return withoutAnsiSequences
            .Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Replace(
                '\r',
                '\n')
            .Replace(
                "\b",
                string.Empty,
                StringComparison.Ordinal);
    }

    private static string LimitTechnicalText(
        string text,
        int maximumLength = 1000)
    {
        var normalized =
            (text ?? string.Empty).Trim();

        if (normalized.Length <= maximumLength)
        {
            return normalized;
        }

        return normalized[..maximumLength] + " …";
    }

    private static void TryTerminateProcess(
        Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }
        catch
        {
            // Ein fehlgeschlagener Prozessabbruch
            // darf die Fehlerbehandlung nicht ersetzen.
        }
    }

    // =====================================================
    // INTERNE ERGEBNISMODELLE
    // =====================================================

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

    private sealed class WingetTableLayout
    {
        public WingetTableLayout(
            IReadOnlyList<int> columnStarts,
            int sourceColumnIndex)
        {
            ColumnStarts = columnStarts;
            SourceColumnIndex = sourceColumnIndex;
        }

        public IReadOnlyList<int> ColumnStarts { get; }

        public int SourceColumnIndex { get; }
    }

    private sealed class ProgramUpdateParseResult
    {
        public bool IsRecognized { get; init; }

        public List<AvailableProgramUpdate> Updates
        {
            get;
            init;
        } = new();

        public int? ReportedUpdateCount { get; init; }

        public string FailureReason { get; init; } =
            string.Empty;
    }
}

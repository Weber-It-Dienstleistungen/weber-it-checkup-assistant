
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Updates;

/// <summary>
/// Ermittelt den aktuellen Bearbeitungsstatus
/// einzelner WinGet-Pakete.
///
/// Grundlage sind:
///
/// - Die zuletzt vorliegenden WinGet-Scanergebnisse
/// - Die gespeicherten technischen Einzelbefehle
/// - Der Zeitpunkt der WinGet-Analyse
///
/// Ein erfolgreicher WinGet-Aufruf bedeutet
/// zunächst nur:
///
/// "Technisch ausgeführt, Kontrolle ausstehend".
///
/// Ein neues Scanergebnis hat Vorrang vor
/// älteren Aktionsprotokollen.
///
/// Der Dienst verändert weder die ursprünglichen
/// Scandaten noch die Aufgaben oder die Historie.
/// </summary>
public static class ProgramUpdatePackageWorkStatusService
{
    private const string ProgramUpdateTaskCode =
        "task.program-updates.available";

    private const string ProgramUpdateActionCode =
        "action.program-updates.selected-upgrades";

    private static readonly Regex CommandPattern =
        new(
            "^winget\\.exe\\s+upgrade\\s+--id\\s+"
            + "(?<id>\"[^\"]+\"|\\S+)\\s+--version\\s+"
            + "(?<version>\"[^\"]+\"|\\S+)\\s+--source\\s+"
            + "(?<source>\"[^\"]+\"|\\S+)\\s+--exact(?:\\s|$)",
            RegexOptions.Compiled
            | RegexOptions.IgnoreCase
            | RegexOptions.CultureInvariant);

    /// <summary>
    /// Erzeugt eine aktuelle Arbeitsansicht
    /// für die im Scan aufgeführten Pakete.
    ///
    /// Die zurückgegebenen Einträge sind
    /// unabhängig von der gespeicherten
    /// ursprünglichen Update-Liste.
    /// </summary>
    public static IReadOnlyList<ProgramUpdatePackageWorkStatus>
        Evaluate(
            ProgramUpdateInformation information,
            CheckupTaskList taskList)
    {
        ArgumentNullException.ThrowIfNull(information);
        ArgumentNullException.ThrowIfNull(taskList);

        var updates =
            information.AvailableUpdates
            ?? new List<AvailableProgramUpdate>();

        /*
         * Ohne belastbare Analyse dürfen
         * historische Aktionsergebnisse
         * keine automatische Ausblendung
         * von Updates auslösen.
         */
        var isAnalysisReliable =
            information.IsWingetAvailable == true
            && information.IsAnalysisPerformed
            && information.IsAnalysisSuccessful
            && !information.IsResultTruncated
            && information.AnalysisDate.HasValue
            && information.AvailableUpdateCount
                == updates.Count;

        if (!isAnalysisReliable)
        {
            return updates
                .Select(update =>
                    new ProgramUpdatePackageWorkStatus
                    {
                        Update = update,

                        State =
                            ProgramUpdatePackageWorkState
                                .NotAssessable,

                        Explanation =
                            "Der aktuelle WinGet-Scan "
                            + "ist nicht vollständig "
                            + "auswertbar. Eine automatische "
                            + "Auswahlentscheidung ist "
                            + "deshalb nicht zulässig."
                    })
                .ToList();
        }

        var analysisAt =
            new DateTimeOffset(
                information.AnalysisDate!.Value);

        var attempts =
            ReadDocumentedAttempts(
                taskList,
                analysisAt);

        var statuses =
            new List<ProgramUpdatePackageWorkStatus>();

        foreach (var update in updates)
        {
            if (update is null)
            {
                continue;
            }

            var matchingAttempts =
                attempts
                    .Where(attempt =>
                        MatchesUpdate(
                            attempt,
                            update))
                    .OrderBy(attempt =>
                        attempt.ActionAt)
                    .ThenBy(attempt =>
                        attempt.ActionOrder)
                    .ThenBy(attempt =>
                        attempt.CommandOrder)
                    .ToList();

            var latestAttempt =
                matchingAttempts.LastOrDefault();

            statuses.Add(
                CreateWorkStatus(
                    update,
                    latestAttempt));
        }

        return statuses;
    }

    private static ProgramUpdatePackageWorkStatus
        CreateWorkStatus(
            AvailableProgramUpdate update,
            ProgramUpdateCommandAttempt? attempt)
    {
        if (attempt is null)
        {
            return new ProgramUpdatePackageWorkStatus
            {
                Update = update,

                State =
                    ProgramUpdatePackageWorkState.Pending,

                Explanation =
                    "Für diese erkannte Aktualisierung "
                    + "ist seit dem aktuellen WinGet-Scan "
                    + "kein technischer Einzelversuch "
                    + "dokumentiert."
            };
        }

        if (!attempt.HasConclusiveTimestamp)
        {
            return new ProgramUpdatePackageWorkStatus
            {
                Update = update,

                State =
                    ProgramUpdatePackageWorkState
                        .NeedsReview,

                Explanation =
                    "Für diesen Updateversuch fehlt "
                    + "ein vollständiger, belastbarer "
                    + "Abschlusszeitpunkt."
            };
        }

        if (attempt.WasStarted == true
            && attempt.ExitCode == 0)
        {
            return new ProgramUpdatePackageWorkStatus
            {
                Update = update,

                State =
                    ProgramUpdatePackageWorkState
                        .AwaitingVerification,

                LastExitCode =
                    attempt.ExitCode,

                Explanation =
                    "WinGet hat diesen Einzelbefehl "
                    + "mit Exitcode 0 beendet. "
                    + "Das Update wird vorerst nicht "
                    + "erneut zur Installation angeboten. "
                    + "Eine spätere WinGet-Prüfung muss "
                    + "den Zielzustand bestätigen."
            };
        }

        if (attempt.WasStarted == true
            && attempt.ExitCode.HasValue)
        {
            return new ProgramUpdatePackageWorkStatus
            {
                Update = update,

                State =
                    ProgramUpdatePackageWorkState.Failed,

                LastExitCode =
                    attempt.ExitCode,

                Explanation =
                    "Der letzte dokumentierte "
                    + "Einzelversuch ist mit Fehlercode "
                    + attempt.ExitCode.Value.ToString(
                        CultureInfo.InvariantCulture)
                    + " fehlgeschlagen. "
                    + "Das Paket bleibt zur weiteren "
                    + "Bearbeitung verfügbar."
            };
        }

        if (attempt.WasStarted == false)
        {
            return new ProgramUpdatePackageWorkStatus
            {
                Update = update,

                State =
                    ProgramUpdatePackageWorkState
                        .NotStarted,

                Explanation =
                    "Der Einzelbefehl wurde vorbereitet, "
                    + "aber nicht gestartet. "
                    + "Das Update kann erneut "
                    + "ausgewählt werden."
            };
        }

        return new ProgramUpdatePackageWorkStatus
        {
            Update = update,

            State =
                ProgramUpdatePackageWorkState.NeedsReview,

            Explanation =
                "Für den letzten Updateversuch liegt "
                + "kein eindeutiges technisches Ergebnis "
                + "vor. Vor einer Wiederholung sollte "
                + "der aktuelle Installationszustand "
                + "kontrolliert werden."
        };
    }

    /// <summary>
    /// Liest nur zur Programmupdate-Aufgabe
    /// gehörende Aktionsprotokolle.
    ///
    /// Protokolle, deren Ausführung vor der
    /// aktuellen WinGet-Analyse lag, können
    /// aktuelle Updatebedarfe nicht erledigen.
    /// </summary>
    private static List<ProgramUpdateCommandAttempt>
        ReadDocumentedAttempts(
            CheckupTaskList taskList,
            DateTimeOffset analysisAt)
    {
        var attempts =
            new List<ProgramUpdateCommandAttempt>();

        var actionOrder = 0;

        foreach (var task in taskList.Tasks)
        {
            if (task is null
                || !string.Equals(
                    task.TaskCode,
                    ProgramUpdateTaskCode,
                    StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var action in task.ActionResults)
            {
                actionOrder++;

                if (action is null
                    || !string.Equals(
                        action.ActionCode,
                        ProgramUpdateActionCode,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var actionAt =
                    action.FinishedAt
                    ?? action.StartedAt;

                if (!actionAt.HasValue
                    || actionAt.Value <= analysisAt)
                {
                    continue;
                }

                /*
                 * Ein nach der Analyse beendeter
                 * Vorgang ist nicht zwangsläufig
                 * vollständig nach der Analyse
                 * gestartet worden.
                 */
                if (action.StartedAt.HasValue
                    && action.StartedAt.Value
                        <= analysisAt)
                {
                    continue;
                }

                var parsedAttempts =
                    ParseActionDetails(
                        action.Details,
                        actionAt.Value,
                        action.FinishedAt.HasValue,
                        actionOrder);

                attempts.AddRange(parsedAttempts);
            }
        }

        return attempts;
    }

    /// <summary>
    /// Das bestehende Protokollformat lautet
    /// für jeden WinGet-Einzelbefehl:
    ///
    /// winget.exe upgrade --id ... --version ...
    /// Gestartet: Ja/Nein
    /// Exitcode: ...
    ///
    /// Es werden nur solche ausdrücklich
    /// dokumentierten Befehle übernommen.
    ///
    /// Standardausgaben oder Fehlertexte
    /// werden nicht als Erfolgsnachweis
    /// interpretiert.
    /// </summary>
    private static List<ProgramUpdateCommandAttempt>
        ParseActionDetails(
            string? details,
            DateTimeOffset actionAt,
            bool hasConclusiveTimestamp,
            int actionOrder)
    {
        var attempts =
            new List<ProgramUpdateCommandAttempt>();

        if (string.IsNullOrWhiteSpace(details))
        {
            return attempts;
        }

        var lines =
            new List<string>();

        using (var reader =
               new StringReader(details))
        {
            while (reader.ReadLine() is { } line)
            {
                lines.Add(line.Trim());
            }
        }

        var commandOrder = 0;

        for (var index = 0;
             index < lines.Count;
             index++)
        {
            var match =
                CommandPattern.Match(lines[index]);

            if (!match.Success)
            {
                continue;
            }

            commandOrder++;

            var packageId =
                NormalizeCommandArgument(
                    match.Groups["id"].Value);

            var version =
                NormalizeCommandArgument(
                    match.Groups["version"].Value);

            var source =
                NormalizeCommandArgument(
                    match.Groups["source"].Value);

            if (string.IsNullOrWhiteSpace(packageId)
                || string.IsNullOrWhiteSpace(version)
                || string.IsNullOrWhiteSpace(source))
            {
                continue;
            }

            bool? wasStarted = null;
            int? exitCode = null;

            /*
             * Nur die beiden direkt auf die
             * Befehlszeile folgenden Felder
             * gehören zum technischen
             * Einzelergebnis.
             */
            if (index + 2 < lines.Count
                && lines[index + 1].StartsWith(
                    "Gestartet:",
                    StringComparison.OrdinalIgnoreCase)
                && lines[index + 2].StartsWith(
                    "Exitcode:",
                    StringComparison.OrdinalIgnoreCase))
            {
                var startedText =
                    lines[index + 1]
                        ["Gestartet:".Length..]
                        .Trim();

                wasStarted =
                    startedText.Equals(
                        "Ja",
                        StringComparison.OrdinalIgnoreCase)
                        ? true
                        : startedText.Equals(
                            "Nein",
                            StringComparison.OrdinalIgnoreCase)
                            ? false
                            : null;

                var exitCodeText =
                    lines[index + 2]
                        ["Exitcode:".Length..]
                        .Trim();

                if (int.TryParse(
                        exitCodeText,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var parsedExitCode))
                {
                    exitCode = parsedExitCode;
                }

                index += 2;
            }

            attempts.Add(
                new ProgramUpdateCommandAttempt
                {
                    PackageId = packageId,
                    AvailableVersion = version,
                    Source = source,

                    WasStarted = wasStarted,
                    ExitCode = exitCode,

                    ActionAt = actionAt,

                    HasConclusiveTimestamp =
                        hasConclusiveTimestamp,

                    ActionOrder = actionOrder,
                    CommandOrder = commandOrder
                });
        }

        return attempts;
    }

    private static bool MatchesUpdate(
        ProgramUpdateCommandAttempt attempt,
        AvailableProgramUpdate update)
    {
        /*
         * Entscheidend sind alle drei
         * Identitätsmerkmale.
         *
         * Ein erfolgreiches Update auf eine
         * alte Zielversion darf ein neues
         * Update nicht versehentlich ausblenden.
         */
        return string.Equals(
                   attempt.PackageId,
                   update.PackageId?.Trim(),
                   StringComparison.OrdinalIgnoreCase)
               && string.Equals(
                   attempt.Source,
                   update.Source?.Trim(),
                   StringComparison.OrdinalIgnoreCase)
               && string.Equals(
                   attempt.AvailableVersion,
                   update.AvailableVersion?.Trim(),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCommandArgument(
        string argument)
    {
        return argument
            .Trim()
            .Trim('"')
            .Trim();
    }

    private sealed class ProgramUpdateCommandAttempt
    {
        public string PackageId { get; init; } =
            string.Empty;

        public string Source { get; init; } =
            string.Empty;

        public string AvailableVersion { get; init; } =
            string.Empty;

        public bool? WasStarted { get; init; }

        public int? ExitCode { get; init; }

        public DateTimeOffset ActionAt { get; init; }

        public bool HasConclusiveTimestamp
        {
            get;
            init;
        }

        public int ActionOrder { get; init; }

        public int CommandOrder { get; init; }
    }
}

public enum ProgramUpdatePackageWorkState
{
    Pending = 0,

    AwaitingVerification = 1,

    Failed = 2,

    NotStarted = 3,

    NeedsReview = 4,

    NotAssessable = 5
}

/// <summary>
/// Darstellung des fachlichen Arbeitsstatus
/// eines einzelnen WinGet-Pakets.
///
/// Der Status wird nur berechnet und
/// nicht in die ursprüngliche Scanliste
/// zurückgeschrieben.
/// </summary>
public sealed class ProgramUpdatePackageWorkStatus
{
    public AvailableProgramUpdate Update { get; init; } =
        new();

    public ProgramUpdatePackageWorkState State { get; init; } =
        ProgramUpdatePackageWorkState.NotAssessable;

    public int? LastExitCode { get; init; }

    public string Explanation { get; init; } =
        string.Empty;

    /// <summary>
    /// Nur tatsächlich offene oder eindeutig
    /// fehlgeschlagene Updates werden
    /// automatisch erneut angeboten.
    /// </summary>
    public bool CanOfferInstallation =>
        State is
            ProgramUpdatePackageWorkState.Pending
            or ProgramUpdatePackageWorkState.Failed
            or ProgramUpdatePackageWorkState.NotStarted;

    /// <summary>
    /// Nach einem erfolgreich protokollierten
    /// Einzelbefehl wird das Paket zunächst
    /// aus der aktiven Installation entfernt.
    /// </summary>
    public bool IsAwaitingVerification =>
        State ==
        ProgramUpdatePackageWorkState.AwaitingVerification;

    public bool NeedsManualReview =>
        State is
            ProgramUpdatePackageWorkState.NeedsReview
            or ProgramUpdatePackageWorkState.NotAssessable;

    public string StatusText =>
        State switch
        {
            ProgramUpdatePackageWorkState.Pending =>
                "Update offen",

            ProgramUpdatePackageWorkState
                .AwaitingVerification =>
                    "Ausgeführt – Kontrolle ausstehend",

            ProgramUpdatePackageWorkState.Failed =>
                "Fehlgeschlagen – erneut prüfen",

            ProgramUpdatePackageWorkState.NotStarted =>
                "Noch nicht ausgeführt",

            ProgramUpdatePackageWorkState.NeedsReview =>
                "Ergebnis unklar – manuell prüfen",

            _ =>
                "WinGet-Analyse nicht belastbar"
        };
}

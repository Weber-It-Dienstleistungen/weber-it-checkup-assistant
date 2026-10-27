
#if DEBUG

using System.Globalization;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Updates;

/// <summary>
/// Isolierte Regressionstests für die
/// fachliche WinGet-Paketstatusbewertung.
///
/// Ausschließlich für Debug-Builds.
///
/// Alle verwendeten Daten sind künstlich.
/// Es erfolgen keine Datenbankzugriffe,
/// Dateizugriffe, Systemscans oder
/// tatsächlichen WinGet-Ausführungen.
///
/// Die Tests starten nicht automatisch.
/// Sie werden ausdrücklich über
/// RunAll() aufgerufen.
/// </summary>
public static class ProgramUpdatePackageWorkStatusRegressionTests
{
    private const string TaskCode =
        "task.program-updates.available";

    private const string ActionCode =
        "action.program-updates.selected-upgrades";

    private const string PackageId =
        "Example.TestApplication";

    private const string PackageSource =
        "winget";

    private const string TargetVersion =
        "2.0.0";

    private static readonly DateTime AnalysisDate =
        new(
            2026, 10, 27, 10, 0, 0,
            DateTimeKind.Local);

    private static readonly DateTimeOffset AnalysisAt =
        new(AnalysisDate);

    /// <summary>
    /// Führt sämtliche WinGet-Regressionstests aus.
    ///
    /// Jeder Test arbeitet mit unabhängig
    /// erzeugten künstlichen Objekten.
    ///
    /// Ein fehlgeschlagener Test löst eine
    /// Ausnahme mit eindeutiger Bezeichnung aus.
    /// </summary>
    public static string RunAll()
    {
        var results = new List<string>();

        Execute(
            "Update ohne Aktion bleibt offen",
            TestPendingWithoutAction,
            results);

        Execute(
            "Erfolgreicher Einzelbefehl wartet auf Kontrolle",
            TestSuccessfulAttempt,
            results);

        Execute(
            "Fehlgeschlagener Einzelbefehl bleibt auswählbar",
            TestFailedAttempt,
            results);

        Execute(
            "Nicht gestarteter Einzelbefehl bleibt auswählbar",
            TestNotStartedAttempt,
            results);

        Execute(
            "Unklares Einzelergebnis erfordert Prüfung",
            TestUnclearAttempt,
            results);

        Execute(
            "Fehlender Abschlusszeitpunkt erfordert Prüfung",
            TestMissingFinishTime,
            results);

        Execute(
            "Letzter fehlgeschlagener Versuch entscheidet",
            TestLatestFailureWins,
            results);

        Execute(
            "Letzter erfolgreicher Versuch entscheidet",
            TestLatestSuccessWins,
            results);

        Execute(
            "Andere Zielversion bleibt unabhängig",
            TestDifferentTargetVersion,
            results);

        Execute(
            "Andere Paketquelle bleibt unabhängig",
            TestDifferentSource,
            results);

        Execute(
            "Andere Paket-ID bleibt unabhängig",
            TestDifferentPackageId,
            results);

        Execute(
            "Ältere Aktion erledigt neuen Scan nicht",
            TestOldActionDoesNotHideUpdate,
            results);

        Execute(
            "Scan während einer Aktion schützt aktuellen Befund",
            TestActionStartedBeforeScan,
            results);

        Execute(
            "Unvollständiger Scan verhindert automatische Freigabe",
            TestUnreliableScan,
            results);

        Execute(
            "Mehrere Einzelbefehle werden getrennt bewertet",
            TestMultipleCommandsInOneAction,
            results);

        return
            $"Alle {results.Count} WinGet-Regressionstests "
            + "wurden erfolgreich abgeschlossen."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =========================================
    // GRUNDZUSTÄNDE
    // =========================================

    private static void TestPendingWithoutAction()
    {
        var status = Evaluate();

        Require(
            status.State ==
            ProgramUpdatePackageWorkState.Pending,
            "Ein Update ohne Aktion muss offen sein.");

        Require(
            status.CanOfferInstallation,
            "Das Paket muss auswählbar bleiben.");
    }

    private static void TestSuccessfulAttempt()
    {
        var status = Evaluate(
            CreateAttempt());

        Require(
            status.State ==
            ProgramUpdatePackageWorkState
                .AwaitingVerification,
            "Exitcode 0 darf nur als technisch "
            + "ausgeführt bewertet werden.");

        Require(
            !status.CanOfferInstallation,
            "Ein technisch erfolgreiches Paket "
            + "darf vor dem Nachscan nicht "
            + "erneut angeboten werden.");

        Require(
            status.IsAwaitingVerification,
            "Die Nachkontrolle muss ausstehen.");
    }

    private static void TestFailedAttempt()
    {
        var status = Evaluate(
            CreateAttempt(exitCode: 23));

        Require(
            status.State ==
            ProgramUpdatePackageWorkState.Failed,
            "Ein Fehlercode muss als "
            + "Fehlschlag gelten.");

        Require(
            status.CanOfferInstallation,
            "Ein fehlgeschlagenes Paket "
            + "muss erneut auswählbar sein.");

        Require(
            status.LastExitCode == 23,
            "Der technische Fehlercode "
            + "muss erhalten bleiben.");
    }

    private static void TestNotStartedAttempt()
    {
        var status = Evaluate(
            CreateAttempt(
                started: false,
                exitCode: null));

        Require(
            status.State ==
            ProgramUpdatePackageWorkState.NotStarted,
            "Ein nicht gestarteter Befehl "
            + "darf nicht als ausgeführt gelten.");

        Require(
            status.CanOfferInstallation,
            "Das Paket muss erneut "
            + "auswählbar bleiben.");
    }

    private static void TestUnclearAttempt()
    {
        var status = Evaluate(
            CreateAttempt(
                started: null,
                exitCode: null));

        Require(
            status.State ==
            ProgramUpdatePackageWorkState.NeedsReview,
            "Ein unbekanntes Ergebnis "
            + "muss überprüft werden.");

        Require(
            status.NeedsManualReview,
            "Die manuelle Kontrolle "
            + "muss angezeigt werden.");

        Require(
            !status.CanOfferInstallation,
            "Ein unbekanntes Ergebnis "
            + "darf nicht automatisch "
            + "erneut ausgeführt werden.");
    }

    private static void TestMissingFinishTime()
    {
        var status = Evaluate(
            CreateAttempt(
                includeFinish: false));

        Require(
            status.State ==
            ProgramUpdatePackageWorkState.NeedsReview,
            "Ohne Abschlusszeitpunkt "
            + "darf kein Erfolg bestätigt werden.");

        Require(
            status.NeedsManualReview,
            "Der Vorgang muss als "
            + "prüfbedürftig gelten.");
    }

    // =========================================
    // WIEDERHOLTE AUSFÜHRUNGEN
    // =========================================

    private static void TestLatestFailureWins()
    {
        var earlierSuccess = CreateAttempt(
            startMinute: 2,
            finishMinute: 3);

        var laterFailure = CreateAttempt(
            exitCode: 17,
            startMinute: 5,
            finishMinute: 6);

        var status = Evaluate(
            laterFailure,
            earlierSuccess);

        Require(
            status.State ==
            ProgramUpdatePackageWorkState.Failed,
            "Der zeitlich letzte Fehlschlag "
            + "muss entscheiden.");

        Require(
            status.LastExitCode == 17,
            "Der neueste Fehlercode "
            + "muss verwendet werden.");

        Require(
            status.CanOfferInstallation,
            "Das Paket muss erneut "
            + "auswählbar sein.");
    }

    private static void TestLatestSuccessWins()
    {
        var earlierFailure = CreateAttempt(
            exitCode: 14,
            startMinute: 2,
            finishMinute: 3);

        var laterSuccess = CreateAttempt(
            startMinute: 5,
            finishMinute: 6);

        var status = Evaluate(
            laterSuccess,
            earlierFailure);

        Require(
            status.State ==
            ProgramUpdatePackageWorkState
                .AwaitingVerification,
            "Der letzte erfolgreiche "
            + "Befehl muss Vorrang haben.");

        Require(
            status.IsAwaitingVerification,
            "Trotz Erfolg bleibt "
            + "die Nachkontrolle notwendig.");
    }

    // =========================================
    // PAKETIDENTITÄT
    // =========================================

    private static void TestDifferentTargetVersion()
    {
        var status = Evaluate(
            CreateAttempt(
                version: "1.9.0"));

        Require(
            status.State ==
            ProgramUpdatePackageWorkState.Pending,
            "Ein Erfolg für eine andere "
            + "Zielversion darf das neue "
            + "Update nicht ausblenden.");

        Require(
            status.CanOfferInstallation,
            "Die aktuelle Zielversion "
            + "muss auswählbar bleiben.");
    }

    private static void TestDifferentSource()
    {
        var status = Evaluate(
            CreateAttempt(
                source: "msstore"));

        Require(
            status.State ==
            ProgramUpdatePackageWorkState.Pending,
            "Eine andere Paketquelle "
            + "darf nicht gleichgesetzt werden.");
    }

    private static void TestDifferentPackageId()
    {
        var status = Evaluate(
            CreateAttempt(
                packageId: "Example.OtherApplication"));

        Require(
            status.State ==
            ProgramUpdatePackageWorkState.Pending,
            "Aktionen anderer Programme "
            + "dürfen das Paket nicht beeinflussen.");
    }

    // =========================================
    // SCANZEITPUNKT UND DATENQUALITÄT
    // =========================================

    private static void TestOldActionDoesNotHideUpdate()
    {
        var status = Evaluate(
            CreateAttempt(
                startMinute: -10,
                finishMinute: -9));

        Require(
            status.State ==
            ProgramUpdatePackageWorkState.Pending,
            "Ein alter erfolgreicher "
            + "WinGet-Aufruf darf einen "
            + "neueren Scanbefund nicht erledigen.");

        Require(
            status.CanOfferInstallation,
            "Das erneut erkannte Update "
            + "muss auswählbar sein.");
    }

    private static void TestActionStartedBeforeScan()
    {
        var status = Evaluate(
            CreateAttempt(
                startMinute: -1,
                finishMinute: 1));

        Require(
            status.State ==
            ProgramUpdatePackageWorkState.Pending,
            "Eine bereits vor dem Scan "
            + "gestartete Aktion darf den "
            + "aktuellen Scanbefund nicht "
            + "automatisch ausblenden.");
    }

    private static void TestUnreliableScan()
    {
        var information = CreateInformation();

        information.IsResultTruncated = true;

        var status = Evaluate(
            information,
            CreateAttempt());

        Require(
            status.State ==
            ProgramUpdatePackageWorkState.NotAssessable,
            "Ein unvollständiger Scan "
            + "darf keine belastbare "
            + "Paketentscheidung liefern.");

        Require(
            status.NeedsManualReview,
            "Die manuelle Prüfung "
            + "muss erforderlich sein.");

        Require(
            !status.CanOfferInstallation,
            "Ein unzuverlässiger Scan "
            + "darf keine automatische "
            + "Installationsfreigabe erzeugen.");
    }

    // =========================================
    // MEHRERE EINZELBEFEHLE
    // =========================================

    private static void TestMultipleCommandsInOneAction()
    {
        var action = CreateAttempt();

        action.Details +=
            Environment.NewLine
            + Environment.NewLine
            + BuildCommandDetails(
                PackageId,
                TargetVersion,
                PackageSource,
                true,
                42);

        var status = Evaluate(action);

        Require(
            status.State ==
            ProgramUpdatePackageWorkState.Failed,
            "Bei mehrfach dokumentierten "
            + "Einzelbefehlen muss der "
            + "letzte Befehl entscheiden.");

        Require(
            status.LastExitCode == 42,
            "Der letzte technische "
            + "Fehlercode muss verwendet werden.");
    }

    // =========================================
    // KÜNSTLICHE TESTDATEN
    // =========================================

    private static ProgramUpdateInformation
        CreateInformation()
    {
        return new ProgramUpdateInformation
        {
            IsWingetAvailable = true,

            IsAnalysisPerformed = true,

            IsAnalysisSuccessful = true,

            AnalysisDate = AnalysisDate,

            AvailableUpdateCount = 1,

            IsResultTruncated = false,

            AvailableUpdates =
            [
                new AvailableProgramUpdate
                {
                    PackageId = PackageId,

                    Name = "Künstliche Testanwendung",

                    InstalledVersion = "1.0.0",

                    AvailableVersion = TargetVersion,

                    Source = PackageSource
                }
            ]
        };
    }

    private static CheckupTaskActionResult
        CreateAttempt(
            string packageId = PackageId,
            string version = TargetVersion,
            string source = PackageSource,
            bool? started = true,
            int? exitCode = 0,
            int startMinute = 2,
            int finishMinute = 3,
            bool includeFinish = true)
    {
        return new CheckupTaskActionResult
        {
            ActionCode = ActionCode,

            ActionTitle =
                "Künstlicher Programmupdateversuch",

            Status =
                exitCode == 0 && started == true
                    ? CheckupTaskActionStatus.Successful
                    : CheckupTaskActionStatus.Failed,

            StartedAt =
                AnalysisAt.AddMinutes(startMinute),

            FinishedAt =
                includeFinish
                    ? AnalysisAt.AddMinutes(finishMinute)
                    : null,

            Details =
                BuildCommandDetails(
                    packageId,
                    version,
                    source,
                    started,
                    exitCode)
        };
    }

    private static string BuildCommandDetails(
        string packageId,
        string version,
        string source,
        bool? started,
        int? exitCode)
    {
        var startedText =
            started switch
            {
                true => "Ja",
                false => "Nein",
                _ => "Unbekannt"
            };

        var exitCodeText =
            exitCode?.ToString(
                CultureInfo.InvariantCulture)
            ?? "Nicht verfügbar";

        return
            "winget.exe upgrade "
            + $"--id \"{packageId}\" "
            + $"--version \"{version}\" "
            + $"--source \"{source}\" "
            + "--exact"
            + Environment.NewLine
            + "Gestartet: "
            + startedText
            + Environment.NewLine
            + "Exitcode: "
            + exitCodeText;
    }

    private static CheckupTaskList CreateTaskList(
        params CheckupTaskActionResult[] actions)
    {
        return new CheckupTaskList
        {
            TaskListVersion = 1,

            Tasks =
            [
                new CheckupTask
                {
                    TaskCode = TaskCode,

                    Title =
                        "Künstliche Programmupdate-Aufgabe",

                    ActionResults =
                        actions.ToList()
                }
            ]
        };
    }

    // =========================================
    // TESTAUSFÜHRUNG
    // =========================================

    private static ProgramUpdatePackageWorkStatus
        Evaluate(
            params CheckupTaskActionResult[] actions)
    {
        return Evaluate(
            CreateInformation(),
            actions);
    }

    private static ProgramUpdatePackageWorkStatus
        Evaluate(
            ProgramUpdateInformation information,
            params CheckupTaskActionResult[] actions)
    {
        var taskList =
            CreateTaskList(actions);

        var statuses =
            ProgramUpdatePackageWorkStatusService
                .Evaluate(
                    information,
                    taskList);

        Require(
            statuses.Count == 1,
            "Der künstliche Scan muss "
            + "genau einen Paketstatus erzeugen.");

        return statuses[0];
    }

    private static void Execute(
        string name,
        Action test,
        List<string> results)
    {
        try
        {
            test();

            results.Add(
                "OK: " + name);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "WinGet-Regressionstest fehlgeschlagen: "
                + name
                + Environment.NewLine
                + exception.Message,
                exception);
        }
    }

    private static void Require(
        bool condition,
        string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                message);
        }
    }
}

#endif

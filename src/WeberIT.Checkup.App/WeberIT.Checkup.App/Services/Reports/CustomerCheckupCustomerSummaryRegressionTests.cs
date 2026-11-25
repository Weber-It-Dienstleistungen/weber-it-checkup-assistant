
#if DEBUG

using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Reports;

/// <summary>
/// Regressionstests für die kundenverständliche
/// Zusammenfassung eines Checkup-Abschlusses.
///
/// Ausschließlich in Debug-Builds verfügbar.
///
/// Verwendet nur künstliche Vergleichsdaten
/// im Arbeitsspeicher.
///
/// Keine Datenbank, keine Systemscans,
/// keine PDF-Dateien und keine Kundendaten.
///
/// Die Tests starten niemals automatisch.
/// </summary>
public static class CustomerCheckupCustomerSummaryRegressionTests
{
    private const string ProgramUpdates =
        "action.program-updates.selected-upgrades";

    private const string Cleanup =
        "action.cleanup.selected-safe-categories";

    private const string SystemFileCheck =
        "action.maintenance.system-file-check";

    private const string ImageRepair =
        "action.maintenance.windows-image-repair";

    public static string RunAll()
    {
        var results = new List<string>();

        Execute(
            "Laptop: vier erfolgreiche Wartungsaktionen",
            TestLaptopMaintenance,
            results);

        Execute(
            "Desktop: drei erfolgreiche Wartungsaktionen",
            TestDesktopMaintenance,
            results);

        Execute(
            "Mehrfach ausgeführte Updates zusammenfassen",
            TestRepeatedActions,
            results);

        Execute(
            "Fehlerhafte und abgebrochene Aktionen trennen",
            TestMixedActionResults,
            results);

        Execute(
            "Ausschließlich fehlgeschlagene Aktionen",
            TestFailedActionsOnly,
            results);

        Execute(
            "Technisch behobenen Befund korrekt benennen",
            TestResolvedFinding,
            results);

        Execute(
            "SFC-Prüfung nicht als Reparatur behaupten",
            TestSystemFileCheckWording,
            results);

        Execute(
            "Unbekannte Wartungsaktion beibehalten",
            TestUnknownAction,
            results);

        Execute(
            "Begrenzung der dargestellten Maßnahmenarten",
            TestActionOverviewLimit,
            results);

        Execute(
            "Neutrale Information nicht als Reparatur zählen",
            TestInformationalFinding,
            results);

        Execute("Kontrollscan schließt Aufgabe ohne manuelle Statusänderung",
            TestControlScanClosed, results);
        Execute("Strukturierte Bereinigungsmenge und Ausnahmen verständlich darstellen",
            TestCleanupQuantities, results);
        Execute("Alte, doppelte oder unvollständige Protokolle nicht aufsummieren",
            TestUnreliableCleanupQuantities, results);
        Execute("Offene Aufgaben und manuell erledigte Aufgaben getrennt halten",
            TestTaskStatusSeparation, results);

        Execute("Akzeptiertes Risiko bleibt Befund und wird nicht als behoben gezählt",
            TestRiskAcceptedSummary, results);
        Execute("Fachlicher Autostartabschluss bleibt technischer Befund",
            TestReviewedStartupSummary, results);
        Execute("Nur eindeutig erfolgreiche WinGet-Protokolle komprimieren",
            TestCompactProgramActions, results);

        Execute("Vorbeugende Wartung mit akzeptierten Empfehlungen suggeriert keinen Reparaturbedarf",
            TestPreventiveMaintenance, results);
        Execute("Nicht erneut auswertbare Befunde bleiben ausdrücklich dokumentiert",
            TestUncertainMaintenance, results);

        return
            $"Alle {results.Count} Tests zur "
            + "Kundenzusammenfassung erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    private static void TestPreventiveMaintenance()
    {
        var comparison = CreateComparison();
        comparison.Actions.Add(Successful(Cleanup));
        comparison.Actions.Add(Successful(SystemFileCheck));
        comparison.Findings.Add(new CustomerCheckupFindingComparison
        {
            Status = CustomerCheckupFindingComparisonStatus.StillOpen,
            BeforeSeverity = FindingSeverity.Recommendation,
            AfterSeverity = FindingSeverity.Recommendation,
            IsCustomerRiskAccepted = true
        });
        var summary = CustomerCheckupCustomerSummary.Create(comparison);
        RequireContains(summary.WorkResultText, "Pflege des Rechners");
        RequireNotContains(summary.WorkResultText, "zuvor festgestellte");
        Require(summary.ResolvedFindingCount == 0, "Keine Reparatur behaupten.");
    }

    private static void TestUncertainMaintenance()
    {
        var comparison = CreateComparison();
        comparison.Actions.Add(Successful(Cleanup));
        comparison.Findings.Add(new CustomerCheckupFindingComparison
        {
            Status = CustomerCheckupFindingComparisonStatus.NotReevaluatable,
            BeforeSeverity = FindingSeverity.Warning
        });
        var summary = CustomerCheckupCustomerSummary.Create(comparison);
        RequireContains(summary.WorkResultText, "nicht zuverlässig überprüft");
        Require(summary.ResolvedFindingCount == 0, "Nicht überprüfbar ist nicht behoben.");
    }

    private static void TestRiskAcceptedSummary()
    {
        var comparison = CreateComparison();
        comparison.AfterScoringVersion = 4;
        comparison.Findings.Add(new CustomerCheckupFindingComparison
        {
            Code = "system.security.mobile-drive-not-encrypted",
            Title = "Laufwerksverschlüsselung",
            Status = CustomerCheckupFindingComparisonStatus.StillOpen,
            AfterSeverity = FindingSeverity.Recommendation,
            IsCustomerRiskAccepted = true
        });
        var summary = CustomerCheckupCustomerSummary.Create(comparison);
        Require(summary.OpenFindingCount == 0 && summary.ResolvedFindingCount == 0,
            "Bewusste Risikoakzeptanz darf nicht als Reparatur erscheinen.");
        RequireContains(summary.RecommendationText, "bewusst akzeptierte");
        RequireContains(summary.RecommendationText, "ohne Punktabzug");
        Require(comparison.StillOpenFindingCount == 1,
            "Der technische Befund muss weiterhin gezählt werden.");
    }

    private static void TestReviewedStartupSummary()
    {
        var comparison = CreateComparison();
        comparison.Findings.Add(new CustomerCheckupFindingComparison
        {
            Code = "system.startup.extensive",
            Title = "Autostart",
            Status = CustomerCheckupFindingComparisonStatus.StillOpen,
            AfterSeverity = FindingSeverity.Recommendation,
            IsOptimizationReviewConfirmed = true
        });
        var summary = CustomerCheckupCustomerSummary.Create(comparison);
        Require(summary.OpenFindingCount == 0 && summary.ResolvedFindingCount == 0,
            "Gepruefter Autostart darf nicht als technisch repariert erscheinen.");
        RequireContains(summary.RecommendationText, "keine weitere Optimierung erforderlich");
        RequireNotContains(summary.RecommendationText, "Risiko");
    }

    private static void TestCompactProgramActions()
    {
        var action = CreateAction(ProgramUpdates, CheckupTaskActionStatus.Successful);
        action.StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        action.FinishedAt = DateTimeOffset.UtcNow;
        action.TargetDescription = "• Test (Example.Test): 1 → 2";
        action.Details = "winget.exe upgrade --id Example.Test --version 2 --source winget --exact"
            + Environment.NewLine + "Gestartet: Ja" + Environment.NewLine + "Exitcode: 0";
        Require(CustomerCheckupPdfReportService.CanSummarizeProgramAction(action),
            "Eindeutiges Erfolgsprotokoll sollte komprimiert werden.");
        action.RestartRequired = true;
        Require(!CustomerCheckupPdfReportService.CanSummarizeProgramAction(action),
            "Neustarthinweise müssen einzeln dokumentiert bleiben.");
        action.RestartRequired = false;
        action.Status = CheckupTaskActionStatus.Failed;
        Require(!CustomerCheckupPdfReportService.CanSummarizeProgramAction(action),
            "Fehlerprotokolle müssen vollständig ausgegeben werden.");
        action.Status = CheckupTaskActionStatus.Successful;
        action.Details = string.Empty;
        Require(!CustomerCheckupPdfReportService.CanSummarizeProgramAction(action),
            "Unvollständige Protokolle dürfen nicht entfallen.");
    }

    private static void TestLaptopMaintenance()
    {
        var comparison = CreateComparison();

        comparison.Tasks.AddRange(
        [
            CompletedTask(),
            CompletedTask(),
            CompletedTask()
        ]);

        comparison.Actions.AddRange(
        [
            Successful(ProgramUpdates),
            Successful(ProgramUpdates),
            Successful(Cleanup),
            Successful(SystemFileCheck)
        ]);

        var summary =
            CustomerCheckupCustomerSummary.Create(comparison);

        Require(
            summary.CompletedTaskCount == 3,
            "Es müssen drei erledigte Aufgaben vorliegen.");

        Require(
            summary.SuccessfulActionCount == 4,
            "Es müssen vier erfolgreiche Aktionen vorliegen.");

        RequireContains(
            summary.WorkResultText,
            "Programmaktualisierungen (2 Vorgänge)");

        RequireContains(
            summary.WorkResultText,
            "Bereinigung ausgewählter Dateien");

        RequireContains(
            summary.WorkResultText,
            "Prüfung der Windows-Systemdateien");

        RequireContains(
            summary.WorkResultText,
            "keine technische Reparatur abgeleitet");

        Require(
            summary.ResolvedFindingCount == 0,
            "Wartungsaktionen dürfen nicht automatisch "
            + "als behobene Befunde gezählt werden.");
    }

    private static void TestDesktopMaintenance()
    {
        var comparison = CreateComparison();

        comparison.Actions.AddRange(
        [
            Successful(Cleanup),
            Successful(ProgramUpdates),
            Successful(SystemFileCheck)
        ]);

        var summary =
            CustomerCheckupCustomerSummary.Create(comparison);

        Require(
            summary.SuccessfulActionCount == 3,
            "Es müssen drei erfolgreiche Aktionen vorliegen.");

        RequireContains(
            summary.WorkResultText,
            "Programmaktualisierungen");

        RequireContains(
            summary.WorkResultText,
            "Bereinigung ausgewählter Dateien");

        RequireContains(
            summary.WorkResultText,
            "Prüfung der Windows-Systemdateien");

        Require(
            summary.ResolvedFindingCount == 0,
            "Die Wartung allein bestätigt "
            + "keine behobenen Bewertungsbefunde.");
    }

    private static void TestRepeatedActions()
    {
        var comparison = CreateComparison();

        comparison.Actions.AddRange(
        [
            Successful(ProgramUpdates),
            Successful(ProgramUpdates),
            Successful(ProgramUpdates)
        ]);

        var summary =
            CustomerCheckupCustomerSummary.Create(comparison);

        Require(
            summary.SuccessfulActionCount == 3,
            "Drei Ausführungen müssen drei Aktionen bleiben.");

        RequireContains(
            summary.WorkResultText,
            "Programmaktualisierungen (3 Vorgänge)");

        Require(
            CountOccurrences(
                summary.WorkResultText,
                "Programmaktualisierungen") == 1,
            "Die Maßnahmenart darf nur einmal "
            + "in der Übersicht erscheinen.");
    }

    private static void TestMixedActionResults()
    {
        var comparison = CreateComparison();

        comparison.Actions.AddRange(
        [
            Successful(Cleanup),

            CreateAction(
                ImageRepair,
                CheckupTaskActionStatus.Failed),

            CreateAction(
                ProgramUpdates,
                CheckupTaskActionStatus.Cancelled)
        ]);

        var summary =
            CustomerCheckupCustomerSummary.Create(comparison);

        Require(
            summary.SuccessfulActionCount == 1,
            "Nur eine Aktion darf erfolgreich sein.");

        Require(
            summary.FailedActionCount == 1,
            "Ein Fehlschlag muss dokumentiert bleiben.");

        Require(
            summary.CancelledActionCount == 1,
            "Ein Abbruch muss dokumentiert bleiben.");

        RequireContains(
            summary.WorkResultText,
            "Bereinigung ausgewählter Dateien");

        RequireContains(
            summary.WorkResultText,
            "fehlgeschlagen");

        RequireContains(
            summary.WorkResultText,
            "abgebrochen");

        RequireNotContains(
            summary.WorkResultText,
            "Wartung des Windows-Komponentenspeichers");

        RequireNotContains(
            summary.WorkResultText,
            "Programmaktualisierungen");
    }

    private static void TestFailedActionsOnly()
    {
        var comparison = CreateComparison();

        comparison.Actions.Add(
            CreateAction(
                ProgramUpdates,
                CheckupTaskActionStatus.Failed));

        var summary =
            CustomerCheckupCustomerSummary.Create(comparison);

        Require(
            summary.SuccessfulActionCount == 0,
            "Es darf keine erfolgreiche Aktion vorliegen.");

        Require(
            summary.FailedActionCount == 1,
            "Der Fehlschlag muss gezählt werden.");

        RequireContains(
            summary.WorkResultText,
            "fehlgeschlagen");

        RequireNotContains(
            summary.WorkResultText,
            "Programmaktualisierungen");

        RequireNotContains(
            summary.WorkResultText,
            "Wartungsaktionen wurden erfolgreich");
    }

    private static void TestResolvedFinding()
    {
        var comparison = CreateComparison();

        comparison.Findings.Add(
            new CustomerCheckupFindingComparison
            {
                Code = "synthetic.resolved",
                Title = "Künstlicher Prüfbefund",

                Status =
                    CustomerCheckupFindingComparisonStatus.Resolved,

                BeforeSeverity =
                    FindingSeverity.Warning
            });

        var summary =
            CustomerCheckupCustomerSummary.Create(comparison);

        Require(
            summary.ResolvedFindingCount == 1,
            "Ein technisch behobener Befund "
            + "muss erkannt werden.");

        RequireContains(
            summary.WorkResultText,
            "nicht mehr nachweisbar");

        RequireNotContains(
            summary.WorkResultText,
            "konnte im Abschlussvergleich "
            + "nicht eindeutig bestätigt werden");
    }

    private static void TestSystemFileCheckWording()
    {
        var comparison = CreateComparison();

        comparison.Actions.Add(
            Successful(SystemFileCheck));

        var summary =
            CustomerCheckupCustomerSummary.Create(comparison);

        RequireContains(
            summary.WorkResultText,
            "Prüfung der Windows-Systemdateien");

        RequireNotContains(
            summary.WorkResultText,
            "Beschädigte Systemdateien wurden repariert");

        Require(
            summary.ResolvedFindingCount == 0,
            "Die erfolgreiche Durchführung "
            + "einer SFC-Prüfung ist nicht automatisch "
            + "ein behobener Bewertungsbefund.");
    }

    private static void TestUnknownAction()
    {
        var comparison = CreateComparison();

        comparison.Actions.Add(
            new CustomerCheckupActionSummary
            {
                ActionCode = "action.synthetic.custom",
                ActionTitle = "Individuelle Wartungsprüfung",
                Status = CheckupTaskActionStatus.Successful
            });

        var summary =
            CustomerCheckupCustomerSummary.Create(comparison);

        RequireContains(
            summary.WorkResultText,
            "Individuelle Wartungsprüfung");

        Require(
            summary.SuccessfulActionCount == 1,
            "Auch unbekannte Aktionen "
            + "müssen korrekt gezählt werden.");
    }

    private static void TestActionOverviewLimit()
    {
        var comparison = CreateComparison();

        comparison.Actions.AddRange(
        [
            Successful(ProgramUpdates),
            Successful(Cleanup),
            Successful(SystemFileCheck),
            Successful(ImageRepair)
        ]);

        var summary =
            CustomerCheckupCustomerSummary.Create(comparison);

        Require(
            summary.SuccessfulActionCount == 4,
            "Alle vier Aktionen müssen gezählt werden.");

        RequireContains(
            summary.WorkResultText,
            "eine weitere Maßnahme");

        RequireNotContains(
            summary.WorkResultText,
            "Wartung des Windows-Komponentenspeichers");

        RequireContains(
            summary.WorkResultText,
            "Programmaktualisierungen");

        RequireContains(
            summary.WorkResultText,
            "Bereinigung ausgewählter Dateien");

        RequireContains(
            summary.WorkResultText,
            "Prüfung der Windows-Systemdateien");
    }

    private static void TestInformationalFinding()
    {
        var comparison = CreateComparison();

        comparison.Findings.Add(
            new CustomerCheckupFindingComparison
            {
                Code = "synthetic.information",
                Title = "Neutrale Systeminformation",

                Status =
                    CustomerCheckupFindingComparisonStatus.Informational,

                BeforeSeverity =
                    FindingSeverity.Information,

                AfterSeverity =
                    FindingSeverity.Information
            });

        comparison.Actions.Add(
            Successful(Cleanup));

        var summary =
            CustomerCheckupCustomerSummary.Create(comparison);

        Require(
            summary.ResolvedFindingCount == 0,
            "Eine neutrale Information "
            + "darf nicht als behoben zählen.");

        Require(
            summary.SuccessfulActionCount == 1,
            "Die unabhängige Wartungsaktion "
            + "muss erhalten bleiben.");

        RequireContains(
            summary.WorkResultText,
            "Bereinigung ausgewählter Dateien");

        RequireContains(
            summary.WorkResultText,
            "keine technische Reparatur abgeleitet");
    }

    private static void TestControlScanClosed()
    {
        var comparison = CreateComparison();
        var task = new CustomerCheckupTaskComparison
        {
            WorkStatus = CheckupTaskStatus.Open,
            Status = CustomerCheckupTaskComparisonStatus.NoLongerDetected
        };
        comparison.Tasks.Add(task);
        var summary = CustomerCheckupCustomerSummary.Create(comparison);
        Require(summary.CompletedTaskCount == 0, "Kein manueller Abschluss.");
        Require(summary.ControlScanClosedTaskCount == 1, "Kontrollabschluss fehlt.");
        Require(task.IsClosedByControlScan && !task.IsStillOpenForWork, "Aufgabe bleibt fälschlich offen.");
        RequireContains(summary.WorkResultText, "Eine Aufgabe wurde durch die Abschlusskontrolle abgeschlossen");
        RequireNotContains(summary.WorkResultText, "Es wurden keine Aufgaben als erledigt");
        Require(task.WorkStatus == CheckupTaskStatus.Open, "Originalstatus wurde verändert.");
    }

    private static CustomerCheckupActionSummary StructuredCleanup(long files, ulong bytes)
    {
        var action = Successful(Cleanup);
        action.CleanupDeletedFileCount = files;
        action.CleanupDeletedSizeBytes = bytes;
        action.CleanupWorkCompleted = true;
        action.CleanupHasExceptions = false;
        return action;
    }

    private static void TestCleanupQuantities()
    {
        var comparison = CreateComparison();
        var first = StructuredCleanup(20, 1024UL * 1024 * 1024);
        first.CleanupHasExceptions = true;
        comparison.Actions.Add(first);
        comparison.Actions.Add(StructuredCleanup(22, 1024UL * 1024 * 1024));
        comparison.Actions.Add(CreateAction(Cleanup, CheckupTaskActionStatus.Failed));
        var summary = CustomerCheckupCustomerSummary.Create(comparison);
        RequireContains(summary.WorkResultText, "42 Dateien");
        RequireContains(summary.WorkResultText, "2 GB");
        RequireContains(summary.WorkResultText, "Nicht entfernte Einträge");
        RequireContains(summary.WorkResultText, "fehlgeschlagen");
        RequireNotContains(summary.WorkResultText, "Es wurden keine Aufgaben als erledigt");
    }

    private static void TestUnreliableCleanupQuantities()
    {
        var comparison = CreateComparison();
        var first = StructuredCleanup(42, 1024);
        comparison.Actions.Add(first);
        comparison.Actions.Add(Successful(Cleanup));
        RequireNotContains(CustomerCheckupCustomerSummary.Create(comparison).WorkResultText, "42 Dateien");
        comparison.Actions[1] = first;
        RequireNotContains(CustomerCheckupCustomerSummary.Create(comparison).WorkResultText, "84 Dateien");
        comparison.Actions.RemoveAt(1);
        first.CleanupWorkCompleted = false;
        RequireNotContains(CustomerCheckupCustomerSummary.Create(comparison).WorkResultText, "42 Dateien");
    }

    private static void TestTaskStatusSeparation()
    {
        var comparison = CreateComparison();
        comparison.Tasks.Add(CompletedTask());
        comparison.Tasks.Add(new CustomerCheckupTaskComparison
        {
            WorkStatus = CheckupTaskStatus.Open,
            Status = CustomerCheckupTaskComparisonStatus.StillOpen
        });
        comparison.Tasks.Add(new CustomerCheckupTaskComparison
        {
            WorkStatus = CheckupTaskStatus.Open,
            Status = CustomerCheckupTaskComparisonStatus.NotReevaluatable
        });
        comparison.Tasks.Add(new CustomerCheckupTaskComparison
        {
            WorkStatus = CheckupTaskStatus.Open,
            Status = CustomerCheckupTaskComparisonStatus.NoLongerDetected
        });
        var summary = CustomerCheckupCustomerSummary.Create(comparison);
        Require(summary.CompletedTaskCount == 1, "Manueller Abschluss fehlt.");
        Require(summary.ControlScanClosedTaskCount == 1, "Kontrollabschluss fehlt.");
        Require(comparison.Tasks.Count(task => task.IsStillOpenForWork) == 2,
            "Offene oder nicht erneut prüfbare Aufgaben wurden geschlossen.");
    }

    private static CustomerCheckupComparison CreateComparison()
    {
        /*
         * Der Vergleich wird vollständig
         * künstlich erzeugt.
         *
         * Keine Kunden-ID, keine Geräte-ID,
         * keine Datenbankabfrage.
         */

        return new CustomerCheckupComparison
        {
            ComparisonModelVersion = 2,

            SystemScore = new CustomerCheckupScoreComparison
            {
                Kind = CustomerCheckupScoreKind.System,
                BeforeScore = 80,
                AfterScore = 85,
                IsComparable = false
            },

            HardwareScore = new CustomerCheckupScoreComparison
            {
                Kind = CustomerCheckupScoreKind.Hardware,
                BeforeScore = 95,
                AfterScore = 95,
                IsComparable = true,
                Difference = 0,
                Change = CustomerCheckupScoreChange.Unchanged
            }
        };
    }

    private static CustomerCheckupTaskComparison CompletedTask()
    {
        return new CustomerCheckupTaskComparison
        {
            Title = "Künstliche Wartungsaufgabe",
            WorkStatus = CheckupTaskStatus.Completed,
            Status = CustomerCheckupTaskComparisonStatus.Completed
        };
    }

    private static CustomerCheckupActionSummary Successful(
        string actionCode)
    {
        return CreateAction(
            actionCode,
            CheckupTaskActionStatus.Successful);
    }

    private static CustomerCheckupActionSummary CreateAction(
        string actionCode,
        CheckupTaskActionStatus status)
    {
        return new CustomerCheckupActionSummary
        {
            ActionResultId = Guid.NewGuid(),
            ActionCode = actionCode,
            ActionTitle = "Künstliche Wartungsaktion",
            Status = status
        };
    }

    private static void Execute(
        string name,
        Action test,
        ICollection<string> results)
    {
        try
        {
            test();
            results.Add("OK: " + name);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Regressionstest fehlgeschlagen: "
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
            throw new InvalidOperationException(message);
        }
    }

    private static void RequireContains(
        string actual,
        string expected)
    {
        Require(
            actual.Contains(
                expected,
                StringComparison.OrdinalIgnoreCase),
            "Folgender Text fehlt: " + expected
            + Environment.NewLine
            + "Tatsächliche Ausgabe: "
            + actual);
    }

    private static void RequireNotContains(
        string actual,
        string unexpected)
    {
        Require(
            !actual.Contains(
                unexpected,
                StringComparison.OrdinalIgnoreCase),
            "Folgender Text darf nicht erscheinen: "
            + unexpected
            + Environment.NewLine
            + "Tatsächliche Ausgabe: "
            + actual);
    }

    private static int CountOccurrences(
        string text,
        string value)
    {
        return text.Split(
            value,
            StringSplitOptions.None).Length - 1;
    }
}

#endif


#if DEBUG

using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Assessment;

/// <summary>
/// Isolierte Regressionstests für die
/// fachliche Bewertung des Neustartstatus.
///
/// Es werden ausschließlich künstliche
/// Daten verwendet.
///
/// Keine Registry-Zugriffe.
/// Keine Windows-Neustarts.
/// Keine Datenbankzugriffe.
/// Keine Änderungen an Kundendaten.
/// </summary>
public static class RestartAssessmentRegressionTests
{
    public static string RunAll()
    {
        var results = new List<string>();

        Execute(
            "Windows Update verlangt Neustart",
            TestWindowsUpdateRestart,
            results);

        Execute(
            "Komponentenwartung verlangt Neustart",
            TestComponentRestart,
            results);

        Execute(
            "Computername-Änderung verlangt Neustart",
            TestComputerRenameRestart,
            results);

        Execute(
            "Dateioperationen bleiben reine Information",
            TestFileOperationsAdvisory,
            results);

        Execute(
            "Ohne Neustartindikatoren keine Warnung",
            TestNoRestartRequired,
            results);

        Execute(
            "Unvollständige Quellenprüfung bleibt unklar",
            TestIncompleteAnalysis,
            results);

        Execute(
            "Bestätigter Neustart trotz Quellenfehler",
            TestConfirmedRestartWithFailedSource,
            results);

        Execute(
            "Historischer Checkup bleibt kompatibel",
            TestLegacyCheckup,
            results);

        return
            $"Alle {results.Count} Regressionstests "
            + "für die Neustartbewertung erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =============================================
    // TEST 1: WINDOWS UPDATE
    // =============================================

    private static void TestWindowsUpdateRestart()
    {
        var finding = Evaluate(
            CreateSession(
                restartRequired: true,
                conclusive: true,
                windowsUpdatePending: true));

        RequireWarning(
            finding,
            "Windows Update");
    }

    // =============================================
    // TEST 2: KOMPONENTENWARTUNG
    // =============================================

    private static void TestComponentRestart()
    {
        var finding = Evaluate(
            CreateSession(
                restartRequired: true,
                conclusive: true,
                componentPending: true));

        RequireWarning(
            finding,
            "Windows-Komponentenwartung");
    }

    // =============================================
    // TEST 3: COMPUTERNAME
    // =============================================

    private static void TestComputerRenameRestart()
    {
        var finding = Evaluate(
            CreateSession(
                restartRequired: true,
                conclusive: true,
                computerRenamePending: true));

        RequireWarning(
            finding,
            "Computername-Änderung");
    }

    // =============================================
    // TEST 4: NUR DATEIOPERATIONEN
    // =============================================

    private static void TestFileOperationsAdvisory()
    {
        var session =
            CreateSession(
                restartRequired: false,
                conclusive: true,
                fileOperationsPending: true);

        var finding =
            Evaluate(session);

        Require(
            session.RestartInformation
                .HasAdvisoryRestartHint,
            "Der Dateioperationshinweis "
            + "muss erhalten bleiben.");

        Require(
            finding.Code ==
                "system.restart.advisory",
            "Der Hinweis muss als "
            + "separater Befund erscheinen.");

        Require(
            finding.Severity ==
                FindingSeverity.Information,
            "Dateioperationen allein dürfen "
            + "keine Neustartempfehlung auslösen.");

        Require(
            finding.AssessmentTarget ==
                FindingAssessmentTarget.InformationOnly,
            "Ein bloßer Hinweis darf nicht "
            + "in die Systembewertung einfließen.");

        Require(
            finding.CauseGroup ==
                "system.restart.file-operations",
            "Dateioperationen und bestätigter "
            + "Neustartbedarf müssen fachlich "
            + "getrennt bleiben.");

        Require(
            session.RestartInformation
                .IsRestartRequired == false,
            "Die Bewertung darf keinen "
            + "Neustartbedarf erzeugen.");
    }

    // =============================================
    // TEST 5: KEIN NEUSTARTBEDARF
    // =============================================

    private static void TestNoRestartRequired()
    {
        var finding = Evaluate(
            CreateSession(
                restartRequired: false,
                conclusive: true));

        Require(
            finding.Code ==
                "system.restart.none-required",
            "Ohne positive Neustartindikatoren "
            + "muss der negative Befund erscheinen.");

        Require(
            finding.Severity ==
                FindingSeverity.Information,
            "Ein negativer Befund "
            + "darf keine Warnung sein.");

        Require(
            finding.AssessmentTarget ==
                FindingAssessmentTarget.SystemCondition,
            "Ein vollständig bestätigter "
            + "negativer Befund ist für die "
            + "Systembewertung verfügbar.");
    }

    // =============================================
    // TEST 6: FEHLGESCHLAGENE QUELLE
    // =============================================

    private static void TestIncompleteAnalysis()
    {
        var finding = Evaluate(
            CreateSession(
                restartRequired: null,
                conclusive: false,
                fileOperationsPending: true,
                componentCheckFailed: true));

        Require(
            finding.Code ==
                "system.restart.not-fully-evaluable",
            "Eine unvollständige Prüfung "
            + "darf keine Entwarnung auslösen.");

        Require(
            finding.AssessmentTarget ==
                FindingAssessmentTarget.InformationOnly,
            "Eine unvollständige Prüfung "
            + "darf nicht als bestätigter "
            + "Systemzustand gelten.");

        Require(
            finding.Description.Contains(
                "Windows-Komponentenwartung",
                StringComparison.Ordinal),
            "Die fehlgeschlagene Quelle "
            + "muss benannt werden.");

        Require(
            finding.Description.Contains(
                "Dateioperationen",
                StringComparison.OrdinalIgnoreCase),
            "Der zusätzliche Dateioperationshinweis "
            + "muss weiterhin sichtbar sein.");
    }

    // =============================================
    // TEST 7: BESTÄTIGTER BEDARF UND QUELLENFEHLER
    // =============================================

    private static void TestConfirmedRestartWithFailedSource()
    {
        var finding = Evaluate(
            CreateSession(
                restartRequired: true,
                conclusive: true,
                windowsUpdatePending: true,
                componentCheckFailed: true));

        RequireWarning(
            finding,
            "Windows Update");
    }

    // =============================================
    // TEST 8: HISTORISCHE CHECKUPS
    // =============================================

    private static void TestLegacyCheckup()
    {
        var session = new CheckupSession
        {
            RestartInformation =
                new RestartInformation
                {
                    IsAnalysisPerformed = false
                },

            WindowsUpdateInformation =
                new WindowsUpdateInformation
                {
                    IsRestartRequired = true,

                    RestartReasons =
                    [
                        "Historischer Windows-Update-Befund"
                    ]
                }
        };

        var finding =
            Evaluate(session);

        Require(
            finding.Code ==
                "system.restart.legacy-required",
            "Historische Checkups müssen "
            + "weiterhin ihre bisherige "
            + "Neustartbewertung verwenden.");

        Require(
            finding.Severity ==
                FindingSeverity.Warning,
            "Historisch bestätigter Neustartbedarf "
            + "muss weiterhin eine Warnung sein.");
    }

    // =============================================
    // KÜNSTLICHE TESTDATEN
    // =============================================

    private static CheckupSession CreateSession(
        bool? restartRequired,
        bool conclusive,
        bool windowsUpdatePending = false,
        bool componentPending = false,
        bool computerRenamePending = false,
        bool fileOperationsPending = false,
        bool componentCheckFailed = false)
    {
        var information =
            new RestartInformation
            {
                IsAnalysisPerformed = true,

                IsAnalysisConclusive =
                    conclusive,

                IsRestartRequired =
                    restartRequired,

                AnalysisDate =
                    new DateTime(
                        2026, 10, 29, 10, 0, 0),

                Sources =
                [
                    CreateSource(
                        RestartSourceType.WindowsUpdate,
                        "Windows Update",
                        windowsUpdatePending),

                    CreateSource(
                        RestartSourceType.ComponentBasedServicing,
                        "Windows-Komponentenwartung",
                        componentPending,
                        !componentCheckFailed),

                    CreateSource(
                        RestartSourceType.PendingFileRenameOperations,
                        "Ausstehende Dateioperationen",
                        fileOperationsPending),

                    CreateSource(
                        RestartSourceType.PendingComputerRename,
                        "Ausstehende Computername-Änderung",
                        computerRenamePending)
                ]
            };

        return new CheckupSession
        {
            RestartInformation = information
        };
    }

    private static RestartSourceResult CreateSource(
        RestartSourceType type,
        string name,
        bool pending,
        bool successful = true)
    {
        return new RestartSourceResult
        {
            SourceType = type,

            DisplayName = name,

            IsCheckSuccessful = successful,

            IsRestartRequired =
                successful
                    ? pending
                    : null,

            Details =
                "Künstlicher Testbefund."
        };
    }

    // =============================================
    // REGEL AUSWERTEN
    // =============================================

    private static CheckupFinding Evaluate(
        CheckupSession session)
    {
        var rule =
            new RestartAssessmentRule();

        return rule
            .Evaluate(session)
            .Single();
    }

    private static void RequireWarning(
        CheckupFinding finding,
        string expectedSource)
    {
        Require(
            finding.Code ==
                "system.restart.required",
            "Ein bestätigter Neustart "
            + "muss als erforderlich gelten.");

        Require(
            finding.Severity ==
                FindingSeverity.Warning,
            "Bestätigter Neustartbedarf "
            + "muss eine Warnung sein.");

        Require(
            finding.AssessmentTarget ==
                FindingAssessmentTarget.SystemCondition,
            "Bestätigter Neustartbedarf "
            + "muss in die Systembewertung "
            + "einfließen.");

        Require(
            finding.Description.Contains(
                expectedSource,
                StringComparison.Ordinal),
            "Die auslösende Windows-Quelle "
            + "muss genannt werden.");
    }

    // =============================================
    // TESTAUSFÜHRUNG
    // =============================================

    private static void Execute(
        string name,
        Action test,
        ICollection<string> results)
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
            throw new InvalidOperationException(
                message);
        }
    }
}

#endif


using System;
using System.Security.Principal;
using WeberIT.Checkup.App.Services.Cleanup;
using WeberIT.Checkup.App.Services.Tasks;

namespace WeberIT.Checkup.SandboxTestRunner;

/// <summary>
/// Sprint 55.14.34k
///
/// Eigenständiger C#-Sandbox-TestRunner.
///
/// 103 aktive Sicherheits- und Regressionstests:
///
/// - 52 Windows-Temp-Sicherheitstests.
/// - 13 Bereinigungs-Zwischenprüfungstests.
/// - 8 Protokoll-Zuordnungsprüfungen.
/// - 6 Arbeitslisten-Regressionstests.
/// - 7 Bereinigungsverlaufsschutztests.
/// - 5 technische Statusanzeigetests.
/// - 8 Kategorie-Fortschrittstests.
/// - 4 Arbeitsabschluss-Regressionstests.
///
/// Die vier veralteten PowerShell-Sandbox-
/// Testblöcke werden nicht mehr ausgeführt.
///
/// Sicherheitsregeln:
/// - Keine Administratorrechte.
/// - Kein Zugriff auf produktives Windows-Temp.
/// - Reale Löschungen nur in GUID-Sandboxen.
/// - Zwischenprüfungstests verwenden
///   ausschließlich künstliche Modellobjekte.
/// - Keine PowerShell-Ausführung.
/// - Keine produktive Handle-Adapter-Ausführung.
/// - Produktive Freigabesperre bleibt geschlossen.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main()
    {
#if DEBUG
        Console.WriteLine(
            "========================================");
        Console.WriteLine(
            " WEBER IT - C# SANDBOX TESTRUNNER");
        Console.WriteLine(
            "========================================");
        Console.WriteLine();

        // =====================================
        // PLATTFORM PRÜFEN
        // =====================================

        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine(
                "FEHLER: Windows wird benötigt.");

            return 2;
        }

        // =====================================
        // ADMINISTRATORRECHTE AUSSCHLIESSEN
        // =====================================

        using var identity =
            WindowsIdentity.GetCurrent();

        var principal =
            new WindowsPrincipal(identity);

        if (principal.IsInRole(
                WindowsBuiltInRole.Administrator))
        {
            Console.Error.WriteLine(
                "SICHERHEITSSTOPP: Der TestRunner "
                + "darf nicht mit Administratorrechten "
                + "ausgeführt werden.");

            return 2;
        }

        Console.WriteLine(
            "Berechtigungsprüfung: OK");

        Console.WriteLine(
            "Ausführung ohne Administratorrechte.");

        Console.WriteLine();

        try
        {
            // =================================
            // TESTBLOCK 1: C#-ERGEBNISVERTRÄGE
            // =================================

            RunTestBlock(
                "TESTBLOCK 1 - C#-ERGEBNISVERTRÄGE",
                WindowsTemporaryFilesCleanupBaselineRegressionTests
                    .RunAll);

            // =================================
            // TESTBLOCK 2: HANDLE-ZUGRIFF
            // =================================

            RunTestBlock(
                "TESTBLOCK 2 - HANDLE-BASIERTER DATEIZUGRIFF",
                WindowsTemporaryFilesHandleAccessRegressionTests
                    .RunAll);

            // =================================
            // TESTBLOCK 3: HANDLE-DATEILÖSCHUNG
            // =================================

            PrintHeading(
                "TESTBLOCK 3 - HANDLE-BASIERTE DATEILÖSCHUNG");

            Console.WriteLine(
                "Prüfe Dateilöschung, Schreibschutz, "
                + "Systemschutz, Dateisperren "
                + "und Identitätswechsel.");

            Console.WriteLine();

            Console.WriteLine(
                WindowsTemporaryFilesHandleDeletionRegressionTests
                    .RunAll());

            Console.WriteLine();

            // =================================
            // TESTBLOCK 4: VERZEICHNISLÖSCHUNG
            // =================================

            PrintHeading(
                "TESTBLOCK 4 - HANDLE-BASIERTE VERZEICHNISLÖSCHUNG");

            Console.WriteLine(
                "Prüfe leere und nicht leere Ordner, "
                + "Schreibschutz, Systemschutz, "
                + "Identitätswechsel und Objekttypen.");

            Console.WriteLine();

            Console.WriteLine(
                WindowsTemporaryFilesHandleDirectoryDeletionRegressionTests
                    .RunAll());

            Console.WriteLine();

            // =================================
            // TESTBLOCK 5: VERZEICHNISDURCHSUCHUNG
            // =================================

            PrintHeading(
                "TESTBLOCK 5 - HANDLE-BASIERTE VERZEICHNISDURCHSUCHUNG");

            Console.WriteLine(
                "Prüfe Aufzählung, verschachtelte "
                + "Ordner, Abbruch, Identitätswechsel "
                + "und Junction-Schutz.");

            Console.WriteLine();

            Console.WriteLine(
                WindowsTemporaryFilesHandleDirectoryEnumerationRegressionTests
                    .RunAll());

            Console.WriteLine();

            // =================================
            // TESTBLOCK 6: ENGINE-INTEGRATION
            // =================================

            PrintHeading(
                "TESTBLOCK 6 - VOLLSTÄNDIGER BEREINIGUNGSLAUF");

            Console.WriteLine(
                "Prüfe Zusammenspiel von Aufzählung, "
                + "Dateilöschung, Verzeichnislöschung, "
                + "Schutzmechanismen und Ergebniszählern.");

            Console.WriteLine(
                "Die Bereinigung betrifft ausschließlich "
                + "selbst erzeugte Sandbox-Testdaten.");

            Console.WriteLine();

            Console.WriteLine(
                WindowsTemporaryFilesHandleCleanupEngineRegressionTests
                    .RunAll());

            Console.WriteLine();

            // =================================
            // TESTBLOCK 7: EXECUTOR-SICHERHEIT
            // =================================

            RunTestBlock(
                "TESTBLOCK 7 - EXECUTOR-SICHERHEIT",
                WindowsTemporaryFilesCleanupExecutorReleaseGateRegressionTests
                    .RunAll);

            // =================================
            // TESTBLOCK 8:
            // BEREINIGUNGS-ZWISCHENPRÜFUNG
            // =================================

            /*
             * Sprint 55.14.34k
             *
             * Dieser Testblock verwendet keine
             * realen Verzeichnisse oder Dateien.
             *
             * Geprüft werden ausschließlich
             * künstliche Datenmodelle:
             *
             * - Größen- und Dateiveränderungen
             * - Unvollständige Messungen
             * - Unabhängige Objektkopien
             * - Mehrere Zwischenprüfungen
             * - JSON-Serialisierung
             * - Historische Daten
             * - Abschluss- und Kategorie-Schutz
             * - Kopien technischer Löschprotokolle
             *
             * Die bestehende Windows-Temp-
             * Ausführungssperre wird nicht
             * verändert.
             */
            RunTestBlock(
                "TESTBLOCK 8 - BEREINIGUNGS-ZWISCHENPRÜFUNG",
                CleanupPotentialIntermediateScanRegressionTests
                    .RunAll);

            // =================================
            // TESTBLOCK 9: BEREINIGUNGSPROTOKOLL-ZUORDNUNG
            // =================================

            /*
             * Ausschließlich künstliche Modelle.
             * Keine Dateisystemoperationen und
             * keine Änderung der Produktivfreigabe.
             */
            RunTestBlock(
                "TESTBLOCK 9 - BEREINIGUNGSPROTOKOLL-ZUORDNUNG",
                CleanupExecutionCorrelationResolverRegressionTests
                    .RunAll);

            // =================================
            // TESTBLOCK 10: BEREINIGUNG IN DER ARBEITSLISTE
            // =================================

            /*
             * Reine Modelltests. Keine Dateien
             * und keine Bereinigung ausgeführt.
             */
            RunTestBlock(
                "TESTBLOCK 10 - BEREINIGUNGS-AUFGABENLISTE",
                CleanupTaskWorkListRegressionTests
                    .RunAll);

            // =================================
            // TESTBLOCK 11: BEREINIGUNGS-VERLAUFSSCHUTZ
            // =================================

            RunTestBlock(
                "TESTBLOCK 11 - BEREINIGUNGS-VERLAUFSSCHUTZ",
                CleanupCategoryExecutionHistoryGuardRegressionTests
                    .RunAll);

            // =================================
            // TESTBLOCK 12: TECHNISCHE STATUSANZEIGE
            // =================================

            /*
             * Reine Modelltests; keine
             * Löschoperationen oder Dateizugriffe.
             */
            RunTestBlock(
                "TESTBLOCK 12 - TECHNISCHE STATUSANZEIGE",
                CleanupTaskActionStatusTextRegressionTests
                    .RunAll);

            // =================================
            // TESTBLOCK 13: BEREINIGUNGSFORTSCHRITT
            // =================================

            RunTestBlock(
                "TESTBLOCK 13 - BEREINIGUNGSFORTSCHRITT",
                CleanupCategoryProgressRegressionTests
                    .RunAll);

            // =================================
            // TESTBLOCK 14: BEREINIGUNGS-ARBEITSABSCHLUSS
            // =================================

            RunTestBlock(
                "TESTBLOCK 14 - CLEANUP-ARBEITSABSCHLUSS",
                CleanupWorkCompletionRegressionTests.RunAll);

            // =================================
            // GESAMTERGEBNIS
            // =================================

            Console.WriteLine(
                "========================================");

            Console.WriteLine(
                " ERGEBNIS: ALLE 103 TESTS BESTANDEN");

            Console.WriteLine(
                "========================================");

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine();

            Console.Error.WriteLine(
                "========================================");

            Console.Error.WriteLine(
                " ERGEBNIS: TEST FEHLGESCHLAGEN");

            Console.Error.WriteLine(
                "========================================");

            Console.WriteLine();

            Console.WriteLine(
                exception.ToString());

            return 1;
        }
#else
        Console.Error.WriteLine(
            "Dieser TestRunner ist ausschließlich "
            + "im Debug-Modus verfügbar.");

        return 2;
#endif
    }

#if DEBUG

    // =============================================
    // TESTBLOCK AUSFÜHREN
    // =============================================

    private static void RunTestBlock(
        string title,
        Func<string> runTests)
    {
        PrintHeading(title);

        Console.WriteLine(
            runTests());

        Console.WriteLine();
    }

    // =============================================
    // KONSOLENAUSGABE
    // =============================================

    private static void PrintHeading(
        string heading)
    {
        Console.WriteLine();

        Console.WriteLine(
            "----------------------------------------");

        Console.WriteLine(" " + heading);

        Console.WriteLine(
            "----------------------------------------");

        Console.WriteLine();
    }

#endif
}

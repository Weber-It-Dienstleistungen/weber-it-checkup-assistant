
using System;
using System.Security.Principal;
using WeberIT.Checkup.App.Services.Tasks;

namespace WeberIT.Checkup.SandboxTestRunner;

/// <summary>
/// Sprint 55.14.28i
///
/// Eigenständiger C#-Sandbox-TestRunner.
///
/// 51 aktive Sicherheits- und Regressionstests.
///
/// Die vier veralteten PowerShell-Sandbox-
/// Testblöcke werden nicht mehr ausgeführt.
///
/// Sicherheitsregeln:
/// - Keine Administratorrechte.
/// - Kein Zugriff auf produktives Windows-Temp.
/// - Reale Löschungen nur in GUID-Sandboxen.
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
            // GESAMTERGEBNIS
            // =================================

            Console.WriteLine(
                "========================================");

            Console.WriteLine(
                " ERGEBNIS: ALLE 51 TESTS BESTANDEN");

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

            Console.Error.WriteLine();

            Console.Error.WriteLine(
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

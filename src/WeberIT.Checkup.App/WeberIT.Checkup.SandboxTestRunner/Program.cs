
using System;
using System.Security.Principal;
using WeberIT.Checkup.App.Services.Tasks;

namespace WeberIT.Checkup.SandboxTestRunner;

/// <summary>
/// Eigenständiger Teststarter für kontrollierte
/// Sicherheitstests des Checkup-Assistenten.
///
/// Sprint 55.13.40
///
/// Keine Administratorrechte.
/// Keine Änderung am produktiven Manifest.
/// Keine eigene Bereinigungslogik.
///
/// Die Tests stammen aus der referenzierten
/// Checkup-Anwendung.
///
/// Testreihenfolge:
/// 1. Kontrollierter Abbruch
/// 2. Isolierter Löschablauf
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
            " WEBER IT - SANDBOX TESTRUNNER");

        Console.WriteLine(
            "========================================");

        Console.WriteLine();

        // =====================================
        // BETRIEBSSYSTEM PRÜFEN
        // =====================================

        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine(
                "FEHLER: Windows wird benötigt.");

            return 2;
        }

        // =====================================
        // ADMINISTRATORRECHTE VERBIETEN
        // =====================================

        using var identity =
            WindowsIdentity.GetCurrent();

        var principal =
            new WindowsPrincipal(identity);

        if (principal.IsInRole(
                WindowsBuiltInRole.Administrator))
        {
            Console.Error.WriteLine(
                "SICHERHEITSSTOPP:");

            Console.Error.WriteLine(
                "Der Teststarter darf nicht mit "
                + "Administratorrechten laufen.");

            return 2;
        }

        Console.WriteLine(
            "Berechtigungsprüfung: OK");

        Console.WriteLine(
            "Ausführung ohne Administratorrechte.");

        Console.WriteLine();

        // =====================================
        // TESTS AUSFÜHREN
        // =====================================

        try
        {
            // =================================
            // TESTBLOCK 1: ABBRUCH
            // =================================

            Console.WriteLine(
                "----------------------------------------");

            Console.WriteLine(
                " TESTBLOCK 1 - ABBRUCHTEST");

            Console.WriteLine(
                "----------------------------------------");

            Console.WriteLine();

            var cancellationResult =
                WindowsTemporaryFilesCleanupSandboxCancellationRegressionTests
                    .RunAll();

            Console.WriteLine(
                cancellationResult);

            Console.WriteLine();

            Console.WriteLine(
                "Abbruchtests: BESTANDEN");

            Console.WriteLine();

            // =================================
            // TESTBLOCK 2: LÖSCHABLAUF
            // =================================

            Console.WriteLine(
                "----------------------------------------");

            Console.WriteLine(
                " TESTBLOCK 2 - KONTROLLIERTE LÖSCHUNG");

            Console.WriteLine(
                "----------------------------------------");

            Console.WriteLine();

            Console.WriteLine(
                "Es werden ausschließlich selbst "
                + "erstellte Sandbox-Testdaten verwendet.");

            Console.WriteLine();

            var deletionResult =
                WindowsTemporaryFilesCleanupSandboxDeletionRegressionTests
                    .RunAll();

            Console.WriteLine(
                deletionResult);

            Console.WriteLine();

            Console.WriteLine(
                "Löschtests: BESTANDEN");

            Console.WriteLine();

            // =================================
            // GESAMTERGEBNIS
            // =================================

            Console.WriteLine(
                "========================================");

            Console.WriteLine(
                " ERGEBNIS: ALLE 11 TESTS BESTANDEN");

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
            "Dieser Teststarter darf ausschließlich "
            + "im Debug-Modus ausgeführt werden.");

        return 2;
#endif
    }
}

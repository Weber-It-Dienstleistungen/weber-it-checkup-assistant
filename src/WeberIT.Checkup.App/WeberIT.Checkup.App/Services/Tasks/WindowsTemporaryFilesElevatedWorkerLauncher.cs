
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.26d
///
/// Startet die eigene Anwendung mit einem
/// festen Worker-Argument über Windows-UAC.
///
/// Ausschließlich für die noch deaktivierte
/// Windows-Temp-Worker-Vorprüfung.
///
/// Keine PowerShell.
/// Keine frei definierten Befehle.
/// Keine frei übergebenen Zielpfade.
/// Keine Ergebnisdateien.
/// Keine produktive Bereinigung.
///
/// WICHTIG:
/// Die Integrität des ausführbaren
/// Programmpakets muss vor einer späteren
/// produktiven Freigabe separat abgesichert
/// werden.
/// </summary>
internal static class WindowsTemporaryFilesElevatedWorkerLauncher
{
    private const int OperationCancelledErrorCode = 1223;

    // =============================================
    // WORKER-VORPRÜFUNG STARTEN
    // =============================================

    internal static async Task<ProcessExecutionResult>
        RunPreflightAsync(
            CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.Now;

        if (cancellationToken.IsCancellationRequested)
        {
            return CreateFailure(
                startedAt,
                "Der Workerstart wurde vor "
                + "der UAC-Anforderung abgebrochen.");
        }

        if (!OperatingSystem.IsWindows())
        {
            return CreateFailure(
                startedAt,
                "Der erhöhte Worker kann nur "
                + "unter Windows gestartet werden.");
        }

        string executablePath;

        try
        {
            executablePath =
                ResolveCurrentExecutablePath();
        }
        catch (Exception exception)
        {
            return CreateFailure(
                startedAt,
                "Die ausführbare Worker-Datei "
                + "konnte nicht bestimmt werden: "
                + exception.Message);
        }

        /*
         * Der Launcher startet ausschließlich
         * die aktuelle ausführbare Anwendung.
         *
         * Es werden keine von außen
         * gelieferten Programmpfade oder
         * Argumente akzeptiert.
         */
        var startInfo =
            new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,

                /*
                 * Kein benutzerdefiniertes
                 * Arbeitsverzeichnis verwenden.
                 */
                WorkingDirectory =
                    Environment.SystemDirectory
            };

        startInfo.ArgumentList.Add(
            WindowsTemporaryFilesCleanupWorkerEntryPoint
                .WorkerArgument);

        var processStarted = false;

        try
        {
            using var process =
                new Process
                {
                    StartInfo = startInfo
                };

            /*
             * Noch vor der UAC-Anforderung
             * auf einen möglichen Abbruch
             * reagieren.
             */
            if (cancellationToken.IsCancellationRequested)
            {
                return CreateFailure(
                    startedAt,
                    "Der Workerstart wurde vor "
                    + "der UAC-Anforderung abgebrochen.");
            }

            /*
             * ShellExecute mit Verb "runas"
             * veranlasst Windows zur
             * Rechteerhöhung.
             *
             * Eine abgelehnte UAC-Anforderung
             * wird gesondert behandelt.
             */
            if (!process.Start())
            {
                return CreateFailure(
                    startedAt,
                    "Der erhöhte Worker konnte "
                    + "nicht gestartet werden.");
            }

            processStarted = true;

            /*
             * Nach dem Start wird nicht
             * versucht, einen erhöhten Prozess
             * gewaltsam zu beenden.
             *
             * Ein kooperatives Abbruchprotokoll
             * wird vor der produktiven
             * Integration separat ergänzt.
             *
             * Der derzeitige Worker besitzt
             * ausschließlich eine Vorprüfung
             * und keine Löschfreigabe.
             */
            await process.WaitForExitAsync();

            var exitCode = process.ExitCode;

            return new ProcessExecutionResult
            {
                WasStarted = true,

                /*
                 * Ein bestätigter Worker-Status
                 * ist aussagekräftiger als die
                 * bloße UAC-Anforderung.
                 *
                 * Die endgültige Vertrauens-
                 * prüfung folgt später.
                 */
                WasElevated =
                    exitCode ==
                        WindowsTemporaryFilesCleanupWorkerEntryPoint
                            .WorkerDisabledExitCode
                    || exitCode ==
                        WindowsTemporaryFilesCleanupWorkerEntryPoint
                            .TargetRejectedExitCode,

                ExitCode = exitCode,

                ErrorMessage =
                    DescribeWorkerExitCode(exitCode),

                StartedAt = startedAt,
                FinishedAt = DateTimeOffset.Now
            };
        }
        catch (Win32Exception exception)
            when (exception.NativeErrorCode
                  == OperationCancelledErrorCode)
        {
            return new ProcessExecutionResult
            {
                WasStarted = false,
                WasElevated = false,
                ElevationWasCancelled = true,

                ErrorMessage =
                    "Die Windows-UAC-Anforderung "
                    + "wurde abgebrochen.",

                StartedAt = startedAt,
                FinishedAt = DateTimeOffset.Now
            };
        }
        catch (Exception exception)
        {
            return new ProcessExecutionResult
            {
                WasStarted = processStarted,
                WasElevated = false,

                ErrorMessage =
                    "Die erhöhte Worker-Ausführung "
                    + "ist fehlgeschlagen: "
                    + exception.Message,

                StartedAt = startedAt,
                FinishedAt = DateTimeOffset.Now
            };
        }
    }

    // =============================================
    // EIGENE EXE ERMITTELN
    // =============================================

    private static string ResolveCurrentExecutablePath()
    {
        var executablePath =
            Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(
                executablePath))
        {
            throw new InvalidOperationException(
                "Der aktuelle Programmpfad "
                + "ist nicht verfügbar.");
        }

        if (!Path.IsPathFullyQualified(
                executablePath)
            || IsNetworkOrDevicePath(
                executablePath))
        {
            throw new InvalidOperationException(
                "Der Worker darf ausschließlich "
                + "über eine lokale, vollständig "
                + "qualifizierte EXE gestartet werden.");
        }

        var normalizedPath =
            Path.GetFullPath(executablePath);

        if (IsNetworkOrDevicePath(normalizedPath))
        {
            throw new InvalidOperationException(
                "Netzwerk- und Gerätepfade "
                + "sind als Worker-Startpfad "
                + "nicht zulässig.");
        }

        if (!string.Equals(
                Path.GetExtension(normalizedPath),
                ".exe",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Der Worker benötigt eine "
                + "ausführbare Windows-EXE.");
        }

        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException(
                "Die aktuelle Programmdatei "
                + "wurde nicht gefunden.",
                normalizedPath);
        }

        /*
         * Offensichtliche Verknüpfungen
         * werden hier abgelehnt.
         *
         * Dies ist ausdrücklich noch
         * keine vollständige Integritäts-
         * oder Vertrauensprüfung.
         */
        var attributes =
            File.GetAttributes(normalizedPath);

        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "Die ausführbare Worker-Datei "
                + "darf kein Reparse Point sein.");
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new InvalidOperationException(
                "Der Worker-Startpfad "
                + "verweist auf ein Verzeichnis.");
        }

        return normalizedPath;
    }

    // =============================================
    // ERGEBNISSTATUS AUSWERTEN
    // =============================================

    private static string DescribeWorkerExitCode(
        int exitCode)
    {
        /*
         * Code 44 ist aktuell das erwartete
         * Ergebnis einer erfolgreichen
         * Vorprüfung.
         *
         * Er bedeutet ausdrücklich NICHT,
         * dass eine Bereinigung erfolgt ist.
         */
        return exitCode switch
        {
            WindowsTemporaryFilesCleanupWorkerEntryPoint
                .WorkerDisabledExitCode =>
                    "Der Worker hat seine Vorprüfung "
                    + "abgeschlossen. Die produktive "
                    + "Bereinigung ist gesperrt.",

            WindowsTemporaryFilesCleanupWorkerEntryPoint
                .InvalidArgumentsExitCode =>
                    "Der Worker hat unzulässige "
                    + "Startargumente erkannt.",

            WindowsTemporaryFilesCleanupWorkerEntryPoint
                .UnsupportedPlatformExitCode =>
                    "Der Worker wurde auf einer "
                    + "nicht unterstützten "
                    + "Plattform gestartet.",

            WindowsTemporaryFilesCleanupWorkerEntryPoint
                .NotElevatedExitCode =>
                    "Der Worker besitzt keine "
                    + "bestätigten Administratorrechte.",

            WindowsTemporaryFilesCleanupWorkerEntryPoint
                .TargetRejectedExitCode =>
                    "Der Worker hat das Windows-Temp-"
                    + "Ziel aus Sicherheitsgründen "
                    + "abgelehnt.",

            _ =>
                "Der Worker wurde mit einem "
                + "unerwarteten Exitcode beendet: "
                + exitCode
        };
    }

    // =============================================
    // FEHLERERGEBNIS ERSTELLEN
    // =============================================

    private static ProcessExecutionResult CreateFailure(
        DateTimeOffset startedAt,
        string errorMessage)
    {
        return new ProcessExecutionResult
        {
            WasStarted = false,
            WasElevated = false,
            ErrorMessage = errorMessage,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.Now
        };
    }

    // =============================================
    // PFADKLASSIFIZIERUNG
    // =============================================

    private static bool IsNetworkOrDevicePath(
        string path)
    {
        return path.StartsWith(
                   @"\\",
                   StringComparison.Ordinal)
               || path.StartsWith(
                   "//",
                   StringComparison.Ordinal);
    }
}

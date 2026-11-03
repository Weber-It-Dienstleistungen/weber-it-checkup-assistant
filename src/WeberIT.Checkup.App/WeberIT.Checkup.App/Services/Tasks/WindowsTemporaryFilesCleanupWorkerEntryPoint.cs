
using System;
using System.Collections.Generic;
using System.Security.Principal;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.26b
///
/// Bereitet den eingeschränkten Einstieg
/// für den erhöhten Windows-Temp-Worker vor.
///
/// Aktuell ausschließlich Vorprüfung.
///
/// Keine Dateien löschen.
/// Keine Verzeichnisse löschen.
/// Keine Ergebnisdateien schreiben.
/// Keine externen Prozesse starten.
/// Keine Berechtigungen verändern.
///
/// Die produktive Bereinigung bleibt
/// ausdrücklich deaktiviert.
/// </summary>
internal static class WindowsTemporaryFilesCleanupWorkerEntryPoint
{
    // =============================================
    // FESTER WORKER-MODUS
    // =============================================

    internal const string WorkerArgument =
        "--weberit-windows-temp-worker";

    // =============================================
    // PROZESS-EXITCODES
    // =============================================

    internal const int InvalidArgumentsExitCode = 40;

    internal const int UnsupportedPlatformExitCode = 41;

    internal const int NotElevatedExitCode = 42;

    internal const int TargetRejectedExitCode = 43;

    internal const int WorkerDisabledExitCode = 44;

    // =============================================
    // WORKER-AUFRUF ERKENNEN
    // =============================================

    /// <summary>
    /// Prüft, ob die Anwendung im reservierten
    /// Worker-Modus aufgerufen wurde.
    ///
    /// Rückgabe false:
    /// Kein Worker-Aufruf erkannt.
    /// Die normale Anwendung darf starten.
    ///
    /// Rückgabe true:
    /// Worker-Aufruf erkannt.
    /// Die normale Anwendung darf nicht starten.
    ///
    /// exitCode enthält dann den technischen
    /// Beendigungsstatus.
    /// </summary>
    internal static bool TryHandle(
        IReadOnlyList<string> arguments,
        out int exitCode)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        exitCode = 0;

        var workerModeRequested = false;

        foreach (var argument in arguments)
        {
            if (argument is not null
                && argument.StartsWith(
                    WorkerArgument,
                    StringComparison.OrdinalIgnoreCase))
            {
                workerModeRequested = true;
                break;
            }
        }

        if (!workerModeRequested)
        {
            return false;
        }

        /*
         * Nur genau ein fest definiertes
         * Argument ist erlaubt.
         *
         * Keine Zielpfade.
         * Keine freien Befehle.
         * Keine zusätzlichen Optionen.
         */
        if (arguments.Count != 1
            || !string.Equals(
                arguments[0],
                WorkerArgument,
                StringComparison.Ordinal))
        {
            exitCode = InvalidArgumentsExitCode;
            return true;
        }

        exitCode = RunReadOnlyPreflight();

        return true;
    }

    // =============================================
    // SICHERE VORPRÜFUNG
    // =============================================

    private static int RunReadOnlyPreflight()
    {
        if (!OperatingSystem.IsWindows())
        {
            return UnsupportedPlatformExitCode;
        }

        /*
         * Der Worker fordert selbst keine
         * Administratorrechte an.
         *
         * Die Erhöhung muss später durch
         * den kontrollierenden Executor
         * über Windows-UAC erfolgen.
         */
        try
        {
            using var identity =
                WindowsIdentity.GetCurrent();

            var principal =
                new WindowsPrincipal(identity);

            if (!principal.IsInRole(
                    WindowsBuiltInRole.Administrator))
            {
                return NotElevatedExitCode;
            }
        }
        catch
        {
            /*
             * Wenn der administrative Zustand
             * nicht zuverlässig ermittelt werden
             * kann, erfolgt keine Ausführung.
             */
            return NotElevatedExitCode;
        }

        /*
         * Das Ziel wird ausschließlich durch
         * unsere bestehende Autorisierungsklasse
         * bestimmt.
         *
         * Der Worker erhält keinen Zielpfad
         * aus der Kommandozeile.
         */
        try
        {
            using var authorizedTarget =
                WindowsTemporaryFilesAuthorizedTarget
                    .Open();

            if (authorizedTarget.RootHandle.IsInvalid
                || authorizedTarget.RootHandle.IsClosed)
            {
                return TargetRejectedExitCode;
            }

            /*
             * Hier wird bewusst noch keine
             * Cleanup-Engine aufgerufen.
             *
             * Der Handle wird beim Verlassen
             * dieses Bereichs geschlossen.
             */
        }
        catch
        {
            /*
             * Jede fehlgeschlagene Autorisierung
             * führt zu einem Sicherheitsstopp.
             *
             * Kein Ausweichpfad.
             * Kein automatischer Wiederholungsversuch.
             */
            return TargetRejectedExitCode;
        }

        /*
         * Vorprüfung erfolgreich.
         *
         * Trotzdem bleibt die Ausführung
         * ausdrücklich deaktiviert.
         *
         * Ein Nicht-Erfolgsstatus verhindert,
         * dass dieser Entwicklungsstand
         * irrtümlich als abgeschlossene
         * Bereinigung gewertet wird.
         */
        return WorkerDisabledExitCode;
    }
}

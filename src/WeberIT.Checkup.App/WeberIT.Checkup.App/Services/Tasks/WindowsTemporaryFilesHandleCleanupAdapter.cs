
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.26f
///
/// Verbindet die bestehende handle-basierte
/// Windows-Temp-Engine mit dem persistierten
/// Bereinigungsergebnis.
///
/// Sicherheitsregeln:
/// - Ausschließlich Windows-Temp.
/// - Keine frei übergebenen Zielpfade.
/// - Administratorrechte erforderlich.
/// - Stammhandle nur über die geprüfte
///   Autorisierungsklasse beziehen.
/// - Keine Berechtigungen verändern.
/// - Keine PowerShell-Ausführung.
/// - Keine externen Prozesse.
/// - Abbruchtoken an die Engine übergeben.
/// - Teilergebnisse unverändert erhalten.
///
/// WICHTIG:
/// Der aufrufende Executor muss zuvor den
/// vollständigen Aktionsplan validieren
/// und die Ausführung exklusiv sperren.
///
/// Diese Klasse wird zunächst noch nicht
/// produktiv aufgerufen.
/// </summary>
internal static class WindowsTemporaryFilesHandleCleanupAdapter
{
    // =============================================
    // BEREINIGUNG AUSFÜHREN
    // =============================================

    internal static CleanupActionCategoryExecutionResult
        Execute(
            CleanupActionCategory approvedCategory,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            approvedCategory);

        /*
         * Eine ungültige Kategorie darf
         * niemals eine Bereinigung starten.
         *
         * Die vollständige Planprüfung
         * erfolgt zusätzlich im bestehenden
         * WindowsTemporaryFilesCleanupExecutor.
         */
        approvedCategory.Validate();

        if (approvedCategory.Category
            != CleanupCategoryType.WindowsTemporaryFiles)
        {
            throw new InvalidOperationException(
                "Der Handle-Adapter darf ausschließlich "
                + "die Windows-Temp-Kategorie bearbeiten.");
        }

        var categoryTitle =
            approvedCategory.Title;

        // =========================================
        // ABBRUCH VOR BEGINN
        // =========================================

        if (cancellationToken.IsCancellationRequested)
        {
            return CreateCancelledResult(
                categoryTitle);
        }

        // =========================================
        // PLATTFORM PRÜFEN
        // =========================================

        if (!OperatingSystem.IsWindows())
        {
            return CreateFailureResult(
                categoryTitle,
                "Die Windows-Temp-Bereinigung "
                + "kann ausschließlich unter "
                + "Windows ausgeführt werden.");
        }

        // =========================================
        // ADMINISTRATORRECHTE PRÜFEN
        // =========================================

        /*
         * Das Manifest fordert derzeit
         * grundsätzlich Administratorrechte.
         *
         * Darauf allein verlassen wir uns
         * allerdings nicht.
         *
         * Die tatsächlichen Rechte des
         * laufenden Prozesses werden
         * ausdrücklich kontrolliert.
         */
        if (!HasAdministratorRights())
        {
            return CreateFailureResult(
                categoryTitle,
                "Die Windows-Temp-Bereinigung "
                + "benötigt bestätigte "
                + "Administratorrechte.");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return CreateCancelledResult(
                categoryTitle);
        }

        // =========================================
        // AUTORISIERTES ZIEL ÖFFNEN
        // =========================================

        try
        {
            /*
             * Die Autorisierungsklasse
             * bestimmt den erlaubten
             * Windows-Temp-Zielpfad selbst.
             *
             * Es werden keine fremden
             * Zielpfade übernommen.
             *
             * Das geöffnete Stammhandle
             * bleibt während der gesamten
             * Engine-Ausführung bestehen.
             */
            using var authorizedTarget =
                WindowsTemporaryFilesAuthorizedTarget
                    .Open();

            /*
             * Nochmals Abbruch prüfen,
             * bevor die Engine beginnt.
             */
            if (cancellationToken.IsCancellationRequested)
            {
                return CreateCancelledResult(
                    categoryTitle);
            }

            // =====================================
            // HANDLE-ENGINE AUFRUFEN
            // =====================================

            /*
             * Die Engine erhält ausschließlich
             * den bereits autorisierten
             * Stammhandle.
             *
             * Kein erneutes Öffnen des
             * Stammordners anhand eines
             * vom Benutzer gelieferten Pfads.
             *
             * Der Stammordner selbst wird
             * durch die Engine nicht gelöscht.
             */
            var engineResult =
                WindowsTemporaryFilesHandleCleanupEngine
                    .Execute(
                        authorizedTarget.RootHandle,
                        cancellationToken);

            // =====================================
            // ERGEBNIS ÜBERNEHMEN
            // =====================================

            return CreateCategoryResult(
                categoryTitle,
                authorizedTarget.TargetPath,
                engineResult);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return CreateCancelledResult(
                categoryTitle);
        }
        catch (Exception exception)
        {
            /*
             * Fehler während der Autorisierung
             * oder vor Beginn der Engine.
             *
             * Die Engine selbst dokumentiert
             * Fehler während einer bereits
             * gestarteten Verarbeitung als
             * Teilergebnis.
             *
             * Kein automatischer Neustart.
             * Kein alternativer Zielpfad.
             */
            return CreateFailureResult(
                categoryTitle,
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Die Windows-Temp-Bereinigung "
                      + "konnte nicht sicher "
                      + "gestartet werden."
                    : exception.Message);
        }
    }

    // =============================================
    // ADMINISTRATORRECHTE ERMITTELN
    // =============================================

    private static bool HasAdministratorRights()
    {
        try
        {
            using var identity =
                WindowsIdentity.GetCurrent();

            var principal =
                new WindowsPrincipal(identity);

            return principal.IsInRole(
                WindowsBuiltInRole.Administrator);
        }
        catch
        {
            /*
             * Wenn sich die tatsächlichen
             * Rechte nicht sicher bestimmen
             * lassen, wird die Ausführung
             * nicht freigegeben.
             */
            return false;
        }
    }

    // =============================================
    // ENGINE-ERGEBNIS ÜBERTRAGEN
    // =============================================

    private static CleanupActionCategoryExecutionResult
        CreateCategoryResult(
            string categoryTitle,
            string targetPath,
            WindowsTemporaryFilesHandleCleanupEngine
                .ExecutionResult engineResult)
    {
        ArgumentNullException.ThrowIfNull(
            engineResult);

        /*
         * Ungültige Zähler dürfen nicht
         * in das persistierte Ergebnis
         * übernommen werden.
         */
        if (engineResult.DeletedFileCount < 0
            || engineResult.DeletedDirectoryCount < 0
            || engineResult.FailedEntryCount < 0
            || engineResult.SkippedEntryCount < 0)
        {
            throw new InvalidOperationException(
                "Die Handle-Engine hat ungültige "
                + "Ergebniszähler geliefert.");
        }

        /*
         * Wenn keine Ausführung begonnen hat,
         * dürfen keine Löschungen gemeldet sein.
         */
        if (!engineResult.WasStarted
            && (engineResult.DeletedFileCount > 0
                || engineResult.DeletedDirectoryCount > 0
                || engineResult.DeletedSizeBytes > 0))
        {
            throw new InvalidOperationException(
                "Die Handle-Engine meldet "
                + "Dateiänderungen ohne "
                + "gestartete Ausführung.");
        }

        /*
         * Ausschlussgründe als eigene Liste
         * übernehmen.
         *
         * Die bestehenden Enum-Werte
         * bleiben vollständig erhalten.
         */
        var skippedEntryReasons =
            engineResult.SkippedEntryReasons
                .Select(entry =>
                    new CleanupSkippedEntryReasonCount
                    {
                        Reason = entry.Reason,
                        Count = entry.Count
                    })
                .ToList();

        long documentedSkipCount = 0;

        var knownReasons =
            new HashSet<CleanupSkippedEntryReason>();

        foreach (var entry in skippedEntryReasons)
        {
            if (!Enum.IsDefined(
                    typeof(CleanupSkippedEntryReason),
                    entry.Reason)
                || entry.Count <= 0
                || !knownReasons.Add(entry.Reason))
            {
                throw new InvalidOperationException(
                    "Die Handle-Engine hat einen "
                    + "ungültigen Überspringgrund "
                    + "geliefert.");
            }

            documentedSkipCount =
                checked(
                    documentedSkipCount
                    + entry.Count);

            if (documentedSkipCount >
                engineResult.SkippedEntryCount)
            {
                throw new InvalidOperationException(
                    "Die dokumentierten "
                    + "Überspringgründe überschreiten "
                    + "den Gesamtzähler.");
            }
        }

        var errorMessage =
            engineResult.ErrorMessage;

        /*
         * Ein nicht gestarteter und nicht
         * abgebrochener Lauf muss einen
         * eindeutigen Fehler melden.
         */
        if (!engineResult.WasStarted
            && !engineResult.WasCancelled
            && string.IsNullOrWhiteSpace(
                errorMessage))
        {
            errorMessage =
                "Die Handle-Bereinigung wurde "
                + "nicht eindeutig gestartet.";
        }

        return new CleanupActionCategoryExecutionResult
        {
            Category =
                CleanupCategoryType.WindowsTemporaryFiles,

            CategoryTitle =
                categoryTitle,

            TargetPath =
                targetPath,

            WasStarted =
                engineResult.WasStarted,

            WasCancelled =
                engineResult.WasCancelled,

            DeletedFileCount =
                engineResult.DeletedFileCount,

            DeletedDirectoryCount =
                engineResult.DeletedDirectoryCount,

            DeletedSizeBytes =
                engineResult.DeletedSizeBytes,

            FailedEntryCount =
                engineResult.FailedEntryCount,

            SkippedEntryCount =
                engineResult.SkippedEntryCount,

            SkippedEntryReasons =
                skippedEntryReasons,

            ErrorMessage =
                errorMessage
        };
    }

    // =============================================
    // ABBRUCHERGEBNIS
    // =============================================

    private static CleanupActionCategoryExecutionResult
        CreateCancelledResult(
            string categoryTitle)
    {
        return new CleanupActionCategoryExecutionResult
        {
            Category =
                CleanupCategoryType.WindowsTemporaryFiles,

            CategoryTitle =
                categoryTitle,

            WasStarted = false,
            WasCancelled = true
        };
    }

    // =============================================
    // TECHNISCHES FEHLERERGEBNIS
    // =============================================

    private static CleanupActionCategoryExecutionResult
        CreateFailureResult(
            string categoryTitle,
            string errorMessage)
    {
        return new CleanupActionCategoryExecutionResult
        {
            Category =
                CleanupCategoryType.WindowsTemporaryFiles,

            CategoryTitle =
                categoryTitle,

            WasStarted = false,
            WasCancelled = false,

            ErrorMessage =
                errorMessage
        };
    }
}

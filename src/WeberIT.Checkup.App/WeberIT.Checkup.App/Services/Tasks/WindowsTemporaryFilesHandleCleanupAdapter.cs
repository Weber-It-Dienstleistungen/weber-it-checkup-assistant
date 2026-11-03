
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.29a
///
/// Verbindet die handle-basierte Windows-Temp-
/// Engine mit dem persistierten Ergebnis.
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
/// - Bereits erfolgte Änderungen dokumentieren.
///
/// WICHTIG:
/// Der Executor muss den Aktionsplan zuvor
/// validieren und die Ausführung exklusiv
/// sperren.
///
/// Die produktive Freigabe bleibt geschlossen.
/// </summary>
internal static class
    WindowsTemporaryFilesHandleCleanupAdapter
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
             * Der Zielpfad wird ausschließlich
             * durch die Autorisierungsklasse
             * ermittelt.
             *
             * Keine fremden Zielpfade.
             * Keine erneute Pfadauflösung
             * während der Bereinigung.
             */
            using var authorizedTarget =
                WindowsTemporaryFilesAuthorizedTarget
                    .Open();

            if (cancellationToken.IsCancellationRequested)
            {
                return CreateCancelledResult(
                    categoryTitle);
            }

            // =====================================
            // HANDLE-ENGINE AUFRUFEN
            // =====================================

            /*
             * Die Engine hält den Stammhandle
             * während des gesamten Laufs offen.
             *
             * Sie dokumentiert bereits
             * abgeschlossene Änderungen
             * in ihrem ExecutionResult.
             */
            var engineResult =
                WindowsTemporaryFilesHandleCleanupEngine
                    .Execute(
                        authorizedTarget.RootHandle,
                        cancellationToken);

            // =====================================
            // ERGEBNIS SICHER ÜBERNEHMEN
            // =====================================

            /*
             * Die strenge Ergebnisvalidierung
             * bleibt unverändert erhalten.
             *
             * Ein Konvertierungsfehler nach
             * dem Engine-Lauf darf aber nicht
             * sämtliche Löschzähler verlieren.
             *
             * Deshalb wird ausschließlich
             * die Ergebnisumwandlung separat
             * abgesichert.
             */
            try
            {
                return CreateCategoryResult(
                    categoryTitle,
                    authorizedTarget.TargetPath,
                    engineResult);
            }
            catch (Exception conversionException)
            {
                /*
                 * Der Engine-Lauf ist bereits
                 * beendet.
                 *
                 * Wir dürfen seine gemeldeten
                 * Änderungen nicht stillschweigend
                 * auf null zurücksetzen.
                 *
                 * Das Ersatzresultat ist immer
                 * als fehlerhaft gekennzeichnet.
                 *
                 * Ungültige Detailursachen
                 * werden nicht übernommen.
                 */
                return CreateConversionFailureResult(
                    categoryTitle,
                    authorizedTarget.TargetPath,
                    engineResult,
                    conversionException);
            }
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
             * Fehler vor einem verfügbaren
             * Engine-Ergebnis.
             *
             * In diesem Fall liegen keine
             * bestätigten Ergebniszähler vor.
             *
             * Keine automatische Wiederholung.
             * Kein alternativer Zielpfad.
             */
            return CreateFailureResult(
                categoryTitle,
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Die Windows-Temp-Bereinigung "
                      + "konnte nicht sicher "
                      + "ausgeführt werden."
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
             * Unklare Berechtigungen dürfen
             * keine Ausführung ermöglichen.
             */
            return false;
        }
    }

    // =============================================
    // ENGINE-ERGEBNIS VALIDIEREN
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

        if (engineResult.DeletedFileCount < 0
            || engineResult.DeletedDirectoryCount < 0
            || engineResult.FailedEntryCount < 0
            || engineResult.SkippedEntryCount < 0)
        {
            throw new InvalidOperationException(
                "Die Handle-Engine hat ungültige "
                + "Ergebniszähler geliefert.");
        }

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
    // TEILERGEBNIS BEI ÜBERTRAGUNGSFEHLER
    // =============================================

    private static CleanupActionCategoryExecutionResult
        CreateConversionFailureResult(
            string categoryTitle,
            string targetPath,
            WindowsTemporaryFilesHandleCleanupEngine
                .ExecutionResult engineResult,
            Exception conversionException)
    {
        /*
         * Hier werden keine weiteren
         * Dateisystemoperationen ausgeführt.
         *
         * Positive Zählerwerte bleiben
         * als Engine-Meldungen erhalten.
         *
         * Negative Zähler sind ungültig
         * und werden nicht persistiert.
         *
         * Es werden keine neuen Löschungen
         * behauptet und keine Löschversuche
         * wiederholt.
         */
        var deletedFileCount =
            Math.Max(
                0L,
                engineResult.DeletedFileCount);

        var deletedDirectoryCount =
            Math.Max(
                0L,
                engineResult.DeletedDirectoryCount);

        var failedEntryCount =
            Math.Max(
                0L,
                engineResult.FailedEntryCount);

        var skippedEntryCount =
            Math.Max(
                0L,
                engineResult.SkippedEntryCount);

        /*
         * Die Detailursachen werden bewusst
         * nicht übernommen:
         *
         * Genau ihre Validierung könnte
         * zuvor fehlgeschlagen sein.
         *
         * Der Gesamtzähler bleibt erhalten,
         * damit die Anzeige ausdrücklich
         * auf nicht aufgeschlüsselte
         * Ausschlüsse hinweisen kann.
         */
        var errorMessage =
            "Die Ergebnisübernahme der "
            + "Windows-Temp-Bereinigung ist "
            + "fehlgeschlagen. Die von der "
            + "Engine gemeldeten Löschzahlen "
            + "wurden als vorläufiges "
            + "Teilergebnis erhalten. "
            + "Die Ausschlussursachen konnten "
            + "nicht vollständig geprüft werden.";

        if (!string.IsNullOrWhiteSpace(
                engineResult.ErrorMessage))
        {
            errorMessage +=
                " Engine-Fehler: "
                + engineResult.ErrorMessage;
        }

        if (!string.IsNullOrWhiteSpace(
                conversionException.Message))
        {
            errorMessage +=
                " Übertragungsfehler: "
                + conversionException.Message;
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
                deletedFileCount,

            DeletedDirectoryCount =
                deletedDirectoryCount,

            DeletedSizeBytes =
                engineResult.DeletedSizeBytes,

            FailedEntryCount =
                failedEntryCount,

            SkippedEntryCount =
                skippedEntryCount,

            SkippedEntryReasons =
                new List<CleanupSkippedEntryReasonCount>(),

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

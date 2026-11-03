
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.33a
///
/// Kontrollierte Windows-Temp-Bereinigung
/// mit direkter C#-Handle-Integration.
///
/// Ausführung:
/// - Aktionsplan vollständig validieren.
/// - Exklusive Systemaktionssperre erhalten.
/// - Produktive Freigabe kontrollieren.
/// - Handle-Adapter außerhalb des UI-Threads
///   ausführen.
/// - Ergebnisse vollständig übernehmen.
/// - Fehler und Abbrüche dokumentieren.
///
/// Keine PowerShell-Ausführung.
/// Kein separater Worker.
/// Keine temporären Skripte.
/// Keine frei übergebenen Zielpfade.
/// Kein automatischer Wiederanlauf.
///
/// WICHTIG:
/// Die allgemeine Produktivfreigabe bleibt
/// ausdrücklich deaktiviert.
///
/// Für einen beaufsichtigten Echt-Test
/// existiert eine zusätzliche Freigabe,
/// die ausschließlich bei einem Debug-Build
/// mit aktiv verbundenem Debugger und
/// laufender WPF-Anwendung greift.
/// </summary>
public sealed class WindowsTemporaryFilesCleanupExecutor
{
    // =============================================
    // ZULÄSSIGE AKTIONSKENNUNGEN
    // =============================================

    private const string RestoreCapacityTaskCode =
        "task.storage.restore-system-volume-capacity";

    private const string ControlledCleanupTaskCode =
        "task.storage.controlled-cleanup";

    private const string SupportedActionCode =
        "action.cleanup.selected-safe-categories";

    // =============================================
    // PRODUKTIVE FREIGABESPERRE
    // =============================================

    /*
     * Diese allgemeine Freigabesperre bleibt
     * ausdrücklich geschlossen.
     *
     * Sie darf erst nach abgeschlossener
     * Produktivbewertung bewusst geändert
     * werden.
     *
     * Unsere bestehenden Regressionstests
     * überwachen weiterhin diesen Wert.
     */
    private static readonly bool HandleCleanupReleaseApproved =
        false;

    // =============================================
    // BEAUFSICHTIGTER DEBUG-ECHT-TEST
    // =============================================

    /// <summary>
    /// Erlaubt einen beaufsichtigten Echt-Test
    /// ausschließlich unter eng begrenzten
    /// Entwicklungsbedingungen.
    ///
    /// Voraussetzungen:
    ///
    /// - Debug-Build.
    /// - Aktiver Debugger.
    /// - Tatsächliche WPF-Anwendung.
    ///
    /// Der SandboxTestRunner besitzt keine
    /// laufende WPF-Anwendung und erhält
    /// dadurch keine Ausführungsfreigabe.
    ///
    /// In Release-Builds gibt diese Methode
    /// grundsätzlich false zurück.
    ///
    /// Diese Testfreigabe ersetzt ausdrücklich
    /// keine allgemeine Produktivfreigabe.
    /// </summary>
    private static bool IsAttendedDebugTrialAllowed()
    {
#if DEBUG

        return
            System.Diagnostics.Debugger.IsAttached
            && System.Windows.Application.Current
                is global::WeberIT.Checkup.App.App;

#else

        return false;

#endif
    }

    // =============================================
    // AUSFÜHRUNGSKOORDINATION
    // =============================================

    private readonly ICheckupTaskActionExecutionCoordinator
        _executionCoordinator;

    // =============================================
    // KONSTRUKTOR
    // =============================================

    public WindowsTemporaryFilesCleanupExecutor(
        IMaintenanceProcessRunner processRunner,
        ICheckupTaskActionExecutionCoordinator
            executionCoordinator)
    {
        ArgumentNullException.ThrowIfNull(
            processRunner);

        ArgumentNullException.ThrowIfNull(
            executionCoordinator);

        /*
         * Der Prozessrunner bleibt vorerst
         * Bestandteil der öffentlichen
         * Konstruktorsignatur.
         *
         * Er wird nicht mehr ausgeführt.
         *
         * Dadurch bleiben bestehende
         * DI-Registrierungen und Tests
         * unverändert kompatibel.
         */
        _executionCoordinator =
            executionCoordinator;
    }

    // =============================================
    // KONTROLLIERTE AUSFÜHRUNG
    // =============================================

    public async Task<CleanupActionExecutionResult>
        ExecuteAsync(
            CheckupTaskActionPlan plan,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        /*
         * Vor jeder weiteren Verarbeitung
         * den fachlichen Plan überprüfen.
         *
         * Insbesondere:
         * - Zulässiger TaskCode.
         * - Zulässiger ActionCode.
         * - Genau eine Windows-Temp-Kategorie.
         * - Keine externen Befehle.
         * - Administratorrechte angefordert.
         * - Kein automatischer Neustart.
         */
        ValidatePlan(plan);

        var startedAt =
            DateTimeOffset.Now;

        // =========================================
        // ABBRUCH VOR BEGINN
        // =========================================

        if (cancellationToken.IsCancellationRequested)
        {
            return CreateCancelledBeforeStartResult(
                plan.Id,
                startedAt);
        }

        // =========================================
        // EXKLUSIVE AUSFÜHRUNG
        // =========================================

        /*
         * Die bestehende Systemaktionssperre
         * wird auch für den neuen direkten
         * C#-Ausführungspfad verwendet.
         *
         * Der Lease bleibt während der
         * gesamten Verarbeitung erhalten
         * und wird automatisch freigegeben.
         */
        using var executionLease =
            _executionCoordinator.TryBeginExecution(
                plan.ActionCode,
                plan.ActionTitle);

        if (executionLease is null)
        {
            return CreateBlockedResult(
                plan.Id,
                startedAt,
                BuildExecutionBlockedMessage());
        }

        // =========================================
        // FREIGABE PRÜFEN
        // =========================================

        /*
         * Zwei strikt getrennte Wege:
         *
         * 1. Allgemeine Produktivfreigabe.
         *    Diese bleibt geschlossen.
         *
         * 2. Beaufsichtigter Debug-Echt-Test.
         *    Nur mit aktivem Debugger und
         *    tatsächlicher WPF-Anwendung.
         *
         * Die Freigabe beeinflusst weder die
         * Zielautorisierung noch die Prüfung
         * der Administratorrechte.
         *
         * Diese erfolgen weiterhin im
         * Handle-Adapter.
         */
        var executionApproved =
            HandleCleanupReleaseApproved
            || IsAttendedDebugTrialAllowed();

        if (!executionApproved)
        {
            return CreateBlockedResult(
                plan.Id,
                startedAt,
                "Die neue Windows-Temp-Bereinigung "
                + "befindet sich noch in der "
                + "Sicherheitsprüfung und ist "
                + "für produktive Ausführungen "
                + "nicht freigegeben.");
        }

        // =========================================
        // DIREKTE HANDLE-BEREINIGUNG
        // =========================================

        try
        {
            var plannedCategory =
                plan.CleanupCategories.Single();

            /*
             * Die Dateisystemverarbeitung
             * darf den WPF-UI-Thread nicht
             * blockieren.
             *
             * Der Abbruchtoken wird an den
             * Handle-Adapter weitergereicht.
             *
             * Task.Run bekommt absichtlich
             * keinen eigenen Abbruchtoken,
             * damit auch ein kurzfristig
             * abgebrochener Start durch
             * den Adapter als fachliches
             * Abbruchergebnis dokumentiert
             * werden kann.
             */
            var categoryResult =
                await Task.Run(
                    () =>
                        WindowsTemporaryFilesHandleCleanupAdapter
                            .Execute(
                                plannedCategory,
                                cancellationToken));

            // =====================================
            // ERGEBNIS ÜBERNEHMEN
            // =====================================

            /*
             * Das Kategorieergebnis wird
             * vollständig übernommen.
             *
             * Dadurch bleiben insbesondere
             * erhalten:
             *
             * - Ausführungsstatus
             * - Abbruchstatus
             * - Zielbereich
             * - Gelöschte Dateien und Ordner
             * - Freigegebene Dateigröße
             * - Technische Fehler
             * - Übersprungene Einträge
             * - Einzelne Überspringgründe
             */
            return new CleanupActionExecutionResult
            {
                PlanId = plan.Id,

                WasCancelled =
                    categoryResult.WasCancelled,

                ErrorMessage =
                    categoryResult.WasCancelled
                        ? string.Empty
                        : categoryResult.ErrorMessage,

                StartedAt = startedAt,
                FinishedAt = DateTimeOffset.Now,

                CategoryResults =
                    new List<CleanupActionCategoryExecutionResult>
                    {
                        categoryResult
                    }
            };
        }
        catch (Exception exception)
        {
            /*
             * Ein unerwarteter Fehler wird
             * dokumentiert.
             *
             * Keine automatische Wiederholung.
             * Keine PowerShell-Ausweichlösung.
             * Keine Änderung des Zielbereichs.
             */
            return CreateTechnicalFailureResult(
                plan.Id,
                startedAt,
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Die handle-basierte "
                      + "Windows-Temp-Bereinigung "
                      + "wurde unerwartet abgebrochen."
                    : exception.Message);
        }
    }

    // =============================================
    // AKTIONSPLAN VALIDIEREN
    // =============================================

    private static void ValidatePlan(
        CheckupTaskActionPlan plan)
    {
        plan.Validate();

        if (!string.Equals(
                plan.TaskCode,
                RestoreCapacityTaskCode,
                StringComparison.Ordinal)
            && !string.Equals(
                plan.TaskCode,
                ControlledCleanupTaskCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Der Aktionsplan gehört nicht zu einer "
                + "freigegebenen Bereinigungsaufgabe.");
        }

        if (!string.Equals(
                plan.ActionCode,
                SupportedActionCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Der Aktionscode ist für die erhöhte "
                + "Windows-Temp-Bereinigung "
                + "nicht freigegeben.");
        }

        if (plan.HasCommands)
        {
            throw new InvalidOperationException(
                "Der Windows-Temp-Bereinigungsplan "
                + "darf keine frei definierten "
                + "externen Befehle enthalten.");
        }

        if (plan.CleanupCategories.Count != 1)
        {
            throw new InvalidOperationException(
                "Für die Windows-Temp-Bereinigung "
                + "muss genau eine Kategorie "
                + "ausgewählt sein.");
        }

        if (!plan.RequiresAdministrator)
        {
            throw new InvalidOperationException(
                "Der Windows-Temp-Bereinigungsplan "
                + "muss ausdrücklich "
                + "Administratorrechte anfordern.");
        }

        if (plan.MayRequireRestart)
        {
            throw new InvalidOperationException(
                "Die Windows-Temp-Bereinigung "
                + "darf keinen automatischen "
                + "Neustart vorsehen.");
        }

        var category =
            plan.CleanupCategories.Single();

        category.Validate();

        if (category.Category
            != CleanupCategoryType.WindowsTemporaryFiles)
        {
            throw new InvalidOperationException(
                "Der Bereinigungsplan enthält "
                + "keine Windows-Temp-Kategorie.");
        }
    }

    // =============================================
    // LAUFENDE SYSTEMAKTION BESCHREIBEN
    // =============================================

    private string BuildExecutionBlockedMessage()
    {
        if (!_executionCoordinator.IsExecutionRunning)
        {
            return
                "Die Windows-Temp-Bereinigung "
                + "konnte nicht exklusiv "
                + "gesperrt werden.";
        }

        if (string.IsNullOrWhiteSpace(
                _executionCoordinator.ActiveActionTitle))
        {
            return
                "Eine andere technische "
                + "Systemaktion wird bereits "
                + "ausgeführt.";
        }

        return
            "Die Windows-Temp-Bereinigung "
            + "kann noch nicht gestartet werden, "
            + "weil bereits folgende Aktion läuft: "
            + _executionCoordinator.ActiveActionTitle;
    }

    // =============================================
    // BLOCKIERTES ERGEBNIS
    // =============================================

    private static CleanupActionExecutionResult
        CreateBlockedResult(
            Guid planId,
            DateTimeOffset startedAt,
            string errorMessage)
    {
        return new CleanupActionExecutionResult
        {
            PlanId = planId,
            WasBlocked = true,
            ErrorMessage = errorMessage,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.Now
        };
    }

    // =============================================
    // ABBRUCH VOR START
    // =============================================

    private static CleanupActionExecutionResult
        CreateCancelledBeforeStartResult(
            Guid planId,
            DateTimeOffset startedAt)
    {
        return new CleanupActionExecutionResult
        {
            PlanId = planId,
            WasCancelled = true,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.Now
        };
    }

    // =============================================
    // TECHNISCHER FEHLER
    // =============================================

    private static CleanupActionExecutionResult
        CreateTechnicalFailureResult(
            Guid planId,
            DateTimeOffset startedAt,
            string errorMessage)
    {
        return new CleanupActionExecutionResult
        {
            PlanId = planId,
            ErrorMessage = errorMessage,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.Now
        };
    }
}

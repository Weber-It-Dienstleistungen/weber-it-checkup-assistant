
using System;
using System.Linq;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Cleanup;

/// <summary>
/// Sprint 55.14.34p
///
/// Sucht ein eindeutig zuordenbares,
/// strukturiertes Bereinigungsprotokoll.
///
/// Keine Dateisystemzugriffe.
/// Keine Datenbankzugriffe.
/// Keine Änderungen an Aktionsprotokollen.
/// Keine Dateilöschung.
///
/// Bei fehlender oder mehrdeutiger
/// Zuordnung wird null zurückgegeben.
/// </summary>
public static class CleanupExecutionCorrelationResolver
{
    private const string CleanupActionCode =
        "action.cleanup.selected-safe-categories";

    private const string ControlledCleanupTaskCode =
        "task.storage.controlled-cleanup";

    private const string RestoreCapacityTaskCode =
        "task.storage.restore-system-volume-capacity";

    // =============================================
    // EINDEUTIGEN NACHWEIS SUCHEN
    // =============================================

    public static CleanupExecutionCorrelationMatch?
        FindUniqueMatch(
            CheckupSession session,
            CustomerCheckupVisit visit,
            CleanupCategoryType category,
            DateTimeOffset measurementStartedAt)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(visit);

        // =========================================
        // KUNDENCHECKUP PRÜFEN
        // =========================================

        if (visit.Id == Guid.Empty
            || !visit.IsInProgress
            || visit.IsCompletionPrepared
            || !ReferenceEquals(
                session.CurrentCustomerCheckupVisit,
                visit))
        {
            return null;
        }

        if (visit.BeforeCheckup?.ScanDate
            is not DateTime entranceScanDate)
        {
            return null;
        }

        var entranceTime =
            ToDateTimeOffset(entranceScanDate);

        if (entranceTime >= measurementStartedAt)
        {
            return null;
        }

        // =========================================
        // ZEITLICHE VERGLEICHSBASIS
        // =========================================

        /*
         * Eine bereits durchgeführte
         * Zwischenprüfung bildet die neue
         * untere Zeitgrenze.
         *
         * Dadurch darf eine ältere Löschaktion
         * nicht beliebig oft erneut
         * zugeordnet werden.
         */
        var previousScans =
            visit.CleanupPotentialIntermediateScans
                .Where(
                    scan =>
                        scan.CustomerCheckupVisitId
                            == visit.Id
                        && scan.Category == category)
                .OrderByDescending(
                    scan => scan.RecordedAt)
                .ToList();

        var lowerTimeBoundary =
            entranceTime;

        if (previousScans.Count > 0)
        {
            var latestScan =
                previousScans[0];

            /*
             * Ein Zwischenstand, dessen Zeitpunkt
             * in der Zukunft der aktuellen Messung
             * liegt, macht die Reihenfolge
             * unzuverlässig.
             */
            if (latestScan.RecordedAt
                >= measurementStartedAt)
            {
                return null;
            }

            if (latestScan.RecordedAt
                > lowerTimeBoundary)
            {
                lowerTimeBoundary =
                    latestScan.RecordedAt;
            }
        }

        // =========================================
        // STRUKTURIERTE AKTIONEN SUCHEN
        // =========================================

        var tasks =
            session.TaskList?.Tasks;

        if (tasks is null)
        {
            return null;
        }

        var matches =
            tasks
                .Where(
                    task =>
                        task is not null
                        && task.Id != Guid.Empty
                        && IsCleanupTask(
                            task.TaskCode))
                .SelectMany(
                    task =>
                        task.ActionResults
                        ?? new())
                .Where(
                    action =>
                        IsMatchingExecution(
                            action,
                            visit.Id,
                            category,
                            lowerTimeBoundary,
                            measurementStartedAt))
                .Take(2)
                .ToList();

        /*
         * Kein Treffer:
         * Keine automatische Zuordnung.
         *
         * Mehrere Treffer:
         * Keine willkürliche Auswahl
         * nach Zeitpunkt oder Reihenfolge.
         *
         * Nur genau ein Treffer ist zulässig.
         */
        if (matches.Count != 1)
        {
            return null;
        }

        var matchedAction =
            matches[0];

        var execution =
            matchedAction.CleanupExecutionResult!;

        var categoryResult =
            execution.CategoryResults[0];

        return new CleanupExecutionCorrelationMatch(
            matchedAction.Id,
            execution.PlanId,
            matchedAction.StartedAt!.Value,
            matchedAction.FinishedAt!.Value,
            categoryResult);
    }

    // =============================================
    // BEREINIGUNGSAUFGABE PRÜFEN
    // =============================================

    private static bool IsCleanupTask(
        string? taskCode)
    {
        return string.Equals(
                   taskCode,
                   ControlledCleanupTaskCode,
                   StringComparison.Ordinal)
               || string.Equals(
                   taskCode,
                   RestoreCapacityTaskCode,
                   StringComparison.Ordinal);
    }

    // =============================================
    // EINZELAKTION VALIDIEREN
    // =============================================

    private static bool IsMatchingExecution(
        CheckupTaskActionResult? action,
        Guid visitId,
        CleanupCategoryType category,
        DateTimeOffset lowerTimeBoundary,
        DateTimeOffset measurementStartedAt)
    {
        if (action is null
            || action.Id == Guid.Empty
            || !action.HasCorrelatableCleanupExecution
            || action.CleanupCustomerCheckupVisitId
                != visitId)
        {
            return false;
        }

        if (!string.Equals(
                action.ActionCode,
                CleanupActionCode,
                StringComparison.Ordinal))
        {
            return false;
        }

        var execution =
            action.CleanupExecutionResult;

        if (execution is null
            || execution.CategoryResults.Count != 1
            || execution.CategoryResults[0].Category
                != category)
        {
            return false;
        }

        // =========================================
        // TECHNISCHEN STATUS BESTÄTIGEN
        // =========================================

        var expectedStatus =
            execution.IsSuccessful
            || execution.IsPartiallySuccessful
                ? CheckupTaskActionStatus.Successful
                : execution.WasCancelled
                    ? CheckupTaskActionStatus.Cancelled
                    : CheckupTaskActionStatus.Failed;

        if (action.Status != expectedStatus)
        {
            return false;
        }

        // =========================================
        // ZEITLICHE REIHENFOLGE PRÜFEN
        // =========================================

        if (!action.StartedAt.HasValue
            || !action.FinishedAt.HasValue)
        {
            return false;
        }

        var startedAt =
            action.StartedAt.Value;

        var finishedAt =
            action.FinishedAt.Value;

        /*
         * Die Aktion muss vollständig
         * innerhalb des aktuellen
         * Messintervalls liegen.
         *
         * Eine während der Messung laufende
         * oder erst danach beendete Aktion
         * darf nicht zugeordnet werden.
         */
        if (startedAt <= lowerTimeBoundary
            || finishedAt < startedAt
            || finishedAt > measurementStartedAt)
        {
            return false;
        }

        return true;
    }

    // =============================================
    // ZEITSTEMPEL NORMALISIEREN
    // =============================================

    private static DateTimeOffset ToDateTimeOffset(
        DateTime value)
    {
        /*
         * Historische Scanzeitpunkte können
         * ohne DateTimeKind gespeichert sein.
         *
         * Solche Werte werden entsprechend
         * der bisherigen lokalen
         * Windows-Zeitinterpretation
         * behandelt.
         */
        var normalized =
            value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(
                    value,
                    DateTimeKind.Local)
                : value;

        return new DateTimeOffset(
            normalized);
    }
}

/// <summary>
/// Eindeutig gefundene technische Aktion.
///
/// Die enthaltene Kategorieausführung
/// stammt aus dem strukturierten Protokoll.
///
/// Der spätere Speicherdienst muss daraus
/// eine unabhängige Kopie übernehmen.
/// </summary>
public sealed record CleanupExecutionCorrelationMatch(
    Guid ActionResultId,
    Guid PlanId,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    CleanupActionCategoryExecutionResult CategoryResult);

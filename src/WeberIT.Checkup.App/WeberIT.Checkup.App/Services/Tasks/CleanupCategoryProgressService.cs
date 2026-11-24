using System;
using System.Collections.Generic;
using System.Linq;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.42: Nur lesender Bearbeitungsfortschritt.
/// Keine neue Messung, kein Score und kein Aufgabenabschluss.
/// </summary>
public static class CleanupCategoryProgressService
{
    public static IReadOnlyList<CleanupCategoryProgressItem> Build(
        IReadOnlyList<CleanupActionCategory> categories,
        CheckupTaskList taskList,
        IReadOnlyCollection<CleanupCategoryType>? recentlyStarted = null)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(taskList);

        var unknownHistory =
            CleanupCategoryExecutionHistoryGuard
                .HasUnattributedCleanupExecution(taskList);

        var evidence =
            CleanupCategoryExecutionHistoryGuard
                .GetCleanupActions(taskList)
                .Where(a => a.CleanupExecutionResult is not null)
                .SelectMany(a =>
                    (a.CleanupExecutionResult!.CategoryResults
                     ?? new List<CleanupActionCategoryExecutionResult>())
                    .Where(c => c is not null && c.WasStarted)
                    .Select(c => (Action: a, Category: c)))
                .ToList();

        var transient =
            recentlyStarted?.ToHashSet()
            ?? new HashSet<CleanupCategoryType>();

        var output = new List<CleanupCategoryProgressItem>();

        foreach (var item in categories)
        {
            if (item?.Category is not CleanupCategoryType category)
                continue;

            var matches =
                evidence.Where(e => e.Category.Category == category)
                    .ToList();

            if (matches.Count > 1)
            {
                output.Add(new(
                    category, item.Title,
                    CleanupCategoryProgressState.Unclear,
                    "Mehrfach gestartet – manuell prüfen",
                    $"{matches.Count} Ausführungen protokolliert. "
                    + "Kein einzelner Erfolg wird unterstellt."));
                continue;
            }

            if (matches.Count == 0)
            {
                var pending = transient.Contains(category);
                var unclear = pending || unknownHistory;
                output.Add(new(
                    category, item.Title,
                    unclear ? CleanupCategoryProgressState.Unclear
                            : CleanupCategoryProgressState.NotStarted,
                    pending ? "Gestartet – Protokoll ausstehend"
                        : unknownHistory ? "Verlauf unklar – Plan gesperrt"
                        : "Noch kein Start dokumentiert",
                    "Der ursprüngliche Eingangsscan ist "
                    + "keine aktuelle Restbestandsmessung."));
                continue;
            }

            var (action, result) = matches[0];
            var execution = action.CleanupExecutionResult!;
            var details =
                $"{result.DeletedFileCount:N0} Dateien gelöscht, "
                + $"{result.SkippedEntryCount:N0} übersprungen. "
                + "Restbestand und Systemscore sind damit "
                + "nicht automatisch bewertet.";

            CleanupCategoryProgressState state;
            string status;

            if (action.Status == CheckupTaskActionStatus.Cancelled
                || execution.WasCancelled || result.WasCancelled)
            {
                state = CleanupCategoryProgressState.Cancelled;
                status = "Abgebrochen – Änderungen prüfen";
            }
            else if (action.Status == CheckupTaskActionStatus.Successful
                && execution.IsPartiallySuccessful)
            {
                state = CleanupCategoryProgressState.Partial;
                status = result.FailedEntryCount > 0
                    ? $"Teilweise erfolgreich – {result.FailedEntryCount:N0} Löschfehler"
                    : "Teilweise erfolgreich – technische Hinweise";
            }
            else if (action.Status == CheckupTaskActionStatus.Successful
                && execution.IsSuccessful && result.IsSuccessful)
            {
                state = result.HasOnlyExpectedSafetySkips
                    ? CleanupCategoryProgressState.Successful
                    : CleanupCategoryProgressState.Unclear;
                status = state == CleanupCategoryProgressState.Successful
                    ? result.SkippedEntryCount > 0
                        ? "Ausgeführt – erwartete Ausnahmen"
                        : "Ausgeführt – ohne Löschfehler"
                    : "Ausgeführt – Ausschlüsse ungeklärt";
            }
            else if (action.Status == CheckupTaskActionStatus.Failed
                || execution.HasFailures || result.FailedEntryCount > 0)
            {
                state = CleanupCategoryProgressState.Failed;
                status = result.FailedEntryCount > 0
                    ? $"Technische Fehler – {result.FailedEntryCount:N0} Löschfehler"
                    : "Technische Ausführung fehlgeschlagen";
            }
            else
            {
                state = CleanupCategoryProgressState.Unclear;
                status = "Ausgeführt – Ergebnis nicht eindeutig";
            }

            output.Add(new(
                category, item.Title, state, status, details));
        }

        return output;
    }
}

public enum CleanupCategoryProgressState
{
    NotStarted,
    Successful,
    Partial,
    Failed,
    Cancelled,
    Unclear
}

/// <summary>Flüchtiger Anzeigewert; nicht Teil des gespeicherten Checkups.</summary>
public sealed record CleanupCategoryProgressItem(
    CleanupCategoryType Category,
    string Title,
    CleanupCategoryProgressState State,
    string StatusText,
    string DetailText);

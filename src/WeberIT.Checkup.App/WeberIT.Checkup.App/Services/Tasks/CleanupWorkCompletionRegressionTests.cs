#if DEBUG

using System;
using System.Collections.Generic;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.43: Keine Dateisystemzugriffe.
/// Arbeitsabschluss ist nicht identisch mit Loeschfehlerfreiheit.
/// </summary>
public static class CleanupWorkCompletionRegressionTests
{
    public static string RunAll()
    {
        var results = new List<string>();
        Run("Windows-Sperren verhindern Abschluss nicht", TestSkips, results);
        Run("Einzelne Loeschfehler bei vollem Lauf sind gruen dokumentiert",
            TestErrors, results);
        Run("Unvollstaendige Durchsuchung ist nicht gruen",
            TestIncomplete, results);
        Run("Abbruch, Blockade und historische Daten sind nicht gruen",
            TestOther, results);
        return $"Alle {results.Count} Arbeitsabschluss-Regressionstests erfolgreich."
            + Environment.NewLine + string.Join(Environment.NewLine, results);
    }

    private static CleanupActionExecutionResult Make(
        bool traversal = true, long failures = 0,
        long skips = 0, bool cancelled = false, bool blocked = false)
    {
        return new CleanupActionExecutionResult
        {
            PlanId = Guid.NewGuid(),
            WasCancelled = cancelled,
            WasBlocked = blocked,
            CategoryResults =
            [
                new CleanupActionCategoryExecutionResult
                {
                    Category = CleanupCategoryType.UserTemporaryFiles,
                    CategoryTitle = "Benutzer-Temp",
                    WasStarted = true,
                    WasCancelled = cancelled,
                    WasTraversalCompleted = traversal,
                    DeletedFileCount = 41579,
                    FailedEntryCount = failures,
                    SkippedEntryCount = skips,
                    SkippedEntryReasons = skips == 0 ? new()
                        : new()
                        {
                            new CleanupSkippedEntryReasonCount
                            {
                                Reason = CleanupSkippedEntryReason.LockedByProcess,
                                Count = skips
                            }
                        }
                }
            ]
        };
    }

    private static void TestSkips()
    {
        var result = Make(skips: 88);
        Require(result.IsSuccessful && result.IsWorkCompleted,
            "Prozesssperren duerfen Abschluss nicht verhindern.");
    }

    private static void TestErrors()
    {
        var result = Make(failures: 4);
        Require(result.IsPartiallySuccessful && result.IsWorkCompleted,
            "Einzelfehler duerfen Abschluss nicht aufhalten.");

        var action = new CheckupTaskActionResult
        {
            ActionCode = "action.cleanup.selected-safe-categories",
            Status = CheckupTaskActionStatus.Successful,
            CleanupExecutionResult = result
        };
        Require(action.StatusText.Contains("Bereinigung abgeschlossen")
            && action.StatusText.Contains("4"),
            "Gruener Abschluss muss nicht loeschbare Eintraege nennen.");

        var task = new CheckupTask
        {
            TaskCode = "task.storage.controlled-cleanup",
            ActionResults = new List<CheckupTaskActionResult> { action }
        };
        var list = new CheckupTaskList
        {
            Tasks = new List<CheckupTask> { task }
        };
        var progress = CleanupCategoryProgressService.Build(
            new List<CleanupActionCategory>
            {
                new()
                {
                    Category = CleanupCategoryType.UserTemporaryFiles,
                    Title = "Benutzer-Temp"
                }
            }, list);
        Require(progress.Count == 1
            && progress[0].State == CleanupCategoryProgressState.Successful,
            "Arbeitsstand bleibt bei komplettem Lauf gelb.");
    }

    private static void TestIncomplete()
    {
        var result = Make(traversal: false, failures: 4);
        Require(!result.IsWorkCompleted && result.IsPartiallySuccessful,
            "Unvollstaendige Bearbeitung darf nicht gruen sein.");
    }

    private static void TestOther()
    {
        Require(!Make(cancelled: true).IsWorkCompleted,
            "Abbruch ist kein Abschluss.");
        Require(!Make(blocked: true).IsWorkCompleted,
            "Blockierter Start ist kein Abschluss.");
        var historical = new CleanupActionCategoryExecutionResult
        {
            WasStarted = true,
            FailedEntryCount = 4,
            DeletedFileCount = 42
        };
        Require(!historical.IsWorkCompleted,
            "Alter Datensatz ohne Nachweis darf nicht hochgestuft werden.");
    }

    private static void Run(
        string name, Action test, ICollection<string> results)
    {
        try { test(); results.Add("OK: " + name); }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Arbeitsabschluss-Test fehlgeschlagen: " + name, exception);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

#endif

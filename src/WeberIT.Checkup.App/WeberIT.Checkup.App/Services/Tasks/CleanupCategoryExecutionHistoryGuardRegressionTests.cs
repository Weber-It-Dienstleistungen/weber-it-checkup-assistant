#if DEBUG

using System;
using System.Collections.Generic;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.40. Reine Modelltests.
/// Keine Dateien, keine Löschaktionen.
/// </summary>
public static class CleanupCategoryExecutionHistoryGuardRegressionTests
{
    private const string CleanupTask =
        "task.storage.controlled-cleanup";

    private const string CapacityTask =
        "task.storage.restore-system-volume-capacity";

    public static string RunAll()
    {
        var results = new List<string>();

        Run("Bereits gestartete Kategorie wird gesperrt",
            TestPreviouslyStartedCategory, results);
        Run("Andere Kategorien bleiben verfügbar",
            TestOtherCategoryAvailable, results);
        Run("Auch technisch fehlgeschlagene Läufe werden nicht wiederholt",
            TestFailedExecution, results);
        Run("Nicht gestartete Aktionen bleiben erneut möglich",
            TestUnstartedExecution, results);
        Run("Beide Bereinigungsaufgaben teilen sich die Historie",
            TestCrossTaskHistory, results);
        Run("Unvollständiges Altprotokoll sperrt ungesicherte Wiederholung",
            TestUnattributedHistory, results);
        Run("Fremde Aufgaben und Aktionen werden ignoriert",
            TestUnrelatedActions, results);

        return $"Alle {results.Count} Verlaufsschutztests erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(Environment.NewLine, results);
    }

    private static void TestPreviouslyStartedCategory()
    {
        var task = CreateTask(CleanupTask);
        task.ActionResults.Add(CreateRecordedAction(
            CleanupCategoryType.UserTemporaryFiles));
        var list = CreateList(task);

        Require(
            !CleanupCategoryExecutionHistoryGuard.CanStartCategory(
                list, CleanupCategoryType.UserTemporaryFiles),
            "Benutzer-Temp wurde erneut freigegeben.");

        ExpectInvalid(
            () => CleanupCategoryExecutionHistoryGuard
                .EnsureCanStartCategory(
                    list, CleanupCategoryType.UserTemporaryFiles));
    }

    private static void TestOtherCategoryAvailable()
    {
        var task = CreateTask(CleanupTask);
        task.ActionResults.Add(CreateRecordedAction(
            CleanupCategoryType.UserTemporaryFiles));
        var list = CreateList(task);

        Require(
            CleanupCategoryExecutionHistoryGuard.CanStartCategory(
                list, CleanupCategoryType.BrowserCache),
            "Eine noch unbearbeitete Kategorie wurde gesperrt.");

        CleanupCategoryExecutionHistoryGuard.EnsureCanStartCategory(
            list, CleanupCategoryType.BrowserCache);
    }

    private static void TestFailedExecution()
    {
        var task = CreateTask(CleanupTask);
        task.ActionResults.Add(CreateRecordedAction(
            CleanupCategoryType.UserTemporaryFiles,
            CheckupTaskActionStatus.Failed));
        var list = CreateList(task);

        Require(
            !CleanupCategoryExecutionHistoryGuard.CanStartCategory(
                list, CleanupCategoryType.UserTemporaryFiles),
            "Bereits begonnener fehlerhafter Lauf "
            + "wurde zur Wiederholung freigegeben.");
    }

    private static void TestUnstartedExecution()
    {
        var task = CreateTask(CleanupTask);
        task.ActionResults.Add(new CheckupTaskActionResult
        {
            ActionCode =
                "action.cleanup.selected-safe-categories",
            Status = CheckupTaskActionStatus.Failed,
            CleanupExecutionResult =
                new CleanupActionExecutionResult
                {
                    PlanId = Guid.NewGuid(),
                    WasBlocked = true
                }
        });
        var list = CreateList(task);

        Require(
            CleanupCategoryExecutionHistoryGuard.CanStartCategory(
                list, CleanupCategoryType.UserTemporaryFiles),
            "Eine nachweislich nicht gestartete "
            + "Aktion wurde als bereits ausgeführt gewertet.");
    }

    private static void TestCrossTaskHistory()
    {
        var first = CreateTask(CleanupTask);
        var other = CreateTask(CapacityTask);
        other.ActionResults.Add(CreateRecordedAction(
            CleanupCategoryType.BrowserCache));

        var list = new CheckupTaskList
        {
            Tasks = new List<CheckupTask> { first, other }
        };

        Require(
            !CleanupCategoryExecutionHistoryGuard.CanStartCategory(
                list, CleanupCategoryType.BrowserCache),
            "Die Kategorie wurde in einer zweiten "
            + "Bereinigungsaufgabe übersehen.");
    }

    private static void TestUnattributedHistory()
    {
        var task = CreateTask(CleanupTask);
        task.ActionResults.Add(new CheckupTaskActionResult
        {
            ActionCode =
                "action.cleanup.selected-safe-categories",
            Status = CheckupTaskActionStatus.Successful,
            StartedAt = DateTimeOffset.Now.AddMinutes(-5),
            FinishedAt = DateTimeOffset.Now.AddMinutes(-4)
        });

        var list = CreateList(task);

        Require(
            CleanupCategoryExecutionHistoryGuard
                .HasUnattributedCleanupExecution(list)
            && !CleanupCategoryExecutionHistoryGuard
                .CanStartCategory(list, CleanupCategoryType.BrowserCache),
            "Eine nicht zuordenbare ältere Aktion "
            + "wurde als sichere Wiederholung behandelt.");
    }

    private static void TestUnrelatedActions()
    {
        var task = CreateTask("task.program-updates.available");
        task.ActionResults.Add(CreateRecordedAction(
            CleanupCategoryType.UserTemporaryFiles));
        var list = CreateList(task);

        Require(
            CleanupCategoryExecutionHistoryGuard.CanStartCategory(
                list, CleanupCategoryType.UserTemporaryFiles),
            "Eine fremde Aufgabe wurde als "
            + "Bereinigungshistorie verwendet.");

        task.TaskCode = CleanupTask;
        task.ActionResults[0].ActionCode = "action.program-updates.test";

        Require(
            CleanupCategoryExecutionHistoryGuard.CanStartCategory(
                list, CleanupCategoryType.UserTemporaryFiles),
            "Ein fremder Aktionscode wurde als "
            + "Bereinigungsaktion interpretiert.");
    }

    private static CheckupTask CreateTask(string code) =>
        new()
        {
            TaskCode = code,
            Status = CheckupTaskStatus.Open
        };

    private static CheckupTaskList CreateList(CheckupTask task) =>
        new()
        {
            Tasks = new List<CheckupTask> { task }
        };

    private static CheckupTaskActionResult CreateRecordedAction(
        CleanupCategoryType category,
        CheckupTaskActionStatus status =
            CheckupTaskActionStatus.Successful)
    {
        var now = DateTimeOffset.Now;

        return new CheckupTaskActionResult
        {
            ActionCode =
                "action.cleanup.selected-safe-categories",
            Status = status,
            StartedAt = now.AddMinutes(-2),
            FinishedAt = now.AddMinutes(-1),
            CleanupExecutionResult =
                new CleanupActionExecutionResult
                {
                    PlanId = Guid.NewGuid(),
                    StartedAt = now.AddMinutes(-2),
                    FinishedAt = now.AddMinutes(-1),
                    CategoryResults = new List<
                        CleanupActionCategoryExecutionResult>
                    {
                        new()
                        {
                            Category = category,
                            WasStarted = true
                        }
                    }
                }
        };
    }

    private static void ExpectInvalid(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException(
            "Der erwartete Wiederholungsstopp wurde nicht ausgelöst.");
    }

    private static void Run(
        string title,
        Action action,
        ICollection<string> results)
    {
        try
        {
            action();
            results.Add("OK: " + title);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Verlaufsschutztest fehlgeschlagen: " + title,
                exception);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

#endif

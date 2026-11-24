#if DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.42: Modellprüfungen ohne Dateisystemzugriff.
/// </summary>
public static class CleanupCategoryProgressRegressionTests
{
    private static readonly CleanupCategoryType[] Types =
    [
        CleanupCategoryType.UserTemporaryFiles,
        CleanupCategoryType.BrowserCache,
        CleanupCategoryType.WindowsTemporaryFiles
    ];

    public static string RunAll()
    {
        var results = new List<string>();
        Run("Ohne Aktionen kein Start dokumentiert", TestUntouched, results);
        Run("Bekannte Sicherheitsausnahmen gelten als ausgeführt",
            TestSuccessful, results);
        Run("Echte Löschfehler bleiben Teilerfolg",
            TestPartial, results);
        Run("Fehler und Abbrüche werden getrennt angezeigt",
            TestFailedCancelled, results);
        Run("Unbekannte Ausschlüsse sind nicht grün",
            TestUnknownSkips, results);
        Run("Nicht zuordenbares Altprotokoll wird nicht erraten",
            TestLegacy, results);
        Run("Beide Bereinigungsaufgaben zählen",
            TestOtherTask, results);
        Run("Mehrfachausführung und lokaler Start bleiben unklar",
            TestAmbiguous, results);
        return $"Alle {results.Count} Kategorie-Fortschrittstests bestanden."
            + Environment.NewLine + string.Join(Environment.NewLine, results);
    }

    private static void TestUntouched()
    {
        var items = Build(new CheckupTaskList());
        Check(items.Count == 3
              && items.All(x => x.State == CleanupCategoryProgressState.NotStarted),
            "Nicht bearbeitete Kategorien falsch eingestuft.");
    }

    private static void TestSuccessful()
    {
        var task = Task("task.storage.controlled-cleanup",
            Action(Types[0], skips: 2,
                reasons: [new CleanupSkippedEntryReasonCount
                {
                    Reason = CleanupSkippedEntryReason.LockedByProcess,
                    Count = 2
                }]));
        var items = Build(List(task));
        Check(items[0].State == CleanupCategoryProgressState.Successful
              && items[1].State == CleanupCategoryProgressState.NotStarted,
            "Schutzgründe oder getrennte Kategorien falsch bewertet.");
    }

    private static void TestPartial()
    {
        var items = Build(List(Task("task.storage.controlled-cleanup",
            Action(Types[0], deleted: 41_579, failures: 4))));
        Check(items[0].State == CleanupCategoryProgressState.Partial
              && items[0].StatusText.Contains("4 Löschfehler"),
            "Teilerfolg wurde nicht korrekt ausgewiesen.");
    }

    private static void TestFailedCancelled()
    {
        var items = Build(List(Task("task.storage.controlled-cleanup",
            Action(Types[0], failures: 2, status: CheckupTaskActionStatus.Failed),
            Action(Types[1], cancelled: true,
                status: CheckupTaskActionStatus.Cancelled))));
        Check(items[0].State == CleanupCategoryProgressState.Failed
              && items[1].State == CleanupCategoryProgressState.Cancelled,
            "Fehler bzw. Abbruch fehlen.");
    }

    private static void TestUnknownSkips()
    {
        var items = Build(List(Task("task.storage.controlled-cleanup",
            Action(Types[0], skips: 1,
                reasons: [new CleanupSkippedEntryReasonCount
                {
                    Reason = CleanupSkippedEntryReason.OtherSafetySkip,
                    Count = 1
                }]))));
        Check(items[0].State == CleanupCategoryProgressState.Unclear,
            "Unbekannte Ausschlüsse fälschlich grün.");
    }

    private static void TestLegacy()
    {
        var task = Task("task.storage.controlled-cleanup",
            new CheckupTaskActionResult
            {
                ActionCode = "action.cleanup.selected-safe-categories",
                StartedAt = DateTimeOffset.Now.AddMinutes(-2),
                Status = CheckupTaskActionStatus.Successful
            });
        Check(Build(List(task)).All(x =>
                x.State == CleanupCategoryProgressState.Unclear),
            "Unzuordenbares Altprotokoll als unbearbeitet gewertet.");
    }

    private static void TestOtherTask()
    {
        var items = Build(List(Task(
            "task.storage.restore-system-volume-capacity",
            Action(Types[1]))));
        Check(items[1].State == CleanupCategoryProgressState.Successful
              && items[0].State == CleanupCategoryProgressState.NotStarted,
            "Bereinigung der anderen Aufgabe fehlt.");
    }

    private static void TestAmbiguous()
    {
        var t = Task("task.storage.controlled-cleanup",
            Action(Types[0]), Action(Types[0]));
        Check(Build(List(t))[0].State == CleanupCategoryProgressState.Unclear,
            "Doppelte Aktionen fälschlich aufgewertet.");

        var transient = CleanupCategoryProgressService.Build(
            Categories(), List(),
            new HashSet<CleanupCategoryType> { Types[1] });
        Check(transient[1].State == CleanupCategoryProgressState.Unclear,
            "Nur lokal gestartete Aktion verschwiegen.");
    }

    private static List<CleanupCategoryProgressItem> Build(
        CheckupTaskList list) =>
        CleanupCategoryProgressService.Build(Categories(), list).ToList();

    private static IReadOnlyList<CleanupActionCategory> Categories() =>
        Types.Select(x => new CleanupActionCategory
        {
            Category = x,
            Title = x.ToString()
        }).ToList();

    private static CheckupTaskList List(params CheckupTask[] tasks) =>
        new() { Tasks = tasks.ToList() };

    private static CheckupTask Task(
        string code, params CheckupTaskActionResult[] actions) =>
        new()
        {
            TaskCode = code,
            ActionResults = actions.ToList()
        };

    private static CheckupTaskActionResult Action(
        CleanupCategoryType category,
        long deleted = 0,
        long failures = 0,
        long skips = 0,
        bool cancelled = false,
        CheckupTaskActionStatus status = CheckupTaskActionStatus.Successful,
        List<CleanupSkippedEntryReasonCount>? reasons = null)
    {
        var start = DateTimeOffset.Now.AddMinutes(-1);
        return new CheckupTaskActionResult
        {
            ActionCode = "action.cleanup.selected-safe-categories",
            Status = status,
            StartedAt = start,
            FinishedAt = start.AddSeconds(1),
            CleanupExecutionResult = new CleanupActionExecutionResult
            {
                PlanId = Guid.NewGuid(),
                StartedAt = start,
                FinishedAt = start.AddSeconds(1),
                WasCancelled = cancelled,
                CategoryResults =
                [
                    new CleanupActionCategoryExecutionResult
                    {
                        Category = category,
                        WasStarted = true,
                        WasCancelled = cancelled,
                        DeletedFileCount = deleted,
                        FailedEntryCount = failures,
                        SkippedEntryCount = skips,
                        SkippedEntryReasons = reasons ?? new()
                    }
                ]
            }
        };
    }

    private static void Run(
        string name, Action test, ICollection<string> output)
    {
        try
        {
            test();
            output.Add("OK: " + name);
        }
        catch (Exception error)
        {
            throw new InvalidOperationException(
                "Kategorie-Fortschrittstest: " + name, error);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

#endif

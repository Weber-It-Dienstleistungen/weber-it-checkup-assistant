#if DEBUG

using System;
using System.Collections.Generic;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.39.
/// Regressionstest für die Sichtbarkeit von
/// mehrstufigen Bereinigungsaufgaben.
///
/// Keine Dateizugriffe, keine Bereinigung,
/// keine Systemänderungen: ausschließlich
/// künstliche Aufgabenmodelle.
/// </summary>
public static class CleanupTaskWorkListRegressionTests
{
    private const string CleanupCode =
        "task.storage.controlled-cleanup";

    private const string CapacityCode =
        "task.storage.restore-system-volume-capacity";

    public static string RunAll()
    {
        var results = new List<string>();

        Run(
            "Beide Bereinigungsaufgaben bleiben nach Einzelaktion sichtbar",
            TestCleanupTasksStayVisible,
            results);

        Run(
            "Mehrere Bereinigungsaktionen blenden die Aufgabe nicht aus",
            TestMultipleCleanupActions,
            results);

        Run(
            "Gewöhnliche Einzelaufgabe wartet weiterhin verborgen",
            TestOrdinaryTaskWaitsForVerification,
            results);

        Run("Nachgewiesener Neustart mit weiterem Neustartbedarf bleibt bearbeitbar",
            TestVerifiedRestartWithFurtherRequirement, results);
        Run("Unvollstaendiger Neustartnachweis bleibt unter bisheriger Sperre",
            TestIncompleteRestartEvidence, results);
        Run(
            "Abgeschlossene oder dokumentiert ausgelassene Aufgabe bleibt verborgen",
            TestTerminalStatusesRemainHidden,
            results);

        Run(
            "Bestehende Ausnahme für Programmupdates bleibt erhalten",
            TestProgramUpdatesStayVisible,
            results);

        Run(
            "Offene Bereinigung bleibt nach Kontrollscan bearbeitbar",
            TestOpenCleanupAfterVerification,
            results);

        return
            $"Alle {results.Count} Arbeitslisten-"
            + "Regressionstests erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(Environment.NewLine, results);
    }

    private static void TestCleanupTasksStayVisible()
    {
        foreach (var taskCode in new[]
                 {
                     CleanupCode,
                     CapacityCode
                 })
        {
            var task = CreateTask(taskCode);
            var list = CreateList(task);

            Require(
                list.ActiveTasks.Count == 1
                && ReferenceEquals(list.ActiveTasks[0], task)
                && list.ActiveTaskCount == 1,
                "Die offene Bereinigungs-Sammelaufgabe "
                + "wurde nach einer erfolgreichen "
                + "Einzelaktion ausgeblendet.");

            Require(
                list.AwaitingVerificationTaskCount == 1
                && list.HasTasksAwaitingVerification,
                "Die Abschlusskontrolle wurde "
                + "durch die Sichtbarkeit vergessen.");

            Require(
                task.ActionProgressText.Contains(
                    "weitere Kategorien",
                    StringComparison.Ordinal),
                "Der Aktionstext suggeriert, dass "
                + "keine weiteren Kategorien möglich sind.");
        }
    }

    private static void TestMultipleCleanupActions()
    {
        var task = CreateTask(CleanupCode);
        task.ActionResults.Add(CreateSuccessfulAction());

        var list = CreateList(task);

        Require(
            list.ActiveTaskCount == 1
            && list.AwaitingVerificationTaskCount == 1
            && task.ActionResultCount == 2,
            "Mehrere Aktionen müssen dokumentiert "
            + "bleiben, ohne die Aufgabe auszublenden.");
    }

    private static void TestOrdinaryTaskWaitsForVerification()
    {
        var task = CreateTask(
            "task.restart.perform-controlled-restart");

        var list = CreateList(task);

        Require(
            list.ActiveTaskCount == 0
            && list.AwaitingVerificationTaskCount == 1,
            "Die bisherige Sperre für "
            + "Einzelaufgaben wurde verändert.");
    }

    private static CheckupTask CreateVerifiedRestartTask()
    {
        var now = DateTimeOffset.UtcNow;
        var visitId = Guid.NewGuid();
        var request = new CheckupTaskActionResult
        {
            ActionCode = ControlledRestartEvidencePolicy.RequestCode,
            Status = CheckupTaskActionStatus.Unknown,
            ControlledRestartVisitId = visitId,
            ControlledRestartBootTimeUtc = now.AddHours(-2),
            StartedAt = now.AddMinutes(-20),
            FinishedAt = now.AddMinutes(-20)
        };
        var verification = new CheckupTaskActionResult
        {
            ActionCode = ControlledRestartEvidencePolicy.VerificationCode,
            Status = CheckupTaskActionStatus.Successful,
            ControlledRestartVisitId = visitId,
            ControlledRestartRequestId = request.Id,
            ControlledRestartBootTimeUtc = now.AddMinutes(-19),
            StartedAt = now.AddMinutes(-15),
            FinishedAt = now.AddMinutes(-15),
            RestartRequired = true,
            RestartStatusWasConclusive = true
        };
        return new CheckupTask
        {
            TaskCode = ControlledRestartEvidencePolicy.TaskCode,
            Status = CheckupTaskStatus.Open,
            ActionResults = new List<CheckupTaskActionResult> { request, verification }
        };
    }

    private static void TestVerifiedRestartWithFurtherRequirement()
    {
        var task = CreateVerifiedRestartTask();
        var list = CreateList(task);
        Require(list.ActiveTaskCount == 1 && list.AwaitingVerificationTaskCount == 0,
            "Ein bereits verifizierter Neustart mit weiterem Bedarf muss erneut bearbeitbar bleiben.");

        task.ActionResults.Add(CreateSuccessfulAction());
        Require(list.ActiveTaskCount == 0 && list.AwaitingVerificationTaskCount == 1,
            "Ein anderer ungepruefter Erfolg darf nicht durch den Neustartnachweis freigegeben werden.");
    }

    private static void TestIncompleteRestartEvidence()
    {
        var task = CreateVerifiedRestartTask();
        var list = CreateList(task);
        var verification = task.ActionResults[1];
        verification.ControlledRestartVisitId = Guid.NewGuid();
        Require(list.ActiveTaskCount == 0 && list.AwaitingVerificationTaskCount == 1,
            "Ein fremder Neustartnachweis darf die allgemeine Sperre nicht aufheben.");

        verification.ControlledRestartVisitId = task.ActionResults[0].ControlledRestartVisitId;
        verification.ControlledRestartBootTimeUtc = task.ActionResults[0].ControlledRestartBootTimeUtc;
        Require(list.ActiveTaskCount == 0 && list.AwaitingVerificationTaskCount == 1,
            "Ein unveraenderter Bootzeitpunkt ist kein gueltiger Neustartnachweis.");
    }

    private static void TestTerminalStatusesRemainHidden()
    {
        foreach (var status in new[]
                 {
                     CheckupTaskStatus.Completed,
                     CheckupTaskStatus.Skipped,
                     CheckupTaskStatus.NotFeasible
                 })
        {
            var task = CreateTask(CapacityCode);
            task.Status = status;

            var list = CreateList(task);

            Require(
                list.ActiveTaskCount == 0
                && list.AwaitingVerificationTaskCount == 0,
                "Eine nicht mehr offene Aufgabe "
                + "wurde erneut als aktiv angezeigt.");
        }
    }

    private static void TestProgramUpdatesStayVisible()
    {
        var list = CreateList(
            CreateTask("task.program-updates.available"));

        Require(
            list.ActiveTaskCount == 1
            && list.AwaitingVerificationTaskCount == 1,
            "Das bisherige Programmupdate-Verhalten "
            + "wurde unbeabsichtigt verändert.");
    }

    private static void TestOpenCleanupAfterVerification()
    {
        var task = CreateTask(CleanupCode);
        var list = CreateList(task);

        list.LastCompletionCheckAt =
            DateTimeOffset.Now.AddMinutes(1);

        Require(
            list.ActiveTaskCount == 1
            && list.AwaitingVerificationTaskCount == 0,
            "Eine weiterhin offene Bereinigungsaufgabe "
            + "muss nach einer Kontrolle erneut "
            + "bearbeitbar bleiben.");
    }

    private static CheckupTask CreateTask(string taskCode)
    {
        return new CheckupTask
        {
            TaskCode = taskCode,
            Status = CheckupTaskStatus.Open,
            ActionResults = new List<CheckupTaskActionResult>
            {
                CreateSuccessfulAction()
            }
        };
    }

    private static CheckupTaskActionResult CreateSuccessfulAction()
    {
        var finishedAt =
            DateTimeOffset.Now.AddMinutes(-1);

        return new CheckupTaskActionResult
        {
            ActionCode =
                "action.cleanup.selected-safe-categories",
            Status =
                CheckupTaskActionStatus.Successful,
            StartedAt = finishedAt.AddMinutes(-1),
            FinishedAt = finishedAt
        };
    }

    private static CheckupTaskList CreateList(CheckupTask task)
    {
        return new CheckupTaskList
        {
            TaskListVersion = 3,
            Tasks = new List<CheckupTask> { task }
        };
    }

    private static void Run(
        string title,
        Action test,
        ICollection<string> results)
    {
        try
        {
            test();
            results.Add("OK: " + title);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Arbeitslisten-Regressionstest "
                + "fehlgeschlagen: " + title,
                exception);
        }
    }

    private static void Require(
        bool condition,
        string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

#endif

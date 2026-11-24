#if DEBUG

using System;
using System.Collections.Generic;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.41.
/// Prüfungen der technischen Bereinigungsanzeige.
///
/// Ausschließlich künstliche Modellobjekte.
/// Keine Dateizugriffe, keine Löschoperationen.
/// </summary>
public static class CleanupTaskActionStatusTextRegressionTests
{
    private const string CleanupCode =
        "action.cleanup.selected-safe-categories";

    public static string RunAll()
    {
        var results = new List<string>();

        Run(
            "Teilerfolg mit Löschfehlern ist nicht pauschal erfolgreich",
            TestPartialExecution,
            results);

        Run(
            "Bekannte Schutzgründe erhalten erfolgreichen Technikstatus",
            TestExpectedSkips,
            results);

        Run(
            "Ungeklärte Ausnahmen verhindern uneingeschränkten Status",
            TestUnexplainedSkips,
            results);

        Run(
            "Historischer Bereinigungsdatensatz bleibt vorsichtig formuliert",
            TestHistoricalAction,
            results);

        Run(
            "Andere technische Aktionen behalten ihren Status",
            TestOtherAction,
            results);

        return
            $"Alle {results.Count} technischen Statusanzeigetests erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(Environment.NewLine, results);
    }

    private static void TestPartialExecution()
    {
        var action = CreateAction(
            failed: 4,
            deleted: 41_579);

        Require(
            action.CleanupExecutionResult!
                .IsPartiallySuccessful,
            "Der synthetische Teilerfolg ist ungültig.");

        Require(
            action.Status ==
                CheckupTaskActionStatus.Successful,
            "Der bisherige Workflow-Status "
            + "wurde unbeabsichtigt geändert.");

        Require(
            action.StatusText ==
                "Teilweise erfolgreich – 4 Löschfehler",
            "Tatsächliche Löschfehler erscheinen "
            + "weiterhin als uneingeschränkter Erfolg.");
    }

    private static void TestExpectedSkips()
    {
        var action = CreateAction(
            skipped: 3,
            reasons: new List<CleanupSkippedEntryReasonCount>
            {
                Reason(CleanupSkippedEntryReason.LockedByProcess, 2),
                Reason(CleanupSkippedEntryReason.ReadOnly, 1)
            });

        Require(
            action.CleanupExecutionResult!.IsSuccessful
            && action.StatusText ==
                "Technisch erfolgreich ausgeführt",
            "Normale Schutzgründe werden "
            + "fälschlich als Löschfehler bezeichnet.");
    }

    private static void TestUnexplainedSkips()
    {
        var action = CreateAction(
            skipped: 1,
            reasons: new List<CleanupSkippedEntryReasonCount>
            {
                Reason(CleanupSkippedEntryReason.OtherSafetySkip, 1)
            });

        Require(
            action.CleanupExecutionResult!.IsSuccessful
            && action.StatusText ==
                "Technisch beendet – Ausschlüsse ungeklärt",
            "Ein sonstiger Sicherheitsstopp "
            + "wird als uneingeschränkter Erfolg dargestellt.");
    }

    private static void TestHistoricalAction()
    {
        var action = new CheckupTaskActionResult
        {
            ActionCode = CleanupCode,
            Status = CheckupTaskActionStatus.Successful
        };

        Require(
            action.StatusText ==
                "Bereinigung dokumentiert – Details prüfen",
            "Ein älteres Protokoll ohne technische "
            + "Strukturdaten wird zu positiv dargestellt.");
    }

    private static void TestOtherAction()
    {
        var action = new CheckupTaskActionResult
        {
            ActionCode = "action.program-updates.selected-upgrades",
            Status = CheckupTaskActionStatus.Successful
        };

        Require(
            action.StatusText ==
                "Technisch erfolgreich ausgeführt",
            "Die Statusanzeige anderer Aufgaben "
            + "wurde unbeabsichtigt verändert.");
    }

    private static CheckupTaskActionResult CreateAction(
        long deleted = 0,
        long failed = 0,
        long skipped = 0,
        List<CleanupSkippedEntryReasonCount>? reasons = null)
    {
        var category = new CleanupActionCategoryExecutionResult
        {
            Category = CleanupCategoryType.UserTemporaryFiles,
            CategoryTitle = "Benutzer-Temp-Test",
            WasStarted = true,
            DeletedFileCount = deleted,
            FailedEntryCount = failed,
            SkippedEntryCount = skipped,
            SkippedEntryReasons =
                reasons ?? new List<CleanupSkippedEntryReasonCount>()
        };

        return new CheckupTaskActionResult
        {
            ActionCode = CleanupCode,
            Status = CheckupTaskActionStatus.Successful,
            CleanupExecutionResult = new CleanupActionExecutionResult
            {
                PlanId = Guid.NewGuid(),
                CategoryResults = new List<
                    CleanupActionCategoryExecutionResult>
                {
                    category
                }
            }
        };
    }

    private static CleanupSkippedEntryReasonCount Reason(
        CleanupSkippedEntryReason reason,
        long count) =>
        new()
        {
            Reason = reason,
            Count = count
        };

    private static void Run(
        string name,
        Action test,
        ICollection<string> results)
    {
        try
        {
            test();
            results.Add("OK: " + name);
        }
        catch (Exception error)
        {
            throw new InvalidOperationException(
                "Statusanzeige-Test fehlgeschlagen: "
                + name,
                error);
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

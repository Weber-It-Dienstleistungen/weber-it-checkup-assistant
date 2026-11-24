#if DEBUG

using System;
using System.Collections.Generic;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Cleanup;

/// <summary>
/// Sprint 55.14.35:
/// Eindeutige Zuordnung eines Bereinigungsprotokolls
/// zum laufenden Kundencheckup absichern.
///
/// Ausschließlich künstliche Modelle; keine Dateien,
/// keine Administratorrechte und keine Datenbankzugriffe.
/// </summary>
public static class CleanupExecutionCorrelationResolverRegressionTests
{
    private const CleanupCategoryType Category =
        CleanupCategoryType.WindowsTemporaryFiles;

    private static readonly DateTime EntranceDate =
        new(2026, 11, 5, 10, 0, 0, DateTimeKind.Local);

    private static readonly DateTimeOffset EntranceTime =
        new(EntranceDate);

    private static readonly DateTimeOffset MeasurementTime =
        EntranceTime.AddMinutes(30);

    public static string RunAll()
    {
        var results = new List<string>();

        Run("Eindeutigen Nachweis mit IDs und Zeitpunkten zuordnen",
            TestUniqueMatch, results);
        Run("Fremden Kundencheckup ausschließen",
            TestForeignVisit, results);
        Run("Falsche Bereinigungskategorie ausschließen",
            TestWrongCategory, results);
        Run("Mehrdeutige Nachweise ablehnen",
            TestAmbiguousMatches, results);
        Run("Bereits kontrollierte Aktion nicht erneut zuordnen",
            TestIntermediateScanBoundary, results);
        Run("Zeitgrenzen der Messung beachten",
            TestTimeBoundaries, results);
        Run("Widersprüchliche Aktionsdaten ablehnen",
            TestInvalidActionMetadata, results);
        Run("Abgeschlossene und vorbereitete Checkups ausschließen",
            TestVisitState, results);

        return "Alle " + results.Count
            + " Zuordnungs-Regressionstests erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(Environment.NewLine, results);
    }

    private static void TestUniqueMatch()
    {
        var fixture = CreateFixture();
        var action = AddExecution(
            fixture.Task, fixture.Visit.Id,
            EntranceTime.AddMinutes(10),
            EntranceTime.AddMinutes(12));

        var match = Resolve(fixture);

        Require(match is not null,
            "Die eindeutige Aktion wurde nicht gefunden.");
        Require(match!.ActionResultId == action.Id,
            "Die Aktions-ID stimmt nicht.");
        Require(match.PlanId == action.CleanupExecutionResult!.PlanId,
            "Die Plan-ID stimmt nicht.");
        Require(match.StartedAt == action.StartedAt
                && match.FinishedAt == action.FinishedAt,
            "Die Ausführungszeitpunkte stimmen nicht.");
        Require(match.CategoryResult.Category == Category
                && match.CategoryResult.WasStarted,
            "Das Kategorieergebnis stimmt nicht.");
    }

    private static void TestForeignVisit()
    {
        var fixture = CreateFixture();

        AddExecution(
            fixture.Task, Guid.NewGuid(),
            EntranceTime.AddMinutes(10),
            EntranceTime.AddMinutes(12));

        Require(Resolve(fixture) is null,
            "Eine Aktion eines fremden Checkups wurde zugeordnet.");
    }

    private static void TestWrongCategory()
    {
        var fixture = CreateFixture();

        AddExecution(
            fixture.Task, fixture.Visit.Id,
            EntranceTime.AddMinutes(10),
            EntranceTime.AddMinutes(12),
            CleanupCategoryType.BrowserCache);

        Require(Resolve(fixture) is null,
            "Eine fremde Bereinigungskategorie wurde zugeordnet.");
    }

    private static void TestAmbiguousMatches()
    {
        var fixture = CreateFixture();

        AddExecution(
            fixture.Task, fixture.Visit.Id,
            EntranceTime.AddMinutes(10),
            EntranceTime.AddMinutes(12));
        AddExecution(
            fixture.Task, fixture.Visit.Id,
            EntranceTime.AddMinutes(15),
            EntranceTime.AddMinutes(17));

        Require(Resolve(fixture) is null,
            "Bei zwei Nachweisen wurde willkürlich einer ausgewählt.");
    }

    private static void TestIntermediateScanBoundary()
    {
        var fixture = CreateFixture();

        AddExecution(
            fixture.Task, fixture.Visit.Id,
            EntranceTime.AddMinutes(10),
            EntranceTime.AddMinutes(12));

        // Eine bereits erfolgte Zwischenprüfung begrenzt
        // die Suche nach neuen Bereinigungen.
        fixture.Visit.CleanupPotentialIntermediateScans.Add(
            new CleanupPotentialIntermediateScan
            {
                CustomerCheckupVisitId = fixture.Visit.Id,
                Category = Category,
                RecordedAt = EntranceTime.AddMinutes(20)
            });

        Require(Resolve(fixture) is null,
            "Eine vor der Zwischenprüfung liegende Aktion "
            + "wurde erneut zugeordnet.");

        var newerAction = AddExecution(
            fixture.Task, fixture.Visit.Id,
            EntranceTime.AddMinutes(23),
            EntranceTime.AddMinutes(25));

        Require(Resolve(fixture)?.ActionResultId == newerAction.Id,
            "Die neuere Bereinigung wurde nicht gefunden.");
    }

    private static void TestTimeBoundaries()
    {
        var beforeEntrance = CreateFixture();
        AddExecution(
            beforeEntrance.Task, beforeEntrance.Visit.Id,
            EntranceTime.AddMinutes(-3),
            EntranceTime.AddMinutes(2));

        Require(Resolve(beforeEntrance) is null,
            "Eine vor dem Eingangsscan gestartete Aktion "
            + "wurde akzeptiert.");

        var duringMeasurement = CreateFixture();
        AddExecution(
            duringMeasurement.Task, duringMeasurement.Visit.Id,
            EntranceTime.AddMinutes(28),
            EntranceTime.AddMinutes(32));

        Require(Resolve(duringMeasurement) is null,
            "Eine erst nach Messbeginn abgeschlossene Aktion "
            + "wurde akzeptiert.");
    }

    private static void TestInvalidActionMetadata()
    {
        var fixture = CreateFixture();
        var action = AddExecution(
            fixture.Task, fixture.Visit.Id,
            EntranceTime.AddMinutes(10),
            EntranceTime.AddMinutes(12));

        action.Status = CheckupTaskActionStatus.Failed;
        Require(Resolve(fixture) is null,
            "Widersprüchlicher Aktionsstatus wurde akzeptiert.");

        action.Status = CheckupTaskActionStatus.Successful;
        action.ActionCode = "action.test.unrelated";
        Require(Resolve(fixture) is null,
            "Fremder Aktionscode wurde akzeptiert.");

        action.ActionCode = "action.cleanup.selected-safe-categories";
        action.StartedAt = EntranceTime.AddMinutes(11);
        Require(Resolve(fixture) is null,
            "Abweichende Protokollzeitpunkte wurden akzeptiert.");
    }

    private static void TestVisitState()
    {
        var completed = CreateFixture();
        AddExecution(
            completed.Task, completed.Visit.Id,
            EntranceTime.AddMinutes(10),
            EntranceTime.AddMinutes(12));

        completed.Visit.Status = CustomerCheckupVisitStatus.Completed;
        Require(Resolve(completed) is null,
            "Abgeschlossener Kundencheckup wurde akzeptiert.");

        var prepared = CreateFixture();
        AddExecution(
            prepared.Task, prepared.Visit.Id,
            EntranceTime.AddMinutes(10),
            EntranceTime.AddMinutes(12));

        prepared.Visit.AfterCheckup = new CheckupSnapshot();
        prepared.Visit.Comparison = new CustomerCheckupComparison();

        Require(Resolve(prepared) is null,
            "Vorbereiteter Abschluss wurde akzeptiert.");
    }

    private static (
        CheckupSession Session,
        CustomerCheckupVisit Visit,
        CheckupTask Task) CreateFixture()
    {
        var visit = new CustomerCheckupVisit
        {
            Id = Guid.NewGuid(),
            StartedAt = EntranceDate.AddMinutes(-5),
            Status = CustomerCheckupVisitStatus.InProgress,
            BeforeCheckup = new CheckupSnapshot
            {
                ScanDate = EntranceDate
            }
        };

        var task = new CheckupTask
        {
            Id = Guid.NewGuid(),
            TaskCode = "task.storage.controlled-cleanup"
        };

        var session = new CheckupSession
        {
            CustomerCheckupVisits = new List<CustomerCheckupVisit> { visit },
            TaskList = new CheckupTaskList
            {
                TaskListVersion = 1,
                Tasks = new List<CheckupTask> { task }
            }
        };

        return (session, visit, task);
    }

    private static CheckupTaskActionResult AddExecution(
        CheckupTask task,
        Guid visitId,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        CleanupCategoryType category = Category)
    {
        var execution = new CleanupActionExecutionResult
        {
            PlanId = Guid.NewGuid(),
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            CategoryResults = new List<CleanupActionCategoryExecutionResult>
            {
                new()
                {
                    Category = category,
                    CategoryTitle = category.ToString(),
                    WasStarted = true,
                    DeletedFileCount = 2,
                    DeletedSizeBytes = 1024,
                    FailedEntryCount = 0,
                    SkippedEntryCount = 0
                }
            }
        };

        var action = new CheckupTaskActionResult
        {
            Id = Guid.NewGuid(),
            ActionCode = "action.cleanup.selected-safe-categories",
            Status = CheckupTaskActionStatus.Successful,
            StartedAt = startedAt,
            FinishedAt = finishedAt
        };

        // Produktiven Modellpfad für die tiefe Kopie verwenden.
        action.CaptureCleanupExecution(execution, visitId);
        task.ActionResults.Add(action);
        return action;
    }

    private static CleanupExecutionCorrelationMatch? Resolve(
        (CheckupSession Session,
         CustomerCheckupVisit Visit,
         CheckupTask Task) fixture)
    {
        return CleanupExecutionCorrelationResolver.FindUniqueMatch(
            fixture.Session,
            fixture.Visit,
            Category,
            MeasurementTime);
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
                "Zuordnungs-Regressionstest fehlgeschlagen: "
                + title, exception);
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

#if DEBUG
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Assessment;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>Sprint 55.14.45: Echte Bewertungsregeln mit synthetischen Scans.</summary>
public static class CleanupCompletionScoringRegressionTests
{
    private const ulong Gb = 1024UL * 1024UL * 1024UL;
    private static readonly DateTimeOffset Entrance =
        new(2026, 11, 24, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ScanStart = Entrance.AddHours(1);

    public static string RunAll()
    {
        var results = new List<string>();
        Run("V3-Abschluss gewinnt Punkte trotz gemessenem Restbestand", () =>
        {
            var (service, working, visit, after) = Scenario();
            var original = visit.BeforeCheckup.Assessment.SystemCondition.Score;
            Require(original.HasValue && original.Value < 100);
            after.Assessment = service.AssessCompletion(after, working, visit, ScanStart);
            Require(after.Assessment.ScoringVersion == 3);
            Require(after.Assessment.SystemCondition.Score == 100);
            Require(after.CleanupPotentialInformation.SafePotentialBytes == 2 * Gb);
            Require(after.Assessment.Findings.Any(f => f.Code == "system.cleanup.work-completed"));
            Require(!after.TaskList.Tasks.Any(t => t.TaskCode == "task.storage.controlled-cleanup"));
        }, results);

        Run("Ohne Abschlussnachweis bleibt der Punktabzug bestehen", () =>
        {
            var (service, working, visit, after) = Scenario(traversal: false);
            var score = service.AssessCompletion(after, working, visit, ScanStart);
            Require(score.SystemCondition.Score == visit.BeforeCheckup.Assessment.SystemCondition.Score);
            Require(score.Findings.Any(f => f.Code == "system.cleanup.large-safe-potential"));
        }, results);

        Run("Tatsaechlicher Speichermangel bleibt ein eigenstaendiger Befund", () =>
        {
            var (service, working, visit, after) = Scenario(lowSpace: true);
            after.Assessment = service.AssessCompletion(after, working, visit, ScanStart);
            Require(after.Assessment.SystemCondition.Score is < 100);
            Require(after.Assessment.Findings.Any(f =>
                f.Code == "system.storage.system-volume-critically-low"));
            Require(after.TaskList.Tasks.Any(t =>
                t.TaskCode == "task.storage.restore-system-volume-capacity"));
            Require(after.StorageInformation.Volumes[0].FreeSpaceBytes == 5 * Gb);
        }, results);

        Run("Laufender V2-Vorgang behaelt alte Bewertungslogik", () =>
        {
            var (service, working, visit, after) = Scenario();
            visit.BeforeCheckup.Assessment.ScoringVersion = 2;
            var assessment = service.AssessCompletion(after, working, visit, ScanStart);
            Require(assessment.ScoringVersion == 2);
            Require(assessment.Findings.Any(f => f.Code == "system.cleanup.large-safe-potential"));
            Require(!assessment.Findings.Any(f => f.Code == "system.cleanup.work-completed"));
        }, results);

        Run("Eingangsscan und Aktionshistorie bleiben unveraendert", () =>
        {
            var (service, working, visit, after) = Scenario();
            var beforeScore = visit.BeforeCheckup.Assessment.SystemCondition.Score;
            var task = working.TaskList.Tasks.Single(t => t.TaskCode == "task.storage.controlled-cleanup");
            var action = task.ActionResults.Single();
            after.Assessment = service.AssessCompletion(after, working, visit, ScanStart);
            var restored = CheckupSnapshot.Capture(after).RestoreAsSession();
            Require(restored.Assessment.SystemCondition.Score == 100);
            Require(restored.CleanupPotentialInformation.SafePotentialBytes == 2 * Gb);
            Require(visit.BeforeCheckup.Assessment.SystemCondition.Score == beforeScore);
            Require(visit.BeforeCheckup.CleanupPotentialInformation.SafePotentialBytes == 2 * Gb);
            Require(ReferenceEquals(task.ActionResults.Single(), action));
            Require(visit.BeforeCheckup.TaskList.Tasks.All(t => t.ActionResults.Count == 0));
        }, results);

        Run("Normaler Scan ohne Vorgang und fremder Kontext erhalten keinen Bonus", () =>
        {
            var (service, working, visit, after) = Scenario();
            Require(service.Assess(after).Findings.Any(f => f.Code == "system.cleanup.large-safe-potential"));
            bool rejected = false;
            try
            {
                service.AssessCompletion(after, working, new CustomerCheckupVisit(), ScanStart);
            }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected);
        }, results);

        return $"Alle {results.Count} Abschlussbewertungs-Regressionstests erfolgreich."
            + Environment.NewLine + string.Join(Environment.NewLine, results);
    }

    private static (CheckupAssessmentService Service, CheckupSession Working,
        CustomerCheckupVisit Visit, CheckupSession After) Scenario(
        bool traversal = true, bool lowSpace = false)
    {
        var service = new CheckupAssessmentService(
            new ICheckupAssessmentRule[]
            {
                new StorageAssessmentRule(),
                new CleanupPotentialAssessmentRule()
            },
            new SystemConditionAssessmentService(),
            new HardwareConditionAssessmentService());
        var working = MakeSession(Entrance, lowSpace);
        working.Assessment = service.Assess(working);
        var visit = new CustomerCheckupVisit
        {
            StartedAt = Entrance.LocalDateTime,
            BeforeCheckup = CheckupSnapshot.Capture(working)
        };
        working.CustomerCheckupVisits.Add(visit);
        var task = working.TaskList.Tasks.Single(t => t.TaskCode == (lowSpace
            ? "task.storage.restore-system-volume-capacity"
            : "task.storage.controlled-cleanup"));
        var started = Entrance.AddMinutes(10);
        var finished = Entrance.AddMinutes(20);
        task.ActionResults.Add(new CheckupTaskActionResult
        {
            Id = Guid.NewGuid(),
            ActionCode = "action.cleanup.selected-safe-categories",
            Status = CheckupTaskActionStatus.Successful,
            CleanupCustomerCheckupVisitId = visit.Id,
            StartedAt = started,
            FinishedAt = finished,
            CleanupExecutionResult = new CleanupActionExecutionResult
            {
                PlanId = Guid.NewGuid(),
                StartedAt = started,
                FinishedAt = finished,
                CategoryResults = new()
                {
                    new()
                    {
                        Category = CleanupCategoryType.UserTemporaryFiles,
                        WasStarted = true,
                        WasTraversalCompleted = traversal,
                        DeletedFileCount = 42,
                        SkippedEntryCount = 1,
                        SkippedEntryReasons = new()
                        {
                            new() { Reason = CleanupSkippedEntryReason.LockedByProcess, Count = 1 }
                        }
                    }
                }
            }
        });
        return (service, working, visit, MakeSession(ScanStart.AddMinutes(5), lowSpace));
    }

    private static CheckupSession MakeSession(DateTimeOffset scanDate, bool lowSpace) => new()
    {
        ScanDate = scanDate.LocalDateTime,
        OperatingSystemInformation = new() { Name = "Windows 11" },
        SecurityInformation = new() { AntivirusStatus = SecurityState.Enabled },
        StartupInformation = new() { AnalysisStatus = StartupAnalysisStatus.Analyzed },
        StorageInformation = new()
        {
            Volumes = new()
            {
                new()
                {
                    DriveLetter = "C:",
                    DriveType = "Fixed",
                    IsReady = true,
                    IsSystemVolume = true,
                    TotalSizeBytes = 100 * Gb,
                    FreeSpaceBytes = lowSpace ? 5 * Gb : 50 * Gb
                }
            }
        },
        CleanupPotentialInformation = new()
        {
            AnalysisStatus = CleanupMeasurementStatus.Measured,
            Categories = new()
            {
                new()
                {
                    Category = CleanupCategoryType.UserTemporaryFiles,
                    Classification = CleanupCategoryClassification.SafePotential,
                    MeasurementStatus = CleanupMeasurementStatus.Measured,
                    SizeBytes = 2 * Gb
                }
            }
        }
    };

    private static void Run(string name, Action test, ICollection<string> results)
    {
        try { test(); results.Add("OK: " + name); }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Abschlussbewertungs-Test fehlgeschlagen: " + name, exception);
        }
    }

    private static void Require(bool condition)
    {
        if (!condition)
            throw new InvalidOperationException("Unerwartete Abschlussbewertung.");
    }
}
#endif

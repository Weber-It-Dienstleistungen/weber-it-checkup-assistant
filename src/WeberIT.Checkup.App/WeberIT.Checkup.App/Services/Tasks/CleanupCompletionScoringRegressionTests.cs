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

        Run("V4 akzeptiert nur ausdruecklich dokumentierte Empfehlung und erhaelt Befund", () =>
        {
            var (service, working, visit, after) = Scenario(version: 4, includeRiskRule: true);
            var task = working.TaskList.Tasks.Single(t => t.TaskCode == "task.security.drive-encryption");
            task.ApplyStatus(CheckupTaskStatus.RiskAccepted, "Kunde nach Beratung dagegen", "");
            task.StatusChangedAt = Entrance.AddMinutes(30).LocalDateTime;
            after.Assessment = service.AssessCompletion(after, working, visit, ScanStart);
            Require(after.Assessment.ScoringVersion == 4);
            Require(after.Assessment.SystemCondition.Score == 100);
            Require(after.Assessment.Findings.Any(f => f.Code == "system.security.mobile-drive-not-encrypted"
                && f.IsCustomerRiskAccepted));
            Require(CheckupSnapshot.Capture(after).RestoreAsSession().Assessment.Findings
                .Any(f => f.IsCustomerRiskAccepted));
            Require(visit.BeforeCheckup.Assessment.Findings.All(f => !f.IsCustomerRiskAccepted));
        }, results);

        Run("Erledigt, uebersprungen und kritische Befunde erhalten keine Risikoentlastung", () =>
        {
            var (service, working, visit, after) = Scenario(version: 4, includeRiskRule: true);
            var task = working.TaskList.Tasks.Single(t => t.TaskCode == "task.security.drive-encryption");
            task.ApplyStatus(CheckupTaskStatus.Completed, "Geprueft", "");
            Require(service.AssessCompletion(after, working, visit, ScanStart)
                .Findings.All(f => !f.IsCustomerRiskAccepted));
            task.ApplyStatus(CheckupTaskStatus.Skipped, "Uebersprungen", "");
            Require(service.AssessCompletion(after, working, visit, ScanStart)
                .Findings.All(f => !f.IsCustomerRiskAccepted));
            var critical = new CheckupTask { TaskCode = "task.security.antivirus",
                SourceFindingCodes = new() { "system.security.antivirus-disabled" } };
            bool rejected = false;
            try { critical.ApplyStatus(CheckupTaskStatus.RiskAccepted, "Kunde", ""); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected);
        }, results);

        Run("V3 bleibt ohne Kundenrisikoentlastung", () =>
        {
            var (service, working, visit, after) = Scenario(version: 3, includeRiskRule: true);
            var task = working.TaskList.Tasks.Single(t => t.TaskCode == "task.security.drive-encryption");
            task.ApplyStatus(CheckupTaskStatus.RiskAccepted, "Kunde", "");
            task.StatusChangedAt = Entrance.AddMinutes(30).LocalDateTime;
            var assessment = service.AssessCompletion(after, working, visit, ScanStart);
            Require(assessment.ScoringVersion == 3 && assessment.Findings.All(f => !f.IsCustomerRiskAccepted));
        }, results);

        Run("Unpassender Entscheidungszeitpunkt erhaelt keine Entlastung", () =>
        {
            var (service, working, visit, after) = Scenario(version: 4, includeRiskRule: true);
            var task = working.TaskList.Tasks.Single(t => t.TaskCode == "task.security.drive-encryption");
            task.ApplyStatus(CheckupTaskStatus.RiskAccepted, "Kunde", "");
            task.StatusChangedAt = Entrance.AddMinutes(-1).LocalDateTime;
            Require(service.AssessCompletion(after, working, visit, ScanStart)
                .Findings.All(f => !f.IsCustomerRiskAccepted));
        }, results);

        Run("Routinebereinigung ist auch ohne Eingangsbefund verfuegbar", () =>
        {
            Require(CheckupTaskGenerator.Generate(Array.Empty<CheckupFinding>(), true)
                .Tasks.Count(t => t.TaskCode == "task.storage.controlled-cleanup") == 1);
            Require(!CheckupTaskGenerator.Generate(Array.Empty<CheckupFinding>())
                .Tasks.Any(t => t.TaskCode == "task.storage.controlled-cleanup"));
        }, results);

        Run("Neustartnachweis verlangt tatsaechlich neuen Bootzeitpunkt nach Auftrag", () =>
        {
            var before = Entrance.AddHours(-1);
            Require(!ControlledRestartEvidencePolicy.IsRebootVerified(before, Entrance, before, ScanStart));
            Require(!ControlledRestartEvidencePolicy.IsRebootVerified(before, Entrance,
                Entrance.AddMinutes(-1), ScanStart));
            Require(ControlledRestartEvidencePolicy.IsRebootVerified(before, Entrance,
                Entrance.AddMinutes(1), ScanStart));
            Require(!ControlledRestartEvidencePolicy.IsRebootVerified(before, Entrance,
                ScanStart.AddMinutes(1), ScanStart));
        }, results);

        Run("Neustartauftrag ist vorgangsgebunden und nur einmal verifizierbar", () =>
        {
            var visitId = Guid.NewGuid();
            var task = new CheckupTask { TaskCode = ControlledRestartEvidencePolicy.TaskCode };
            var request = new CheckupTaskActionResult
            {
                ActionCode = ControlledRestartEvidencePolicy.RequestCode,
                Status = CheckupTaskActionStatus.Unknown,
                ControlledRestartVisitId = visitId,
                ControlledRestartBootTimeUtc = Entrance.AddHours(-1),
                StartedAt = Entrance
            };
            task.ActionResults.Add(request);
            Require(ReferenceEquals(ControlledRestartEvidencePolicy.GetPendingRequest(task, visitId), request));
            Require(ControlledRestartEvidencePolicy.GetPendingRequest(task, Guid.NewGuid()) is null);
            task.ActionResults.Add(new CheckupTaskActionResult
            {
                ActionCode = ControlledRestartEvidencePolicy.VerificationCode,
                ControlledRestartVisitId = visitId,
                ControlledRestartRequestId = request.Id
            });
            Require(ControlledRestartEvidencePolicy.GetPendingRequest(task, visitId) is null);
        }, results);

        Run("Gepruefter unveraenderter Autostart erhaelt Punkte ohne Befundverlust", () =>
        {
            var (service, working, visit, after) = Scenario(version: 4, includeStartup: true);
            var task = working.TaskList.Tasks.Single(t => t.TaskCode == "task.performance.startup-background-load");
            task.ApplyStatus(CheckupTaskStatus.NoOptimizationRequired, "Eintraege einzeln geprueft", "");
            task.StatusChangedAt = Entrance.AddMinutes(30).LocalDateTime;
            var assessment = service.AssessCompletion(after, working, visit, ScanStart);
            Require(assessment.SystemCondition.Score == 100);
            Require(assessment.Findings.Any(f => f.Code == "system.startup.extensive"
                && f.IsOptimizationReviewConfirmed && !f.IsCustomerRiskAccepted));
            Require(visit.BeforeCheckup.Assessment.Findings.All(f => !f.IsOptimizationReviewConfirmed));
        }, results);

        Run("Neue oder veraenderte Autostarteintraege verhindern Entlastung", () =>
        {
            var (service, working, visit, after) = Scenario(version: 4, includeStartup: true);
            var task = working.TaskList.Tasks.Single(t => t.TaskCode == "task.performance.startup-background-load");
            task.ApplyStatus(CheckupTaskStatus.NoOptimizationRequired, "Geprueft", "");
            task.StatusChangedAt = Entrance.AddMinutes(30).LocalDateTime;
            after.StartupInformation.Entries[0].SourceIdentifier = "NewEntry";
            Require(service.AssessCompletion(after, working, visit, ScanStart)
                .Findings.All(f => !f.IsOptimizationReviewConfirmed));
            after.StartupInformation = MakeStartup(ScanStart.AddMinutes(1));
            after.StartupInformation.AnalysisStatus = StartupAnalysisStatus.PartiallyAnalyzed;
            Require(service.AssessCompletion(after, working, visit, ScanStart)
                .Findings.All(f => !f.IsOptimizationReviewConfirmed));
            after.StartupInformation = MakeStartup(ScanStart.AddMinutes(1));
            after.StartupInformation.Entries[0].Classification = StartupClassification.Conspicuous;
            Require(service.AssessCompletion(after, working, visit, ScanStart)
                .Findings.All(f => !f.IsOptimizationReviewConfirmed));
        }, results);

        Run("Fachlicher Autostartabschluss gilt nicht fuer andere Empfehlungen", () =>
        {
            var (service, working, visit, after) = Scenario(version: 4, includeRiskRule: true);
            var task = working.TaskList.Tasks.Single(t => t.TaskCode == "task.security.drive-encryption");
            bool rejected = false;
            try { task.ApplyStatus(CheckupTaskStatus.NoOptimizationRequired, "Geprueft", ""); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected);
        }, results);

        return $"Alle {results.Count} Abschlussbewertungs-Regressionstests erfolgreich."
            + Environment.NewLine + string.Join(Environment.NewLine, results);
    }

    private static (CheckupAssessmentService Service, CheckupSession Working,
        CustomerCheckupVisit Visit, CheckupSession After) Scenario(
        bool traversal = true, bool lowSpace = false,
        int version = 3, bool includeRiskRule = false, bool includeStartup = false)
    {
        var rules = new List<ICheckupAssessmentRule>
        {
            new StorageAssessmentRule(), new CleanupPotentialAssessmentRule()
        };
        if (includeRiskRule)
            rules.Add(new SyntheticRiskRule());
        if (includeStartup)
            rules.Add(new StartupAssessmentRule());
        var service = new CheckupAssessmentService(
            rules,
            new SystemConditionAssessmentService(),
            new HardwareConditionAssessmentService());
        var working = MakeSession(Entrance, lowSpace);
        if (includeStartup)
            working.StartupInformation = MakeStartup(Entrance);
        working.Assessment = service.Assess(working);
        working.Assessment.ScoringVersion = version;
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
        var after = MakeSession(ScanStart.AddMinutes(5), lowSpace);
        if (includeStartup)
            after.StartupInformation = MakeStartup(ScanStart.AddMinutes(1));
        return (service, working, visit, after);
    }

    private static StartupInformation MakeStartup(DateTimeOffset date) => new()
    {
        AnalysisStatus = StartupAnalysisStatus.Analyzed,
        AnalysisDate = date.LocalDateTime,
        Entries = Enumerable.Range(1, 21).Select(index => new StartupEntryInformation
        {
            DisplayName = $"Test {index}", SourceIdentifier = $"TestSource{index}",
            State = StartupEntryState.Enabled, Classification = StartupClassification.ProbablyUseful,
            TargetExists = true
        }).ToList()
    };

    private sealed class SyntheticRiskRule : ICheckupAssessmentRule
    {
        public IEnumerable<CheckupFinding> Evaluate(CheckupSession session) =>
            new[] { new CheckupFinding
            {
                Code = "system.security.mobile-drive-not-encrypted",
                CauseGroup = "system.security.drive-encryption",
                Title = "Laufwerk nicht verschluesselt",
                Severity = FindingSeverity.Recommendation,
                AssessmentTarget = FindingAssessmentTarget.SystemCondition
            } };
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

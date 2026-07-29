using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Comparison;

public sealed class CustomerCheckupComparisonService :
    ICustomerCheckupComparisonService
{
    private const double MaximumCoverageDifference = 0.15d;

    private readonly IDeviceIdentityService _deviceIdentityService;

    public CustomerCheckupComparisonService(
        IDeviceIdentityService deviceIdentityService)
    {
        ArgumentNullException.ThrowIfNull(deviceIdentityService);
        _deviceIdentityService = deviceIdentityService;
    }

    public CustomerCheckupComparison Compare(
        CustomerCheckupVisit customerCheckupVisit,
        CheckupSession workingCheckup,
        CheckupSession afterCheckup)
    {
        ArgumentNullException.ThrowIfNull(customerCheckupVisit);
        ArgumentNullException.ThrowIfNull(workingCheckup);
        ArgumentNullException.ThrowIfNull(afterCheckup);

        ValidateInput(
            customerCheckupVisit,
            workingCheckup,
            afterCheckup);

        EnsureSameDevice(
            customerCheckupVisit.BeforeCheckup,
            afterCheckup);

        var beforeCheckup = customerCheckupVisit.BeforeCheckup;
        var afterSnapshot = CheckupSnapshot.Capture(afterCheckup);
        var areas = CreateAreaDefinitions();

        var findingComparisons = CompareFindings(
            beforeCheckup,
            afterSnapshot,
            areas);

        var comparison = new CustomerCheckupComparison
        {
            CustomerCheckupVisitId = customerCheckupVisit.Id,
            CreatedAt = DateTime.Now,
            BeforeScanDate = beforeCheckup.ScanDate,
            AfterScanDate = afterSnapshot.ScanDate,
            BeforeScoringVersion = beforeCheckup.Assessment.ScoringVersion,
            AfterScoringVersion = afterSnapshot.Assessment.ScoringVersion,
            BeforeTaskListVersion = beforeCheckup.TaskList.TaskListVersion,
            WorkingTaskListVersion = workingCheckup.TaskList.TaskListVersion,
            AfterTaskListVersion = afterSnapshot.TaskList.TaskListVersion,
            SystemScore = CompareScore(
                CustomerCheckupScoreKind.System,
                beforeCheckup.Assessment.SystemCondition,
                afterSnapshot.Assessment.SystemCondition,
                beforeCheckup.Assessment.ScoringVersion,
                afterSnapshot.Assessment.ScoringVersion),
            HardwareScore = CompareScore(
                CustomerCheckupScoreKind.Hardware,
                beforeCheckup.Assessment.HardwareCondition,
                afterSnapshot.Assessment.HardwareCondition,
                beforeCheckup.Assessment.ScoringVersion,
                afterSnapshot.Assessment.ScoringVersion),
            Areas = CompareAreas(
                beforeCheckup,
                afterSnapshot,
                findingComparisons,
                areas),
            Findings = findingComparisons,
            Tasks = CompareTasks(
                beforeCheckup,
                workingCheckup,
                afterSnapshot,
                findingComparisons,
                areas),
            Actions = BuildActionSummaries(workingCheckup.TaskList)
        };

        comparison.ComparisonNotes = BuildComparisonNotes(comparison);
        return comparison;
    }

    private static void ValidateInput(
        CustomerCheckupVisit visit,
        CheckupSession workingCheckup,
        CheckupSession afterCheckup)
    {
        if (!visit.IsInProgress)
        {
            throw new InvalidOperationException(
                "Ein neuer Vorher-/Nachher-Vergleich kann nur "
                + "für einen laufenden Kundencheckup erstellt werden.");
        }

        if (!visit.BeforeCheckup.ScanDate.HasValue)
        {
            throw new InvalidOperationException(
                "Der laufende Kundencheckup enthält keinen "
                + "abgeschlossenen Eingangsscan.");
        }

        if (!afterCheckup.ScanDate.HasValue)
        {
            throw new ArgumentException(
                "Für den Vergleich ist ein vollständig "
                + "abgeschlossener Nachher-Scan erforderlich.",
                nameof(afterCheckup));
        }

        if (afterCheckup.ScanDate.Value < visit.BeforeCheckup.ScanDate.Value)
        {
            throw new ArgumentException(
                "Der Nachher-Scan darf zeitlich nicht vor dem "
                + "Eingangsscan liegen.",
                nameof(afterCheckup));
        }

        if (!workingCheckup.CustomerCheckupVisits.Any(
                currentVisit => currentVisit.Id == visit.Id))
        {
            throw new ArgumentException(
                "Der übergebene Arbeitsstand gehört nicht zum "
                + "laufenden Kundencheckup.",
                nameof(workingCheckup));
        }
    }

    private void EnsureSameDevice(
        CheckupSnapshot beforeCheckup,
        CheckupSession afterCheckup)
    {
        var referenceSession = beforeCheckup.RestoreAsSession();
        var referenceDevice = new CustomerDevice
        {
            DisplayName = string.IsNullOrWhiteSpace(
                referenceSession.DeviceInformation.Name)
                    ? "Referenzgerät"
                    : referenceSession.DeviceInformation.Name,
            CheckupSession = referenceSession
        };

        var match = _deviceIdentityService.FindMatchingDevice(
            new[] { referenceDevice },
            afterCheckup.DeviceInformation);

        if (match is null)
        {
            throw new InvalidOperationException(
                "Der Nachher-Scan konnte dem ursprünglichen Gerät "
                + "nicht eindeutig zugeordnet werden. Der Vergleich "
                + "wurde nicht erstellt.");
        }
    }

    private static List<CustomerCheckupFindingComparison> CompareFindings(
        CheckupSnapshot beforeCheckup,
        CheckupSnapshot afterCheckup,
        IReadOnlyList<AreaDefinition> areas)
    {
        var remainingBefore = GetFindings(beforeCheckup).ToList();
        var remainingAfter = GetFindings(afterCheckup).ToList();
        var comparisons = new List<CustomerCheckupFindingComparison>();

        MatchFindingGroups(
            remainingBefore,
            remainingAfter,
            finding => string.IsNullOrWhiteSpace(finding.Code)
                ? string.Empty
                : "code:" + NormalizeStableValue(finding.Code),
            CustomerCheckupMatchBasis.Code,
            beforeCheckup,
            afterCheckup,
            areas,
            comparisons);

        MatchFindingGroups(
            remainingBefore,
            remainingAfter,
            finding => string.IsNullOrWhiteSpace(finding.CauseGroup)
                ? string.Empty
                : $"cause:{finding.Category}:"
                  + NormalizeStableValue(finding.CauseGroup),
            CustomerCheckupMatchBasis.CauseGroup,
            beforeCheckup,
            afterCheckup,
            areas,
            comparisons);

        MatchFindingGroups(
            remainingBefore,
            remainingAfter,
            finding => BuildTitleKey(finding.Category, finding.Title),
            CustomerCheckupMatchBasis.CategoryAndTitle,
            beforeCheckup,
            afterCheckup,
            areas,
            comparisons);

        AddUnmatchedFindingGroups(
            remainingBefore,
            true,
            beforeCheckup,
            afterCheckup,
            areas,
            comparisons);

        AddUnmatchedFindingGroups(
            remainingAfter,
            false,
            beforeCheckup,
            afterCheckup,
            areas,
            comparisons);

        return comparisons
            .OrderBy(item => GetFindingStatusOrder(item.Status))
            .ThenByDescending(item =>
                GetSeverityOrder(item.AfterSeverity ?? item.BeforeSeverity))
            .ThenBy(item => item.Title,
                StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static void MatchFindingGroups(
        List<CheckupFinding> remainingBefore,
        List<CheckupFinding> remainingAfter,
        Func<CheckupFinding, string> keySelector,
        CustomerCheckupMatchBasis matchBasis,
        CheckupSnapshot beforeCheckup,
        CheckupSnapshot afterCheckup,
        IReadOnlyList<AreaDefinition> areas,
        ICollection<CustomerCheckupFindingComparison> comparisons)
    {
        var beforeGroups = CreateFindingGroups(
            remainingBefore,
            keySelector);

        var afterGroups = CreateFindingGroups(
            remainingAfter,
            keySelector);

        var matchedKeys = beforeGroups.Keys.Intersect(
            afterGroups.Keys,
            StringComparer.OrdinalIgnoreCase);

        foreach (var key in matchedKeys.ToList())
        {
            var beforeGroup = beforeGroups[key];
            var afterGroup = afterGroups[key];

            comparisons.Add(CreateFindingComparison(
                key,
                matchBasis,
                beforeGroup,
                afterGroup,
                beforeCheckup,
                afterCheckup,
                areas));

            RemoveFindings(remainingBefore, beforeGroup);
            RemoveFindings(remainingAfter, afterGroup);
        }
    }

    private static Dictionary<string, List<CheckupFinding>>
        CreateFindingGroups(
            IEnumerable<CheckupFinding> findings,
            Func<CheckupFinding, string> keySelector)
    {
        return findings
            .Select(finding => new
            {
                Finding = finding,
                Key = keySelector(finding)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(item => item.Key,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Finding).ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static void AddUnmatchedFindingGroups(
        IEnumerable<CheckupFinding> findings,
        bool isBeforeGroup,
        CheckupSnapshot beforeCheckup,
        CheckupSnapshot afterCheckup,
        IReadOnlyList<AreaDefinition> areas,
        ICollection<CustomerCheckupFindingComparison> comparisons)
    {
        foreach (var group in findings.GroupBy(
                     BuildBestFindingKey,
                     StringComparer.OrdinalIgnoreCase))
        {
            var groupedFindings = group.ToList();
            var matchBasis = GetBestMatchBasis(groupedFindings[0]);

            comparisons.Add(CreateFindingComparison(
                group.Key,
                matchBasis,
                isBeforeGroup
                    ? groupedFindings
                    : Array.Empty<CheckupFinding>(),
                isBeforeGroup
                    ? Array.Empty<CheckupFinding>()
                    : groupedFindings,
                beforeCheckup,
                afterCheckup,
                areas));
        }
    }

    private static CustomerCheckupFindingComparison
        CreateFindingComparison(
            string matchKey,
            CustomerCheckupMatchBasis matchBasis,
            IReadOnlyCollection<CheckupFinding> beforeFindings,
            IReadOnlyCollection<CheckupFinding> afterFindings,
            CheckupSnapshot beforeCheckup,
            CheckupSnapshot afterCheckup,
            IReadOnlyList<AreaDefinition> areas)
    {
        var representative = afterFindings
            .Concat(beforeFindings)
            .OrderByDescending(finding =>
                GetSeverityOrder(finding.Severity))
            .First();

        var beforeRepresentative = beforeFindings
            .OrderByDescending(finding =>
                GetSeverityOrder(finding.Severity))
            .FirstOrDefault();

        var afterRepresentative = afterFindings
            .OrderByDescending(finding =>
                GetSeverityOrder(finding.Severity))
            .FirstOrDefault();

        var area = ResolveArea(representative, areas);
        var beforeEvaluation = EvaluateFinding(
            beforeCheckup,
            representative,
            area);
        var afterEvaluation = EvaluateFinding(
            afterCheckup,
            representative,
            area);

        var status = DetermineFindingStatus(
            beforeFindings.Count,
            afterFindings.Count,
            afterEvaluation.IsEvaluable);

        return new CustomerCheckupFindingComparison
        {
            MatchKey = matchKey,
            MatchBasis = matchBasis,
            AreaCode = area.Code,
            Code = FirstNonEmpty(
                afterFindings.Select(item => item.Code)
                    .Concat(beforeFindings.Select(item => item.Code))),
            CauseGroup = FirstNonEmpty(
                afterFindings.Select(item => item.CauseGroup)
                    .Concat(beforeFindings.Select(item => item.CauseGroup))),
            Title = afterRepresentative?.Title
                    ?? beforeRepresentative?.Title
                    ?? representative.Title,
            BeforeDescription = beforeRepresentative?.Description
                                ?? string.Empty,
            AfterDescription = afterRepresentative?.Description
                               ?? string.Empty,
            Category = representative.Category,
            AssessmentTarget = representative.AssessmentTarget,
            BeforeSeverity = beforeRepresentative?.Severity,
            AfterSeverity = afterRepresentative?.Severity,
            BeforeOccurrenceCount = beforeFindings.Count,
            AfterOccurrenceCount = afterFindings.Count,
            WasBeforeAreaEvaluable = beforeEvaluation.IsEvaluable,
            IsAfterAreaEvaluable = afterEvaluation.IsEvaluable,
            Status = status
        };
    }

    private static CustomerCheckupFindingComparisonStatus
        DetermineFindingStatus(
            int beforeCount,
            int afterCount,
            bool isAfterEvaluable)
    {
        if (beforeCount > 0 && afterCount > 0)
        {
            return CustomerCheckupFindingComparisonStatus.StillOpen;
        }

        if (beforeCount > 0)
        {
            return isAfterEvaluable
                ? CustomerCheckupFindingComparisonStatus.Resolved
                : CustomerCheckupFindingComparisonStatus.NotReevaluatable;
        }

        return CustomerCheckupFindingComparisonStatus.NewlyDetected;
    }

    private static List<CustomerCheckupTaskComparison> CompareTasks(
        CheckupSnapshot beforeCheckup,
        CheckupSession workingCheckup,
        CheckupSnapshot afterCheckup,
        IReadOnlyCollection<CustomerCheckupFindingComparison> findings,
        IReadOnlyList<AreaDefinition> areas)
    {
        var sourceTasks = MergeSourceTasks(
            beforeCheckup.TaskList,
            workingCheckup.TaskList);

        var sourceGroups = sourceTasks
            .GroupBy(BuildTaskKey,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.ToList(),
                StringComparer.OrdinalIgnoreCase);

        var afterGroups = GetTasks(afterCheckup.TaskList)
            .GroupBy(BuildTaskKey,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.ToList(),
                StringComparer.OrdinalIgnoreCase);

        var keys = sourceGroups.Keys.Union(
            afterGroups.Keys,
            StringComparer.OrdinalIgnoreCase);

        var comparisons = new List<CustomerCheckupTaskComparison>();

        foreach (var key in keys)
        {
            sourceGroups.TryGetValue(key, out var sourceGroup);
            afterGroups.TryGetValue(key, out var afterGroup);

            comparisons.Add(CreateTaskComparison(
                key,
                sourceGroup ?? new List<CheckupTask>(),
                afterGroup ?? new List<CheckupTask>(),
                afterCheckup,
                findings,
                areas));
        }

        return comparisons
            .OrderBy(item => GetTaskStatusOrder(item.Status))
            .ThenByDescending(item => item.Priority)
            .ThenBy(item => item.Title,
                StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static CustomerCheckupTaskComparison CreateTaskComparison(
        string matchKey,
        IReadOnlyCollection<CheckupTask> sourceTasks,
        IReadOnlyCollection<CheckupTask> afterTasks,
        CheckupSnapshot afterCheckup,
        IReadOnlyCollection<CustomerCheckupFindingComparison> findings,
        IReadOnlyList<AreaDefinition> areas)
    {
        var sourceTask = sourceTasks
            .OrderByDescending(task =>
                task.StatusChangedAt ?? task.CreatedAt)
            .FirstOrDefault();

        var afterTask = afterTasks.FirstOrDefault();
        var representative = sourceTask ?? afterTask
            ?? throw new InvalidOperationException(
                "Der Aufgabenvergleich enthält keine Aufgabe.");

        var sourceFindingCodes = sourceTasks
            .SelectMany(task => task.SourceFindingCodes
                ?? new List<string>())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code,
                StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sourceCauseGroup = FirstNonEmpty(
            sourceTasks.Select(task => task.SourceCauseGroup));

        var area = ResolveArea(
            representative,
            sourceFindingCodes,
            sourceCauseGroup,
            findings,
            areas);

        var actionResults = sourceTasks
            .SelectMany(task => task.ActionResults
                ?? new List<CheckupTaskActionResult>())
            .ToList();

        var isRecurringMaintenanceTask =
            sourceFindingCodes.Count == 0
            && StartsWithCode(
                representative.TaskCode,
                "task.maintenance.");

        var isAfterEvaluable = area.Evaluate(afterCheckup).IsEvaluable;
        var status = DetermineTaskStatus(
            sourceTask,
            afterTasks.Count > 0,
            isRecurringMaintenanceTask,
            isAfterEvaluable);

        return new CustomerCheckupTaskComparison
        {
            MatchKey = matchKey,
            AreaCode = area.Code,
            TaskCode = FirstNonEmpty(
                sourceTasks.Select(task => task.TaskCode)
                    .Concat(afterTasks.Select(task => task.TaskCode))),
            Title = sourceTask?.Title ?? afterTask?.Title ?? string.Empty,
            Category = representative.Category,
            Priority = representative.Priority,
            WorkStatus = sourceTask?.Status,
            StatusReason = sourceTask?.StatusReason ?? string.Empty,
            TechnicianNote = sourceTask?.TechnicianNote ?? string.Empty,
            SourceFindingCodes = sourceFindingCodes,
            SourceCauseGroup = sourceCauseGroup,
            IsPresentAfterCheckup = afterTasks.Count > 0,
            SuccessfulActionCount = actionResults.Count(result =>
                result.Status == CheckupTaskActionStatus.Successful),
            FailedActionCount = actionResults.Count(result =>
                result.Status == CheckupTaskActionStatus.Failed),
            CancelledActionCount = actionResults.Count(result =>
                result.Status == CheckupTaskActionStatus.Cancelled),
            InconclusiveActionCount = actionResults.Count(result =>
                result.Status == CheckupTaskActionStatus.Unknown),
            RestartRequired = actionResults.Any(result =>
                result.RestartRequired),
            Status = status
        };
    }

    private static CustomerCheckupTaskComparisonStatus DetermineTaskStatus(
        CheckupTask? sourceTask,
        bool isPresentAfterCheckup,
        bool isRecurringMaintenanceTask,
        bool isAfterEvaluable)
    {
        if (sourceTask is null)
        {
            return isRecurringMaintenanceTask
                ? CustomerCheckupTaskComparisonStatus.StillOpen
                : CustomerCheckupTaskComparisonStatus.NewlyDetected;
        }

        if (sourceTask.Status == CheckupTaskStatus.Skipped)
        {
            return CustomerCheckupTaskComparisonStatus.Skipped;
        }

        if (sourceTask.Status == CheckupTaskStatus.NotFeasible)
        {
            return CustomerCheckupTaskComparisonStatus.NotFeasible;
        }

        if (isRecurringMaintenanceTask)
        {
            return sourceTask.Status == CheckupTaskStatus.Completed
                ? CustomerCheckupTaskComparisonStatus.Completed
                : CustomerCheckupTaskComparisonStatus.StillOpen;
        }

        if (isPresentAfterCheckup)
        {
            return sourceTask.Status == CheckupTaskStatus.Completed
                ? CustomerCheckupTaskComparisonStatus.CompletedButStillDetected
                : CustomerCheckupTaskComparisonStatus.StillOpen;
        }

        if (!isAfterEvaluable)
        {
            return CustomerCheckupTaskComparisonStatus.NotReevaluatable;
        }

        return sourceTask.Status == CheckupTaskStatus.Completed
            ? CustomerCheckupTaskComparisonStatus.Completed
            : CustomerCheckupTaskComparisonStatus.NoLongerDetected;
    }

    private static List<CustomerCheckupActionSummary> BuildActionSummaries(
        CheckupTaskList taskList)
    {
        return GetTasks(taskList)
            .SelectMany(task =>
                (task.ActionResults ?? new List<CheckupTaskActionResult>())
                .Select(result => new CustomerCheckupActionSummary
                {
                    TaskId = task.Id,
                    TaskCode = task.TaskCode,
                    TaskTitle = task.Title,
                    ActionResultId = result.Id,
                    ActionCode = result.ActionCode,
                    ActionTitle = result.ActionTitle,
                    TargetDescription = result.TargetDescription,
                    Status = result.Status,
                    Summary = result.Summary,
                    Details = result.Details,
                    ExitCode = result.ExitCode,
                    RestartRequired = result.RestartRequired,
                    RestartStatusWasConclusive =
                        result.RestartStatusWasConclusive,
                    StartedAt = result.StartedAt,
                    FinishedAt = result.FinishedAt
                }))
            .OrderBy(item => item.StartedAt ?? DateTimeOffset.MinValue)
            .ThenBy(item => item.TaskTitle,
                StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static List<CustomerCheckupAreaComparison> CompareAreas(
        CheckupSnapshot beforeCheckup,
        CheckupSnapshot afterCheckup,
        IReadOnlyCollection<CustomerCheckupFindingComparison> findings,
        IReadOnlyList<AreaDefinition> areas)
    {
        var result = new List<CustomerCheckupAreaComparison>();

        foreach (var area in areas)
        {
            var beforeEvaluation = area.Evaluate(beforeCheckup);
            var afterEvaluation = area.Evaluate(afterCheckup);

            var beforeCount = findings
                .Where(item => item.AreaCode == area.Code)
                .Where(item => item.BeforeSeverity.HasValue
                    && item.BeforeSeverity != FindingSeverity.Information)
                .Sum(item => item.BeforeOccurrenceCount);

            var afterCount = findings
                .Where(item => item.AreaCode == area.Code)
                .Where(item => item.AfterSeverity.HasValue
                    && item.AfterSeverity != FindingSeverity.Information)
                .Sum(item => item.AfterOccurrenceCount);

            result.Add(new CustomerCheckupAreaComparison
            {
                AreaCode = area.Code,
                Title = area.Title,
                WasBeforeEvaluable = beforeEvaluation.IsEvaluable,
                IsAfterEvaluable = afterEvaluation.IsEvaluable,
                BeforeEvaluationNote = beforeEvaluation.Note,
                AfterEvaluationNote = afterEvaluation.Note,
                BeforeActionableFindingCount = beforeCount,
                AfterActionableFindingCount = afterCount,
                Status = DetermineAreaStatus(
                    beforeEvaluation.IsEvaluable,
                    afterEvaluation.IsEvaluable,
                    beforeCount,
                    afterCount)
            });
        }

        return result;
    }

    private static CustomerCheckupAreaComparisonStatus DetermineAreaStatus(
        bool wasBeforeEvaluable,
        bool isAfterEvaluable,
        int beforeCount,
        int afterCount)
    {
        if (!wasBeforeEvaluable || !isAfterEvaluable)
        {
            return CustomerCheckupAreaComparisonStatus.NotComparable;
        }

        if (beforeCount == 0 && afterCount == 0)
        {
            return CustomerCheckupAreaComparisonStatus.UnchangedHealthy;
        }

        if (beforeCount == 0)
        {
            return CustomerCheckupAreaComparisonStatus.NewlyNeedsAttention;
        }

        if (afterCount == 0)
        {
            return CustomerCheckupAreaComparisonStatus.Improved;
        }

        if (afterCount < beforeCount)
        {
            return CustomerCheckupAreaComparisonStatus
                .ImprovedButStillNeedsAttention;
        }

        if (afterCount > beforeCount)
        {
            return CustomerCheckupAreaComparisonStatus.Worsened;
        }

        return CustomerCheckupAreaComparisonStatus
            .UnchangedNeedsAttention;
    }

    private static CustomerCheckupScoreComparison CompareScore(
        CustomerCheckupScoreKind kind,
        ConditionAssessment beforeAssessment,
        ConditionAssessment afterAssessment,
        int beforeScoringVersion,
        int afterScoringVersion)
    {
        var comparison = new CustomerCheckupScoreComparison
        {
            Kind = kind,
            BeforeScore = beforeAssessment.Score,
            AfterScore = afterAssessment.Score,
            BeforeRating = beforeAssessment.Rating,
            AfterRating = afterAssessment.Rating,
            BeforeDataQuality = beforeAssessment.DataQuality,
            AfterDataQuality = afterAssessment.DataQuality,
            BeforeEvaluatedAreaCount = beforeAssessment.EvaluatedAreaCount,
            BeforeAvailableAreaCount = beforeAssessment.AvailableAreaCount,
            AfterEvaluatedAreaCount = afterAssessment.EvaluatedAreaCount,
            AfterAvailableAreaCount = afterAssessment.AvailableAreaCount
        };

        var blockReason = GetScoreComparisonBlockReason(
            beforeAssessment,
            afterAssessment,
            beforeScoringVersion,
            afterScoringVersion);

        if (!string.IsNullOrWhiteSpace(blockReason))
        {
            comparison.IsComparable = false;
            comparison.Change = CustomerCheckupScoreChange.NotComparable;
            comparison.ComparisonReason = blockReason;
            return comparison;
        }

        var difference = afterAssessment.Score!.Value
                         - beforeAssessment.Score!.Value;

        comparison.IsComparable = true;
        comparison.Difference = difference;
        comparison.Change = difference switch
        {
            > 0 => CustomerCheckupScoreChange.Improved,
            < 0 => CustomerCheckupScoreChange.Worsened,
            _ => CustomerCheckupScoreChange.Unchanged
        };
        comparison.ComparisonReason =
            "Beide Werte stammen aus demselben Bewertungsmodell "
            + "und aus ausreichend vergleichbaren Datengrundlagen.";

        return comparison;
    }

    private static string GetScoreComparisonBlockReason(
        ConditionAssessment beforeAssessment,
        ConditionAssessment afterAssessment,
        int beforeScoringVersion,
        int afterScoringVersion)
    {
        if (beforeScoringVersion <= 0 || afterScoringVersion <= 0)
        {
            return "Mindestens ein Scan besitzt keine versionierte "
                   + "Bewertungsgrundlage.";
        }

        if (beforeScoringVersion != afterScoringVersion)
        {
            return "Die Scores wurden mit unterschiedlichen "
                   + "Bewertungsmodell-Versionen erzeugt.";
        }

        if (!beforeAssessment.Score.HasValue
            || !afterAssessment.Score.HasValue)
        {
            return "Mindestens einer der beiden Scores ist nicht "
                   + "verfügbar.";
        }

        if (!HasSufficientDataQuality(beforeAssessment.DataQuality)
            || !HasSufficientDataQuality(afterAssessment.DataQuality))
        {
            return "Mindestens ein Score beruht nicht auf einer "
                   + "ausreichenden Datengrundlage.";
        }

        if (beforeAssessment.EvaluatedAreaCount <= 0
            || afterAssessment.EvaluatedAreaCount <= 0)
        {
            return "Die Abdeckung der Bewertungsbereiche ist nicht "
                   + "ausreichend dokumentiert.";
        }

        if (beforeAssessment.EvaluatedAreaCount
            != afterAssessment.EvaluatedAreaCount)
        {
            return "Die Anzahl der bewerteten Bereiche unterscheidet "
                   + "sich zwischen Vorher- und Nachher-Scan.";
        }

        if (Math.Abs(beforeAssessment.AvailableAreaCount
                     - afterAssessment.AvailableAreaCount) > 1)
        {
            return "Die Anzahl der tatsächlich auswertbaren Bereiche "
                   + "unterscheidet sich zu stark.";
        }

        var beforeCoverage = (double)beforeAssessment.AvailableAreaCount
                             / beforeAssessment.EvaluatedAreaCount;
        var afterCoverage = (double)afterAssessment.AvailableAreaCount
                            / afterAssessment.EvaluatedAreaCount;

        if (Math.Abs(beforeCoverage - afterCoverage)
            > MaximumCoverageDifference)
        {
            return "Die Datenabdeckung der beiden Scans ist nicht "
                   + "ausreichend ähnlich.";
        }

        return string.Empty;
    }

    private static List<string> BuildComparisonNotes(
        CustomerCheckupComparison comparison)
    {
        var notes = new List<string>();

        var fallbackCount = comparison.Findings.Count(finding =>
            finding.MatchBasis != CustomerCheckupMatchBasis.Code);

        if (fallbackCount > 0)
        {
            notes.Add(
                $"{fallbackCount} Befundvergleiche konnten nicht "
                + "ausschließlich über einen stabilen Befundcode "
                + "zugeordnet werden.");
        }

        if (!comparison.SystemScore.IsComparable)
        {
            notes.Add(
                "Der Systemscore ist nicht direkt vergleichbar: "
                + comparison.SystemScore.ComparisonReason);
        }

        if (!comparison.HardwareScore.IsComparable)
        {
            notes.Add(
                "Der Hardwarezustand ist nicht direkt vergleichbar: "
                + comparison.HardwareScore.ComparisonReason);
        }

        if (comparison.BeforeTaskListVersion
            != comparison.WorkingTaskListVersion
            || comparison.WorkingTaskListVersion
            != comparison.AfterTaskListVersion)
        {
            notes.Add(
                "Die Aufgabenlisten stammen aus unterschiedlichen "
                + "Modellständen. Stabile Aufgabencodes wurden "
                + "bevorzugt; ältere Aufgaben können eingeschränkt "
                + "vergleichbar sein.");
        }

        var unavailableAreas = comparison.Areas
            .Where(area =>
                area.Status == CustomerCheckupAreaComparisonStatus
                    .NotComparable)
            .Select(area => area.Title)
            .ToList();

        if (unavailableAreas.Count > 0)
        {
            notes.Add(
                "Nicht vollständig erneut auswertbar: "
                + string.Join(", ", unavailableAreas)
                + ".");
        }

        var uncertainNewFindings = comparison.Findings.Count(finding =>
            finding.Status
                == CustomerCheckupFindingComparisonStatus.NewlyDetected
            && !finding.WasBeforeAreaEvaluable);

        if (uncertainNewFindings > 0)
        {
            notes.Add(
                $"Bei {uncertainNewFindings} neu erkannten Befunden "
                + "war der betreffende Bereich im Eingangsscan "
                + "nicht belastbar auswertbar. ‚Neu erkannt‘ "
                + "bedeutet deshalb nicht zwingend, dass der Zustand "
                + "erst während des Checkups entstanden ist.");
        }

        if (comparison.FailedActionCount > 0)
        {
            notes.Add(
                $"{comparison.FailedActionCount} technische Aktionen "
                + "sind fehlgeschlagen.");
        }

        if (comparison.CancelledActionCount > 0)
        {
            notes.Add(
                $"{comparison.CancelledActionCount} technische "
                + "Aktionen wurden abgebrochen.");
        }

        if (comparison.HasRestartRequirement)
        {
            notes.Add(
                "Mindestens eine technische Aktion meldet "
                + "Neustartbedarf.");
        }

        return notes;
    }

    private static IReadOnlyList<AreaDefinition> CreateAreaDefinitions()
    {
        return new[]
        {
            new AreaDefinition(
                "windows-update",
                "Windows Update",
                snapshot => snapshot.WindowsUpdateInformation
                        .IsUpdateSearchPerformed
                    && snapshot.WindowsUpdateInformation
                        .IsUpdateSearchSuccessful
                        ? AreaEvaluation.Available(
                            "Windows-Update-Suche erfolgreich")
                        : AreaEvaluation.Unavailable(
                            "Windows-Update-Suche nicht erfolgreich"),
                finding => StartsWithCode(
                    finding.Code,
                    "system.windows-update."),
                task => task.Category
                    == CheckupTaskCategory.WindowsUpdate),

            new AreaDefinition(
                "program-updates",
                "Programmupdates",
                snapshot => snapshot.ProgramUpdateInformation
                        .IsAnalysisPerformed
                    && snapshot.ProgramUpdateInformation
                        .IsAnalysisSuccessful
                        ? AreaEvaluation.Available(
                            "Programmupdate-Analyse erfolgreich")
                        : AreaEvaluation.Unavailable(
                            "Programmupdate-Analyse nicht erfolgreich"),
                finding => StartsWithCode(
                    finding.Code,
                    "system.program-updates."),
                task => task.Category
                    == CheckupTaskCategory.ProgramUpdates),

            new AreaDefinition(
                "restart",
                "Neustartstatus",
                snapshot => snapshot.RestartInformation
                        .IsAnalysisPerformed
                    && snapshot.RestartInformation
                        .IsAnalysisConclusive
                        ? AreaEvaluation.Available(
                            "Neustartstatus eindeutig ermittelt")
                        : AreaEvaluation.Unavailable(
                            "Neustartstatus nicht eindeutig ermittelt"),
                finding => StartsWithCode(
                    finding.Code,
                    "system.restart."),
                task => task.Category
                    == CheckupTaskCategory.Restart),

            new AreaDefinition(
                "cleanup",
                "Bereinigungspotenzial",
                EvaluateCleanup,
                finding => StartsWithCode(
                    finding.Code,
                    "system.cleanup."),
                task => StartsWithCode(
                        task.TaskCode,
                        "task.storage.controlled-cleanup")
                    || (task.SourceFindingCodes
                        ?? new List<string>()).Any(code =>
                            StartsWithCode(code, "system.cleanup."))),

            new AreaDefinition(
                "startup",
                "Autostart",
                snapshot => snapshot.StartupInformation.AnalysisStatus
                    == StartupAnalysisStatus.Analyzed
                        ? AreaEvaluation.Available(
                            "Autostart vollständig analysiert")
                        : AreaEvaluation.Unavailable(
                            "Autostart nicht vollständig analysiert"),
                finding => StartsWithCode(
                    finding.Code,
                    "system.startup."),
                task => task.Category
                    == CheckupTaskCategory.Performance),

            new AreaDefinition(
                "devices-and-drivers",
                "Geräte und Treiber",
                snapshot => snapshot.DeviceDriverInformation.AnalysisStatus
                    == DeviceDriverAnalysisStatus.Analyzed
                        ? AreaEvaluation.Available(
                            "Geräte und Treiber vollständig analysiert")
                        : AreaEvaluation.Unavailable(
                            "Geräte und Treiber nicht vollständig analysiert"),
                finding => StartsWithCode(
                    finding.Code,
                    "system.devices."),
                task => task.Category
                    == CheckupTaskCategory.DevicesAndDrivers),

            new AreaDefinition(
                "storage",
                "Datenträger und Speicherplatz",
                EvaluateStorage,
                finding => StartsWithCode(
                        finding.Code,
                        "hardware.storage.")
                    || StartsWithCode(
                        finding.Code,
                        "system.storage."),
                task => task.Category == CheckupTaskCategory.Storage),

            new AreaDefinition(
                "security",
                "Sicherheit",
                EvaluateSecurity,
                finding => finding.Category == FindingCategory.Security
                    || StartsWithCode(
                        finding.Code,
                        "system.security."),
                task => task.Category == CheckupTaskCategory.Security),

            new AreaDefinition(
                "operating-system",
                "Betriebssystem",
                EvaluateOperatingSystem,
                finding => finding.Category
                        == FindingCategory.OperatingSystem
                    || StartsWithCode(
                        finding.Code,
                        "system.operating-system."),
                task => task.Category
                    == CheckupTaskCategory.OperatingSystem),

            new AreaDefinition(
                "hardware",
                "Hardware",
                EvaluateHardware,
                finding => finding.Category == FindingCategory.Hardware,
                task => task.Category == CheckupTaskCategory.Hardware),

            new AreaDefinition(
                "general",
                "Allgemeine Bewertung",
                EvaluateGeneral,
                finding => true,
                task => task.Category == CheckupTaskCategory.General)
        };
    }

    private static AreaDefinition ResolveArea(
        CheckupFinding finding,
        IReadOnlyList<AreaDefinition> areas)
    {
        return areas.First(area => area.MatchesFinding(finding));
    }

    private static AreaDefinition ResolveArea(
        CheckupTask task,
        IReadOnlyCollection<string> sourceFindingCodes,
        string sourceCauseGroup,
        IReadOnlyCollection<CustomerCheckupFindingComparison> findings,
        IReadOnlyList<AreaDefinition> areas)
    {
        var relatedFinding = findings.FirstOrDefault(finding =>
            sourceFindingCodes.Contains(
                finding.Code,
                StringComparer.OrdinalIgnoreCase));

        relatedFinding ??= findings.FirstOrDefault(finding =>
            !string.IsNullOrWhiteSpace(sourceCauseGroup)
            && string.Equals(
                finding.CauseGroup,
                sourceCauseGroup,
                StringComparison.OrdinalIgnoreCase));

        if (relatedFinding is not null)
        {
            return areas.First(area => area.Code == relatedFinding.AreaCode);
        }

        return areas.First(area => area.MatchesTask(task));
    }

    private static AreaEvaluation EvaluateFinding(
        CheckupSnapshot snapshot,
        CheckupFinding finding,
        AreaDefinition fallbackArea)
    {
        var code = finding.Code;
        var security = snapshot.SecurityInformation;

        if (StartsWithCode(code, "system.security.antivirus-"))
        {
            return security.AntivirusStatus != SecurityState.Unknown
                ? AreaEvaluation.Available("Virenschutzstatus ermittelt")
                : AreaEvaluation.Unavailable("Virenschutzstatus unbekannt");
        }

        if (StartsWithCode(code, "system.security.firewall-"))
        {
            return (security.FirewallProfiles?.Count ?? 0) > 0
                ? AreaEvaluation.Available("Firewallprofile ermittelt")
                : AreaEvaluation.Unavailable("Firewallprofile nicht ermittelt");
        }

        if (StartsWithCode(code, "system.security.uac-"))
        {
            return security.UserAccountControlStatus
                != SecurityState.Unknown
                ? AreaEvaluation.Available("UAC-Status ermittelt")
                : AreaEvaluation.Unavailable("UAC-Status unbekannt");
        }

        if (StartsWithCode(code, "system.security.security-center-"))
        {
            return security.WindowsSecurityCenterStatus
                != SecurityState.Unknown
                ? AreaEvaluation.Available(
                    "Windows-Sicherheitscenter ermittelt")
                : AreaEvaluation.Unavailable(
                    "Windows-Sicherheitscenter unbekannt");
        }

        if (StartsWithCode(code, "system.security.secure-boot-"))
        {
            return security.SecureBootStatus != SecurityState.Unknown
                ? AreaEvaluation.Available("Secure-Boot-Status ermittelt")
                : AreaEvaluation.Unavailable("Secure-Boot-Status unbekannt");
        }

        if (StartsWithCode(code, "system.security.drive-encryption-")
            || StartsWithCode(
                code,
                "system.security.mobile-drive-")
            || StartsWithCode(
                code,
                "system.security.stationary-drive-"))
        {
            return security.SystemDriveEncryption.ProtectionState
                != SecurityState.Unknown
                ? AreaEvaluation.Available(
                    "Laufwerksverschlüsselung ermittelt")
                : AreaEvaluation.Unavailable(
                    "Laufwerksverschlüsselung unbekannt");
        }

        if (StartsWithCode(code, "hardware.memory."))
        {
            return HasMeaningfulValue(
                snapshot.HardwareInformation.InstalledMemory)
                ? AreaEvaluation.Available(
                    "Arbeitsspeicher ermittelt")
                : AreaEvaluation.Unavailable(
                    "Arbeitsspeicher nicht ermittelt");
        }

        if (StartsWithCode(code, "hardware.tpm.")
            || StartsWithCode(code, "system.security.tpm-"))
        {
            var hardware = snapshot.HardwareInformation;
            return HasMeaningfulValue(hardware.TpmStatus)
                   || HasMeaningfulValue(hardware.TpmVersion)
                ? AreaEvaluation.Available("TPM-Status ermittelt")
                : AreaEvaluation.Unavailable("TPM-Status unbekannt");
        }

        return fallbackArea.Evaluate(snapshot);
    }

    private static AreaEvaluation EvaluateHardware(
        CheckupSnapshot snapshot)
    {
        var condition = snapshot.Assessment.HardwareCondition;
        var hardware = snapshot.HardwareInformation;

        var hasData = condition.AvailableAreaCount > 0
            || HasMeaningfulValue(hardware.ProcessorName)
            || HasMeaningfulValue(hardware.InstalledMemory)
            || HasMeaningfulValue(hardware.MainboardManufacturer)
            || HasMeaningfulValue(hardware.MainboardProduct)
            || (hardware.GraphicsCards?.Count ?? 0) > 0;

        return hasData
            ? AreaEvaluation.Available("Hardwaredaten vorhanden")
            : AreaEvaluation.Unavailable("Hardwaredaten nicht verfügbar");
    }

    private static AreaEvaluation EvaluateOperatingSystem(
        CheckupSnapshot snapshot)
    {
        var operatingSystem = snapshot.OperatingSystemInformation;
        var hasData = HasMeaningfulValue(operatingSystem.Name)
            || HasMeaningfulValue(operatingSystem.Version)
            || HasMeaningfulValue(operatingSystem.BuildNumber);

        return hasData
            ? AreaEvaluation.Available("Betriebssystemdaten vorhanden")
            : AreaEvaluation.Unavailable(
                "Betriebssystemdaten nicht verfügbar");
    }

    private static AreaEvaluation EvaluateSecurity(
        CheckupSnapshot snapshot)
    {
        var security = snapshot.SecurityInformation;
        var hasData = security.AntivirusStatus != SecurityState.Unknown
            || security.UserAccountControlStatus != SecurityState.Unknown
            || security.SecureBootStatus != SecurityState.Unknown
            || security.WindowsSecurityCenterStatus != SecurityState.Unknown
            || security.SystemDriveEncryption.ProtectionState
                != SecurityState.Unknown
            || (security.AntivirusProducts?.Count ?? 0) > 0
            || (security.FirewallProfiles?.Count ?? 0) > 0;

        return hasData
            ? AreaEvaluation.Available("Sicherheitsdaten vorhanden")
            : AreaEvaluation.Unavailable(
                "Sicherheitsdaten nicht verfügbar");
    }

    private static AreaEvaluation EvaluateStorage(
        CheckupSnapshot snapshot)
    {
        var storage = snapshot.StorageInformation;
        var hasData = storage.IsAnalysisSuccessful
            && ((storage.PhysicalDrives?.Count ?? 0) > 0
                || (storage.Volumes?.Count ?? 0) > 0);

        return hasData
            ? AreaEvaluation.Available(
                "Datenträgeranalyse erfolgreich")
            : AreaEvaluation.Unavailable(
                "Datenträgeranalyse nicht erfolgreich");
    }

    private static AreaEvaluation EvaluateCleanup(
        CheckupSnapshot snapshot)
    {
        return snapshot.CleanupPotentialInformation.AnalysisStatus switch
        {
            CleanupMeasurementStatus.Measured =>
                AreaEvaluation.Available(
                    "Bereinigungspotenzial vollständig analysiert"),
            CleanupMeasurementStatus.InformationOnly =>
                AreaEvaluation.Available(
                    "Bereinigungsbereich ohne Messbedarf ausgewertet"),
            _ => AreaEvaluation.Unavailable(
                "Bereinigungspotenzial nicht vollständig auswertbar")
        };
    }

    private static AreaEvaluation EvaluateGeneral(
        CheckupSnapshot snapshot)
    {
        return snapshot.Assessment.ScoringVersion > 0
               && snapshot.Assessment.AssessmentCreatedAt.HasValue
            ? AreaEvaluation.Available(
                "Versionierte Bewertung vorhanden")
            : AreaEvaluation.Unavailable(
                "Versionierte Bewertung nicht verfügbar");
    }

    private static List<CheckupTask> MergeSourceTasks(
        CheckupTaskList beforeTaskList,
        CheckupTaskList workingTaskList)
    {
        var tasks = GetTasks(workingTaskList).ToList();
        var keys = tasks.Select(BuildTaskKey).ToHashSet(
            StringComparer.OrdinalIgnoreCase);

        foreach (var task in GetTasks(beforeTaskList))
        {
            if (keys.Add(BuildTaskKey(task)))
            {
                tasks.Add(task);
            }
        }

        return tasks;
    }

    private static IEnumerable<CheckupFinding> GetFindings(
        CheckupSnapshot snapshot)
    {
        return snapshot.Assessment.Findings
               ?? Enumerable.Empty<CheckupFinding>();
    }

    private static IEnumerable<CheckupTask> GetTasks(
        CheckupTaskList taskList)
    {
        return taskList.Tasks ?? Enumerable.Empty<CheckupTask>();
    }

    private static string BuildBestFindingKey(CheckupFinding finding)
    {
        if (!string.IsNullOrWhiteSpace(finding.Code))
        {
            return "code:" + NormalizeStableValue(finding.Code);
        }

        if (!string.IsNullOrWhiteSpace(finding.CauseGroup))
        {
            return $"cause:{finding.Category}:"
                   + NormalizeStableValue(finding.CauseGroup);
        }

        return BuildTitleKey(finding.Category, finding.Title);
    }

    private static CustomerCheckupMatchBasis GetBestMatchBasis(
        CheckupFinding finding)
    {
        if (!string.IsNullOrWhiteSpace(finding.Code))
        {
            return CustomerCheckupMatchBasis.Code;
        }

        return !string.IsNullOrWhiteSpace(finding.CauseGroup)
            ? CustomerCheckupMatchBasis.CauseGroup
            : CustomerCheckupMatchBasis.CategoryAndTitle;
    }

    private static string BuildTitleKey(
        FindingCategory category,
        string title)
    {
        return $"title:{category}:" + NormalizeDisplayValue(title);
    }

    private static string BuildTaskKey(CheckupTask task)
    {
        return !string.IsNullOrWhiteSpace(task.TaskCode)
            ? "code:" + NormalizeStableValue(task.TaskCode)
            : $"legacy:{task.Category}:"
              + NormalizeDisplayValue(task.Title);
    }

    private static string NormalizeStableValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private static string NormalizeDisplayValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(
                ' ',
                value.Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();
    }

    private static string FirstNonEmpty(IEnumerable<string?> values)
    {
        return values.FirstOrDefault(value =>
                   !string.IsNullOrWhiteSpace(value))
               ?.Trim()
               ?? string.Empty;
    }

    private static bool StartsWithCode(
        string? value,
        string prefix)
    {
        return !string.IsNullOrWhiteSpace(value)
               && value.StartsWith(
                   prefix,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasMeaningfulValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        return !normalized.Equals("Unknown",
                   StringComparison.OrdinalIgnoreCase)
               && !normalized.Equals("Unbekannt",
                   StringComparison.OrdinalIgnoreCase)
               && !normalized.Contains("nicht erkannt",
                   StringComparison.OrdinalIgnoreCase)
               && !normalized.Equals("N/A",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasSufficientDataQuality(
        AssessmentDataQuality dataQuality)
    {
        return dataQuality is AssessmentDataQuality.Sufficient
            or AssessmentDataQuality.Good;
    }

    private static void RemoveFindings(
        ICollection<CheckupFinding> source,
        IEnumerable<CheckupFinding> findings)
    {
        foreach (var finding in findings)
        {
            source.Remove(finding);
        }
    }

    private static int GetSeverityOrder(FindingSeverity? severity)
    {
        return severity switch
        {
            FindingSeverity.Critical => 3,
            FindingSeverity.Warning => 2,
            FindingSeverity.Recommendation => 1,
            _ => 0
        };
    }

    private static int GetFindingStatusOrder(
        CustomerCheckupFindingComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupFindingComparisonStatus.NewlyDetected => 0,
            CustomerCheckupFindingComparisonStatus.StillOpen => 1,
            CustomerCheckupFindingComparisonStatus.NotReevaluatable => 2,
            _ => 3
        };
    }

    private static int GetTaskStatusOrder(
        CustomerCheckupTaskComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupTaskComparisonStatus.NewlyDetected => 0,
            CustomerCheckupTaskComparisonStatus
                .CompletedButStillDetected => 1,
            CustomerCheckupTaskComparisonStatus.StillOpen => 2,
            CustomerCheckupTaskComparisonStatus.NotFeasible => 3,
            CustomerCheckupTaskComparisonStatus.Skipped => 4,
            CustomerCheckupTaskComparisonStatus.NotReevaluatable => 5,
            CustomerCheckupTaskComparisonStatus.NoLongerDetected => 6,
            _ => 7
        };
    }

    private sealed class AreaDefinition
    {
        public AreaDefinition(
            string code,
            string title,
            Func<CheckupSnapshot, AreaEvaluation> evaluate,
            Func<CheckupFinding, bool> matchesFinding,
            Func<CheckupTask, bool> matchesTask)
        {
            Code = code;
            Title = title;
            Evaluate = evaluate;
            MatchesFinding = matchesFinding;
            MatchesTask = matchesTask;
        }

        public string Code { get; }

        public string Title { get; }

        public Func<CheckupSnapshot, AreaEvaluation> Evaluate { get; }

        public Func<CheckupFinding, bool> MatchesFinding { get; }

        public Func<CheckupTask, bool> MatchesTask { get; }
    }

    private readonly record struct AreaEvaluation(
        bool IsEvaluable,
        string Note)
    {
        public static AreaEvaluation Available(string note) =>
            new(true, note);

        public static AreaEvaluation Unavailable(string note) =>
            new(false, note);
    }
}
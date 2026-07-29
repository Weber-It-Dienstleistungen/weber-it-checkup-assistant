using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public sealed class CustomerCheckupComparison
{
    public int ComparisonModelVersion { get; set; } = 1;

    public Guid CustomerCheckupVisitId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? BeforeScanDate { get; set; }

    public DateTime? AfterScanDate { get; set; }

    public int BeforeScoringVersion { get; set; }

    public int AfterScoringVersion { get; set; }

    public int BeforeTaskListVersion { get; set; }

    public int WorkingTaskListVersion { get; set; }

    public int AfterTaskListVersion { get; set; }

    public CustomerCheckupScoreComparison SystemScore { get; set; } = new();

    public CustomerCheckupScoreComparison HardwareScore { get; set; } = new();

    public List<CustomerCheckupAreaComparison> Areas { get; set; } = new();

    public List<CustomerCheckupFindingComparison> Findings { get; set; } = new();

    public List<CustomerCheckupTaskComparison> Tasks { get; set; } = new();

    public List<CustomerCheckupActionSummary> Actions { get; set; } = new();

    public List<string> ComparisonNotes { get; set; } = new();

    [JsonIgnore]
    public int ResolvedFindingCount =>
        Findings.Count(finding =>
            finding.Status == CustomerCheckupFindingComparisonStatus.Resolved);

    [JsonIgnore]
    public int StillOpenFindingCount =>
        Findings.Count(finding =>
            finding.Status == CustomerCheckupFindingComparisonStatus.StillOpen);

    [JsonIgnore]
    public int NewlyDetectedFindingCount =>
        Findings.Count(finding =>
            finding.Status == CustomerCheckupFindingComparisonStatus.NewlyDetected);

    [JsonIgnore]
    public int NotReevaluatableFindingCount =>
        Findings.Count(finding =>
            finding.Status == CustomerCheckupFindingComparisonStatus.NotReevaluatable);

    [JsonIgnore]
    public int SuccessfulActionCount =>
        Actions.Count(action =>
            action.Status == CheckupTaskActionStatus.Successful);

    [JsonIgnore]
    public int FailedActionCount =>
        Actions.Count(action =>
            action.Status == CheckupTaskActionStatus.Failed);

    [JsonIgnore]
    public int CancelledActionCount =>
        Actions.Count(action =>
            action.Status == CheckupTaskActionStatus.Cancelled);

    [JsonIgnore]
    public bool HasRestartRequirement =>
        Actions.Any(action => action.RestartRequired);
}

public sealed class CustomerCheckupFindingComparison
{
    public string MatchKey { get; set; } = string.Empty;

    public CustomerCheckupMatchBasis MatchBasis { get; set; } =
        CustomerCheckupMatchBasis.Code;

    public string AreaCode { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string CauseGroup { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string BeforeDescription { get; set; } = string.Empty;

    public string AfterDescription { get; set; } = string.Empty;

    public FindingCategory Category { get; set; }

    public FindingAssessmentTarget AssessmentTarget { get; set; } =
        FindingAssessmentTarget.InformationOnly;

    public FindingSeverity? BeforeSeverity { get; set; }

    public FindingSeverity? AfterSeverity { get; set; }

    public int BeforeOccurrenceCount { get; set; }

    public int AfterOccurrenceCount { get; set; }

    public bool WasBeforeAreaEvaluable { get; set; }

    public bool IsAfterAreaEvaluable { get; set; }

    public CustomerCheckupFindingComparisonStatus Status { get; set; }
}

public sealed class CustomerCheckupTaskComparison
{
    public string MatchKey { get; set; } = string.Empty;

    public string AreaCode { get; set; } = string.Empty;

    public string TaskCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public CheckupTaskCategory Category { get; set; } =
        CheckupTaskCategory.General;

    public CheckupTaskPriority Priority { get; set; } =
        CheckupTaskPriority.Optional;

    public CheckupTaskStatus? WorkStatus { get; set; }

    public string StatusReason { get; set; } = string.Empty;

    public string TechnicianNote { get; set; } = string.Empty;

    public List<string> SourceFindingCodes { get; set; } = new();

    public string SourceCauseGroup { get; set; } = string.Empty;

    public bool IsPresentAfterCheckup { get; set; }

    public int SuccessfulActionCount { get; set; }

    public int FailedActionCount { get; set; }

    public int CancelledActionCount { get; set; }

    public int InconclusiveActionCount { get; set; }

    public bool RestartRequired { get; set; }

    public CustomerCheckupTaskComparisonStatus Status { get; set; }
}

public sealed class CustomerCheckupActionSummary
{
    public Guid TaskId { get; set; }

    public string TaskCode { get; set; } = string.Empty;

    public string TaskTitle { get; set; } = string.Empty;

    public Guid ActionResultId { get; set; }

    public string ActionCode { get; set; } = string.Empty;

    public string ActionTitle { get; set; } = string.Empty;

    public string TargetDescription { get; set; } = string.Empty;

    public CheckupTaskActionStatus Status { get; set; } =
        CheckupTaskActionStatus.Unknown;

    public string Summary { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;

    public int? ExitCode { get; set; }

    public bool RestartRequired { get; set; }

    public bool RestartStatusWasConclusive { get; set; } = true;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }
}

public sealed class CustomerCheckupScoreComparison
{
    public CustomerCheckupScoreKind Kind { get; set; }

    public int? BeforeScore { get; set; }

    public int? AfterScore { get; set; }

    public ConditionRating BeforeRating { get; set; } =
        ConditionRating.NotAvailable;

    public ConditionRating AfterRating { get; set; } =
        ConditionRating.NotAvailable;

    public AssessmentDataQuality BeforeDataQuality { get; set; } =
        AssessmentDataQuality.NotAvailable;

    public AssessmentDataQuality AfterDataQuality { get; set; } =
        AssessmentDataQuality.NotAvailable;

    public int BeforeEvaluatedAreaCount { get; set; }

    public int BeforeAvailableAreaCount { get; set; }

    public int AfterEvaluatedAreaCount { get; set; }

    public int AfterAvailableAreaCount { get; set; }

    public bool IsComparable { get; set; }

    public int? Difference { get; set; }

    public CustomerCheckupScoreChange Change { get; set; } =
        CustomerCheckupScoreChange.NotComparable;

    public string ComparisonReason { get; set; } = string.Empty;
}

public sealed class CustomerCheckupAreaComparison
{
    public string AreaCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public bool WasBeforeEvaluable { get; set; }

    public bool IsAfterEvaluable { get; set; }

    public string BeforeEvaluationNote { get; set; } = string.Empty;

    public string AfterEvaluationNote { get; set; } = string.Empty;

    public int BeforeActionableFindingCount { get; set; }

    public int AfterActionableFindingCount { get; set; }

    public CustomerCheckupAreaComparisonStatus Status { get; set; }
}

public enum CustomerCheckupFindingComparisonStatus
{
    Resolved,
    StillOpen,
    NewlyDetected,
    NotReevaluatable
}

public enum CustomerCheckupTaskComparisonStatus
{
    Completed,
    CompletedButStillDetected,
    StillOpen,
    NoLongerDetected,
    Skipped,
    NotFeasible,
    NewlyDetected,
    NotReevaluatable
}

public enum CustomerCheckupAreaComparisonStatus
{
    NotComparable,
    UnchangedHealthy,
    Improved,
    ImprovedButStillNeedsAttention,
    UnchangedNeedsAttention,
    Worsened,
    NewlyNeedsAttention
}

public enum CustomerCheckupMatchBasis
{
    Code,
    CauseGroup,
    CategoryAndTitle
}

public enum CustomerCheckupScoreKind
{
    System,
    Hardware
}

public enum CustomerCheckupScoreChange
{
    NotComparable,
    Unchanged,
    Improved,
    Worsened
}
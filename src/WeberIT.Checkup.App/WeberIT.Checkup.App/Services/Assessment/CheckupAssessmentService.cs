
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;
using WeberIT.Checkup.App.Services.Tasks;

namespace WeberIT.Checkup.App.Services.Assessment;

public class CheckupAssessmentService :
    ICheckupAssessmentService
{
    /*
     * SCHRITT 48:
     *
     * Version 2 kennzeichnet den aktuellen Stand
     * des Bewertungsmodells einschließlich der
     * differenzierten Berücksichtigung von
     * Windows- und Programmaktualisierungen.
     *
     * Frühere Checkups mit Version 1 bleiben
     * unverändert gespeichert.
     *
     * Die Vorher-/Nachher-Vergleichslogik
     * untersagt bereits den direkten Vergleich
     * von Scores unterschiedlicher Versionen.
     *
     * WICHTIG FÜR KÜNFTIGE ENTWICKLUNG:
     *
     * Wird die fachliche Punkteberechnung
     * verändert, muss geprüft werden, ob
     * diese Modellversion erneut erhöht
     * werden muss.
     *
     * Das betrifft insbesondere:
     *
     * - Gewichtungen der Bewertungsbereiche
     * - Punktabzüge und Score-Obergrenzen
     * - fachliche Bewertung von Befunden
     * - Verfügbarkeit von Bewertungsbereichen
     * - Regeln für die Datenqualität
     *
     * Reine Anzeigeänderungen oder technische
     * Fehlerdiagnosen begründen dagegen nicht
     * automatisch eine neue Scoring-Version.
     */
    // Version 3 beruecksichtigt nachgewiesenen Cleanup-Arbeitsabschluss.
    // Laufende V2-Vorgaenge behalten die unveraenderte V2-Bewertung.
    private const int CurrentScoringVersion = 3;

    private readonly IEnumerable<ICheckupAssessmentRule>
        _assessmentRules;

    private readonly ISystemConditionAssessmentService
        _systemConditionAssessmentService;

    private readonly IHardwareConditionAssessmentService
        _hardwareConditionAssessmentService;

    public CheckupAssessmentService(
        IEnumerable<ICheckupAssessmentRule> assessmentRules,
        ISystemConditionAssessmentService
            systemConditionAssessmentService,
        IHardwareConditionAssessmentService
            hardwareConditionAssessmentService)
    {
        _assessmentRules =
            assessmentRules;

        _systemConditionAssessmentService =
            systemConditionAssessmentService;

        _hardwareConditionAssessmentService =
            hardwareConditionAssessmentService;
    }

    public CheckupAssessment Assess(
        CheckupSession checkupSession)
    {
        return AssessCore(checkupSession, CurrentScoringVersion, null);
    }

    public CheckupAssessment AssessCompletion(
        CheckupSession afterCheckup,
        CheckupSession workingCheckup,
        CustomerCheckupVisit visit,
        DateTimeOffset scanStartedAt)
    {
        ArgumentNullException.ThrowIfNull(afterCheckup);
        ArgumentNullException.ThrowIfNull(workingCheckup);
        ArgumentNullException.ThrowIfNull(visit);

        if (ReferenceEquals(afterCheckup, workingCheckup)
            || !ReferenceEquals(workingCheckup.CurrentCustomerCheckupVisit, visit)
            || visit.Id == Guid.Empty
            || !visit.IsInProgress
            || visit.IsCompletionPrepared
            || visit.BeforeCheckup?.ScanDate is not DateTime entranceDate
            || afterCheckup.ScanDate is not DateTime afterDate
            || ToOffset(entranceDate) >= scanStartedAt
            || ToOffset(afterDate) < scanStartedAt)
        {
            throw new InvalidOperationException(
                "Die Abschlussbewertung besitzt keinen gueltigen Kundencheckup-Kontext.");
        }

        var beforeVersion = visit.BeforeCheckup.Assessment.ScoringVersion;
        if (beforeVersion == 2)
            return AssessCore(afterCheckup, 2, null);

        ulong? relevantBytes = beforeVersion == CurrentScoringVersion
            ? CleanupScorePotentialService.GetScoreRelevantSafePotentialBytes(
                afterCheckup.CleanupPotentialInformation,
                workingCheckup.TaskList,
                visit.Id,
                ToOffset(entranceDate),
                scanStartedAt)
            : null;

        return AssessCore(afterCheckup, CurrentScoringVersion, relevantBytes);
    }

    private static DateTimeOffset ToOffset(DateTime value) =>
        new(value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Local)
            : value);

    private CheckupAssessment AssessCore(
        CheckupSession checkupSession,
        int scoringVersion,
        ulong? relevantCleanupBytes)
    {
        var assessment =
            new CheckupAssessment
            {
                ScoringVersion =
                    scoringVersion,

                AssessmentCreatedAt =
                    DateTime.Now
            };

        foreach (var assessmentRule in _assessmentRules)
        {
            assessment.Findings.AddRange(
                relevantCleanupBytes.HasValue
                && assessmentRule is CleanupPotentialAssessmentRule cleanupRule
                    ? cleanupRule.Evaluate(checkupSession, relevantCleanupBytes.Value)
                    : assessmentRule.Evaluate(checkupSession));
        }

        assessment.SystemCondition =
            _systemConditionAssessmentService.Assess(
                checkupSession,
                assessment.Findings);

        var hardwareAssessment =
            _hardwareConditionAssessmentService.Assess(
                checkupSession,
                assessment.Findings);

        assessment.HardwareCondition =
            hardwareAssessment.Condition;

        assessment.HardwarePlanningHorizon =
            hardwareAssessment.PlanningHorizon;

        assessment.HardwarePlanningSummary =
            hardwareAssessment.PlanningSummary;

        checkupSession.TaskList =
            CheckupTaskGenerator.Generate(
                assessment.Findings);

        return assessment;
    }
}


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
    private const int CurrentScoringVersion =
        2;

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
        var assessment =
            new CheckupAssessment
            {
                ScoringVersion =
                    CurrentScoringVersion,

                AssessmentCreatedAt =
                    DateTime.Now
            };

        foreach (var assessmentRule in _assessmentRules)
        {
            assessment.Findings.AddRange(
                assessmentRule.Evaluate(
                    checkupSession));
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

using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public class CheckupTaskActionDefinition
{
    public string TaskCode { get; init; } =
        string.Empty;

    public CheckupTaskActionAvailability Availability
    {
        get;
        init;
    } = CheckupTaskActionAvailability.ManualOnly;

    public string ActionCode { get; init; } =
        string.Empty;

    public string ActionTitle { get; init; } =
        string.Empty;

    public string Description { get; init; } =
        string.Empty;

    public string RiskDescription { get; init; } =
        string.Empty;

    public CheckupTaskActionRiskLevel RiskLevel
    {
        get;
        init;
    } = CheckupTaskActionRiskLevel.None;

    public bool RequiresAdministrator { get; init; }

    public bool MayRequireRestart { get; init; }

    [JsonIgnore]
    public bool IsManualOnly =>
        Availability
        == CheckupTaskActionAvailability.ManualOnly;

    [JsonIgnore]
    public bool IsGuided =>
        Availability
        == CheckupTaskActionAvailability.Guided;

    [JsonIgnore]
    public bool IsExecutable =>
        Availability
        == CheckupTaskActionAvailability.Executable;

    [JsonIgnore]
    public string AvailabilityText =>
        Availability switch
        {
            CheckupTaskActionAvailability.Executable =>
                "Kontrollierte Aktion verfügbar",

            CheckupTaskActionAvailability.Guided =>
                "Geführte Unterstützung verfügbar",

            _ =>
                "Manuell zu bearbeiten"
        };

    [JsonIgnore]
    public string RiskLevelText =>
        RiskLevel switch
        {
            CheckupTaskActionRiskLevel.Low =>
                "Niedriges Risiko",

            CheckupTaskActionRiskLevel.Medium =>
                "Mittleres Risiko",

            CheckupTaskActionRiskLevel.High =>
                "Hohes Risiko",

            CheckupTaskActionRiskLevel.VeryHigh =>
                "Sehr hohes Risiko",

            _ =>
                "Keine automatische Systemänderung"
        };

    [JsonIgnore]
    public string AdministratorRequirementText =>
        RequiresAdministrator
            ? "Administratorrechte erforderlich"
            : "Keine Administratorrechte erforderlich";

    [JsonIgnore]
    public string RestartPossibilityText =>
        MayRequireRestart
            ? "Ein Neustart kann erforderlich werden"
            : "Kein Neustart durch diese Unterstützung vorgesehen";
}
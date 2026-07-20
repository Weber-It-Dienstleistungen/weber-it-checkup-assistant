using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public sealed class CheckupTaskActionPlan
{
    public Guid Id { get; init; } =
        Guid.NewGuid();

    public Guid TaskId { get; init; }

    public string TaskCode { get; init; } =
        string.Empty;

    public string ActionCode { get; init; } =
        string.Empty;

    public string ActionTitle { get; init; } =
        string.Empty;

    public string TargetDescription { get; init; } =
        string.Empty;

    public string ExpectedEffect { get; init; } =
        string.Empty;

    public string RiskDescription { get; init; } =
        string.Empty;

    public CheckupTaskActionRiskLevel RiskLevel { get; init; } =
        CheckupTaskActionRiskLevel.None;

    public bool RequiresAdministrator { get; init; }

    public bool MayRequireRestart { get; init; }

    public DateTimeOffset CreatedAt { get; init; } =
        DateTimeOffset.Now;

    public List<CheckupTaskActionCommandPreview> Commands
    {
        get;
        init;
    } = new();

    [JsonIgnore]
    public bool HasCommands =>
        Commands.Count > 0;

    [JsonIgnore]
    public int CommandCount =>
        Commands.Count;

    [JsonIgnore]
    public string CommandCountText =>
        CommandCount switch
        {
            0 =>
                "Keine automatische Befehlsausführung vorgesehen",

            1 =>
                "Ein Befehl ist vorgesehen",

            _ =>
                $"{CommandCount} Befehle sind vorgesehen"
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
            : "Kein Neustart durch diese Aktion vorgesehen";

    public void Validate()
    {
        if (Id == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Der Aktionsplan benötigt eine "
                + "eindeutige Kennung.");
        }

        if (TaskId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Der Aktionsplan ist keiner "
                + "eindeutigen Aufgabe zugeordnet.");
        }

        if (string.IsNullOrWhiteSpace(
                TaskCode))
        {
            throw new InvalidOperationException(
                "Der Aktionsplan benötigt einen "
                + "stabilen TaskCode.");
        }

        if (string.IsNullOrWhiteSpace(
                ActionCode))
        {
            throw new InvalidOperationException(
                "Der Aktionsplan benötigt einen "
                + "stabilen Aktionscode.");
        }

        if (string.IsNullOrWhiteSpace(
                ActionTitle))
        {
            throw new InvalidOperationException(
                "Der Aktionsplan benötigt eine "
                + "verständliche Bezeichnung.");
        }

        if (string.IsNullOrWhiteSpace(
                TargetDescription))
        {
            throw new InvalidOperationException(
                "Das konkrete Ziel der Aktion muss "
                + "beschrieben sein.");
        }

        if (string.IsNullOrWhiteSpace(
                ExpectedEffect))
        {
            throw new InvalidOperationException(
                "Die erwartete Wirkung der Aktion "
                + "muss beschrieben sein.");
        }

        if (string.IsNullOrWhiteSpace(
                RiskDescription))
        {
            throw new InvalidOperationException(
                "Das Risiko der Aktion muss vor "
                + "der Ausführung beschrieben sein.");
        }

        if (!HasCommands)
        {
            throw new InvalidOperationException(
                "Ein ausführbarer Aktionsplan benötigt "
                + "mindestens einen konkreten Befehl.");
        }

        foreach (var command in Commands)
        {
            if (command is null)
            {
                throw new InvalidOperationException(
                    "Der Aktionsplan enthält einen "
                    + "ungültigen Befehl.");
            }

            command.Validate();
        }

        if (RequiresAdministrator
            && Commands.Any(
                command =>
                    !command.RequiresAdministrator))
        {
            throw new InvalidOperationException(
                "Die Rechteanforderung des Aktionsplans "
                + "stimmt nicht mit allen Befehlen überein.");
        }
    }
}
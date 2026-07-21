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

    public List<CleanupActionCategory> CleanupCategories
    {
        get;
        init;
    } = new();

    [JsonIgnore]
    public bool HasCommands =>
        Commands.Count > 0;

    [JsonIgnore]
    public bool HasCleanupCategories =>
        CleanupCategories.Count > 0;

    [JsonIgnore]
    public int CommandCount =>
        Commands.Count;

    [JsonIgnore]
    public int CleanupCategoryCount =>
        CleanupCategories.Count;

    [JsonIgnore]
    public string CommandCountText =>
        CommandCount switch
        {
            0 =>
                "Keine externe Befehlsausführung vorgesehen",

            1 =>
                "Ein Befehl ist vorgesehen",

            _ =>
                $"{CommandCount} Befehle sind vorgesehen"
        };

    [JsonIgnore]
    public string CleanupCategoryCountText =>
        CleanupCategoryCount switch
        {
            0 =>
                "Keine Bereinigungskategorie vorgesehen",

            1 =>
                "Eine Bereinigungskategorie ist vorgesehen",

            _ =>
                $"{CleanupCategoryCount} "
                + "Bereinigungskategorien sind vorgesehen"
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

        if (!HasCommands
            && !HasCleanupCategories)
        {
            throw new InvalidOperationException(
                "Ein ausführbarer Aktionsplan benötigt "
                + "mindestens einen konkreten Befehl "
                + "oder eine validierte "
                + "Bereinigungskategorie.");
        }

        if (HasCommands
            && HasCleanupCategories)
        {
            throw new InvalidOperationException(
                "Ein Aktionsplan darf nicht gleichzeitig "
                + "externe Befehle und Bereinigungsziele "
                + "enthalten.");
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

        foreach (var category in CleanupCategories)
        {
            if (category is null)
            {
                throw new InvalidOperationException(
                    "Der Aktionsplan enthält eine "
                    + "ungültige Bereinigungskategorie.");
            }

            category.Validate();
        }

        var duplicateCleanupCategory =
            CleanupCategories
                .GroupBy(
                    category =>
                        category.Category)
                .FirstOrDefault(
                    group =>
                        group.Count() > 1);

        if (duplicateCleanupCategory is not null)
        {
            throw new InvalidOperationException(
                "Der Aktionsplan enthält mindestens "
                + "eine Bereinigungskategorie mehrfach.");
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
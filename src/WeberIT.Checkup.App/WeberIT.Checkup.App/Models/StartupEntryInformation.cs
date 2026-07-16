using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public class StartupEntryInformation
{
    public string DisplayName { get; set; } =
        string.Empty;

    public string NormalizedProgramName { get; set; } =
        string.Empty;

    public string ProductName { get; set; } =
        string.Empty;

    public string Publisher { get; set; } =
        string.Empty;

    public StartupSourceType SourceType { get; set; } =
        StartupSourceType.Unknown;

    public StartupEntryContext Context { get; set; } =
        StartupEntryContext.Unknown;

    public StartupRegistryView RegistryView { get; set; } =
        StartupRegistryView.NotApplicable;

    public StartupEntryState State { get; set; } =
        StartupEntryState.Unknown;

    public StartupTargetType TargetType { get; set; } =
        StartupTargetType.Unknown;

    public StartupClassification Classification { get; set; } =
        StartupClassification.Unknown;

    public bool? TargetExists { get; set; }

    public string SourceIdentifier { get; set; } =
        string.Empty;

    public string Description { get; set; } =
        string.Empty;

    [JsonIgnore]
    public bool IsEnabled =>
        State == StartupEntryState.Enabled;

    [JsonIgnore]
    public bool IsDisabled =>
        State == StartupEntryState.Disabled;

    [JsonIgnore]
    public bool IsConspicuous =>
        Classification == StartupClassification.Conspicuous;

    [JsonIgnore]
    public bool IsOptionalReview =>
        Classification == StartupClassification.OptionalReview;

    [JsonIgnore]
    public string DisplayNameText =>
        string.IsNullOrWhiteSpace(DisplayName)
            ? "Unbekannter Autostarteintrag"
            : DisplayName;

    [JsonIgnore]
    public string ProductNameText =>
        string.IsNullOrWhiteSpace(ProductName)
            ? "Produkt nicht eindeutig bestimmbar"
            : ProductName;

    [JsonIgnore]
    public string PublisherText =>
        string.IsNullOrWhiteSpace(Publisher)
            ? "Hersteller nicht verfügbar"
            : Publisher;

    [JsonIgnore]
    public string SourceText =>
        SourceType switch
        {
            StartupSourceType.RegistryRun =>
                "Registry – Run",

            StartupSourceType.RegistryRunOnce =>
                "Registry – RunOnce",

            StartupSourceType.StartupFolder =>
                "Autostartordner",

            _ =>
                "Unbekannte Quelle"
        };

    [JsonIgnore]
    public string ContextText =>
        Context switch
        {
            StartupEntryContext.CurrentUser =>
                "Aktueller Benutzer",

            StartupEntryContext.AllUsers =>
                "Alle Benutzer",

            _ =>
                "Kontext nicht bestimmbar"
        };

    [JsonIgnore]
    public string RegistryViewText =>
        RegistryView switch
        {
            StartupRegistryView.Registry32 =>
                "32-Bit-Registryansicht",

            StartupRegistryView.Registry64 =>
                "64-Bit-Registryansicht",

            _ =>
                "Nicht zutreffend"
        };

    [JsonIgnore]
    public string StateText =>
        State switch
        {
            StartupEntryState.Enabled =>
                "Aktiv",

            StartupEntryState.Disabled =>
                "Deaktiviert",

            _ =>
                "Status nicht eindeutig"
        };

    [JsonIgnore]
    public string TargetTypeText =>
        TargetType switch
        {
            StartupTargetType.Executable =>
                "Programm",

            StartupTargetType.Shortcut =>
                "Verknüpfung",

            StartupTargetType.Script =>
                "Skript",

            StartupTargetType.CommandInterpreter =>
                "Windows-Befehlsinterpreter",

            StartupTargetType.PowerShell =>
                "PowerShell-Aufruf",

            StartupTargetType.DynamicLibrary =>
                "Indirekter DLL-Aufruf",

            StartupTargetType.StoreApplication =>
                "Windows- oder Store-Anwendung",

            StartupTargetType.IndirectTarget =>
                "Indirektes Startziel",

            _ =>
                "Startziel nicht eindeutig"
        };

    [JsonIgnore]
    public string TargetStatusText =>
        TargetExists switch
        {
            true =>
                "Lokales Ziel vorhanden",

            false =>
                "Lokales Ziel nicht gefunden",

            null =>
                "Ziel nicht sicher prüfbar"
        };

    [JsonIgnore]
    public string ClassificationText =>
        Classification switch
        {
            StartupClassification.SystemOrDriverRelated =>
                "System- oder treibernah",

            StartupClassification.ProbablyUseful =>
                "Wahrscheinlich sinnvoll",

            StartupClassification.OptionalReview =>
                "Optional prüfbar",

            StartupClassification.Conspicuous =>
                "Manuell prüfen",

            StartupClassification.Disabled =>
                "Deaktiviert",

            StartupClassification.NotEvaluable =>
                "Nicht auswertbar",

            _ =>
                "Nicht eindeutig einzuordnen"
        };

    [JsonIgnore]
    public string DescriptionText =>
        string.IsNullOrWhiteSpace(Description)
            ? "Zu diesem Autostarteintrag sind keine weiteren Angaben verfügbar."
            : Description;
}
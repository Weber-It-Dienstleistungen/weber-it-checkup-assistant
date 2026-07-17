using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public class DeviceDriverEntryInformation
{
    public string DisplayName { get; set; } =
        string.Empty;

    public string DeviceClass { get; set; } =
        string.Empty;

    public string Manufacturer { get; set; } =
        string.Empty;

    public DeviceOperationalState OperationalState
    {
        get;
        set;
    } = DeviceOperationalState.Unknown;

    public int? ConfigManagerErrorCode { get; set; }

    public string StatusDescription { get; set; } =
        string.Empty;

    public DriverAssignmentState DriverAssignmentState
    {
        get;
        set;
    } = DriverAssignmentState.Unknown;

    public string DriverProvider { get; set; } =
        string.Empty;

    public string DriverVersion { get; set; } =
        string.Empty;

    public DateTime? DriverDate { get; set; }

    public string InfName { get; set; } =
        string.Empty;

    public bool? IsSigned { get; set; }

    public DeviceDriverClassification Classification
    {
        get;
        set;
    } = DeviceDriverClassification.Unknown;

    [JsonIgnore]
    public bool HasWindowsProblem =>
        OperationalState == DeviceOperationalState.Problem;

    [JsonIgnore]
    public bool HasMissingDriver =>
        DriverAssignmentState == DriverAssignmentState.Missing;

    [JsonIgnore]
    public bool IsDisabled =>
        OperationalState == DeviceOperationalState.Disabled;

    [JsonIgnore]
    public bool IsNotEvaluable =>
        Classification == DeviceDriverClassification.NotEvaluable;

    [JsonIgnore]
    public string DisplayNameText =>
        string.IsNullOrWhiteSpace(DisplayName)
            ? "Unbekanntes Gerät"
            : DisplayName;

    [JsonIgnore]
    public string DeviceClassText =>
        string.IsNullOrWhiteSpace(DeviceClass)
            ? "Geräteklasse nicht verfügbar"
            : DeviceClass;

    [JsonIgnore]
    public string ManufacturerText =>
        string.IsNullOrWhiteSpace(Manufacturer)
            ? "Hersteller nicht verfügbar"
            : Manufacturer;

    [JsonIgnore]
    public string OperationalStateText =>
        OperationalState switch
        {
            DeviceOperationalState.Working =>
                "Funktioniert laut Windows ordnungsgemäß",

            DeviceOperationalState.Problem =>
                "Windows meldet ein Geräteproblem",

            DeviceOperationalState.Disabled =>
                "Gerät ist deaktiviert",

            _ =>
                "Gerätestatus nicht eindeutig"
        };

    [JsonIgnore]
    public string ConfigManagerErrorCodeText =>
        ConfigManagerErrorCode.HasValue
            ? $"Windows-Gerätecode {ConfigManagerErrorCode.Value}"
            : "Kein Gerätecode verfügbar";

    [JsonIgnore]
    public string StatusDescriptionText =>
        string.IsNullOrWhiteSpace(StatusDescription)
            ? "Keine weitere Statusbeschreibung verfügbar."
            : StatusDescription;

    [JsonIgnore]
    public string DriverAssignmentStateText =>
        DriverAssignmentState switch
        {
            DriverAssignmentState.Assigned =>
                "Treiber zugeordnet",

            DriverAssignmentState.Missing =>
                "Benötigter Treiber fehlt",

            DriverAssignmentState.NotRequired =>
                "Kein eigener Gerätetreiber erforderlich",

            _ =>
                "Treiberzuordnung nicht eindeutig"
        };

    [JsonIgnore]
    public string DriverProviderText =>
        string.IsNullOrWhiteSpace(DriverProvider)
            ? "Treiberanbieter nicht verfügbar"
            : DriverProvider;

    [JsonIgnore]
    public string DriverVersionText =>
        string.IsNullOrWhiteSpace(DriverVersion)
            ? "Treiberversion nicht verfügbar"
            : DriverVersion;

    [JsonIgnore]
    public string DriverDateText =>
        DriverDate.HasValue
            ? DriverDate.Value.ToString("dd.MM.yyyy")
            : "Treiberdatum nicht verfügbar";

    [JsonIgnore]
    public string InfNameText =>
        string.IsNullOrWhiteSpace(InfName)
            ? "INF-Datei nicht verfügbar"
            : InfName;

    [JsonIgnore]
    public string SignedStatusText =>
        IsSigned switch
        {
            true =>
                "Treiber laut Windows signiert",

            false =>
                "Treiber laut Windows nicht signiert",

            null =>
                "Signaturstatus nicht verfügbar"
        };

    [JsonIgnore]
    public string ClassificationText =>
        Classification switch
        {
            DeviceDriverClassification.Working =>
                "Funktioniert ordnungsgemäß",

            DeviceDriverClassification.WindowsProblem =>
                "Windows-Problem gemeldet",

            DeviceDriverClassification.MissingDriver =>
                "Treiber fehlt",

            DeviceDriverClassification.UnsignedDriver =>
                "Nicht signierter Treiber",

            DeviceDriverClassification.Disabled =>
                "Deaktiviert",

            DeviceDriverClassification.NotEvaluable =>
                "Nicht auswertbar",

            _ =>
                "Nicht eindeutig einzuordnen"
        };
}
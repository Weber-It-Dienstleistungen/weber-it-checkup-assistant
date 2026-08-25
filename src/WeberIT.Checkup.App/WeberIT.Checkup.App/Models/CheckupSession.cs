using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public class CheckupSession
{
    private List<CustomerCheckupVisit>
        _customerCheckupVisits =
            new();

    public DateTime? ScanDate { get; set; }

    public DeviceInformation DeviceInformation { get; set; } =
        new();

    public HardwareInformation HardwareInformation { get; set; } =
        new();

    public OperatingSystemInformation OperatingSystemInformation
    {
        get;
        set;
    } = new();

    public StorageInformation StorageInformation { get; set; } =
        new();

    public CleanupPotentialInformation CleanupPotentialInformation
    {
        get;
        set;
    } = new();

    public StartupInformation StartupInformation
    {
        get;
        set;
    } = new();

    public DeviceDriverInformation DeviceDriverInformation
    {
        get;
        set;
    } = new();

    public SecurityInformation SecurityInformation { get; set; } =
        new();

    public WindowsUpdateInformation WindowsUpdateInformation
    {
        get;
        set;
    } = new();

    public ProgramUpdateInformation ProgramUpdateInformation
    {
        get;
        set;
    } = new();

    public RestartInformation RestartInformation { get; set; } =
        new();

    public CheckupAssessment Assessment { get; set; } =
        new();

    public CheckupTaskList TaskList { get; set; } =
        new();

    public List<CustomerCheckupVisit> CustomerCheckupVisits
    {
        get =>
            _customerCheckupVisits;

        set =>
            _customerCheckupVisits =
                value
                ?? new List<CustomerCheckupVisit>();
    }

    [JsonIgnore]
    public CustomerCheckupVisit?
        CurrentCustomerCheckupVisit =>
            CustomerCheckupVisits
                .Where(
                    visit =>
                        visit.IsInProgress)
                .OrderByDescending(
                    visit =>
                        visit.StartedAt)
                .FirstOrDefault();

    [JsonIgnore]
    public bool HasInProgressCustomerCheckupVisit =>
        CurrentCustomerCheckupVisit is not null;

    [JsonIgnore]
    public bool HasCompletedCustomerCheckupVisit =>
        CustomerCheckupVisits.Any(
            visit =>
                visit.IsCompleted);

    [JsonIgnore]
    public int CustomerCheckupVisitCount =>
        CustomerCheckupVisits.Count;

    [JsonIgnore]
    public string CurrentCheckupTitle
    {
        get
        {
            var currentVisit =
                CurrentCustomerCheckupVisit;

            if (currentVisit is null)
            {
                return
                    "Gespeicherter Systemcheck";
            }

            return currentVisit.IsCompletionPrepared
                ? "Arbeitsstand mit vorbereitetem Abschluss"
                : "Arbeitsstand des laufenden Kundencheckups";
        }
    }

    [JsonIgnore]
    public string CurrentCheckupDescription
    {
        get
        {
            var currentVisit =
                CurrentCustomerCheckupVisit;

            if (currentVisit is null)
            {
                return
                    "Vollständiger Stand des zuletzt "
                    + "gespeicherten Scans.";
            }

            if (currentVisit.IsCompletionPrepared)
            {
                return
                    "Der bisherige Arbeitsstand bleibt für die "
                    + "Aktionsdokumentation erhalten. Der Nachher-Scan, "
                    + "der Vorher-/Nachher-Vergleich und die "
                    + "Technikerangaben wurden bereits als "
                    + "Abschlussentwurf im Vorgang gesichert.";
            }

            return
                "Dieser Scan ist der aktuelle Arbeitsstand. "
                + "Für den laufenden Vorgang wurde eine "
                + "unabhängige Kopie als unveränderlicher "
                + "Vorher-Zustand gesichert.";
        }
    }

    [JsonIgnore]
    public string CustomerCheckupWorkflowText
    {
        get
        {
            var currentVisit =
                CurrentCustomerCheckupVisit;

            if (currentVisit is null)
            {
                return
                    "Für dieses Gerät läuft derzeit "
                    + "kein Kundencheckup.";
            }

            if (currentVisit.IsCompletionPrepared)
            {
                var afterScanDate =
                    currentVisit.AfterCheckup?.ScanDate;

                var afterScanText =
                    afterScanDate.HasValue
                        ? afterScanDate.Value
                            .ToString(
                                "dd.MM.yyyy HH:mm")
                          + " Uhr"
                        : "unbekanntem Zeitpunkt";

                return
                    $"Die Abschlusskontrolle vom {afterScanText} "
                    + "wurde als Nachher-Zustand gesichert. "
                    + "Vergleich und Technikerangaben liegen als "
                    + "Entwurf vor. Der Kundencheckup bleibt bis "
                    + "zur späteren Berichtserstellung und "
                    + "endgültigen Bestätigung in Bearbeitung.";
            }

            return
                $"Der Eingangsscan vom "
                + $"{currentVisit.StartedAtText} wurde als "
                + "unveränderlicher Vorher-Zustand gesichert. "
                + "Bearbeiten Sie jetzt die erkannten Aufgaben. "
                + "Der Nachher-Zustand wird mit der späteren "
                + "Abschlusskontrolle erfasst.";
        }
    }
}
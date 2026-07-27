using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public sealed class CustomerCheckupVisit
{
    public int VisitModelVersion { get; set; } =
        1;

    public Guid Id { get; set; } =
        Guid.NewGuid();

    public DateTime StartedAt { get; set; } =
        DateTime.Now;

    public DateTime? CompletedAt { get; set; }

    public CustomerCheckupVisitStatus Status { get; set; } =
        CustomerCheckupVisitStatus.InProgress;

    public CheckupSnapshot BeforeCheckup { get; set; } =
        new();

    public CheckupSnapshot? AfterCheckup { get; set; }

    public string TechnicianSummary { get; set; } =
        string.Empty;

    public string NextSteps { get; set; } =
        string.Empty;

    public DateTime? NextCheckupDate { get; set; }

    public string CancellationReason { get; set; } =
        string.Empty;

    [JsonIgnore]
    public bool IsInProgress =>
        Status
        == CustomerCheckupVisitStatus.InProgress;

    [JsonIgnore]
    public bool IsCompleted =>
        Status
        == CustomerCheckupVisitStatus.Completed;

    [JsonIgnore]
    public bool IsCancelled =>
        Status
        == CustomerCheckupVisitStatus.Cancelled;

    [JsonIgnore]
    public bool HasAfterCheckup =>
        AfterCheckup is not null;

    [JsonIgnore]
    public string StatusText =>
        Status switch
        {
            CustomerCheckupVisitStatus.Completed =>
                "Abgeschlossen",

            CustomerCheckupVisitStatus.Cancelled =>
                "Abgebrochen",

            _ =>
                "In Bearbeitung"
        };

    [JsonIgnore]
    public string StartedAtText =>
        StartedAt.ToString(
            "dd.MM.yyyy HH:mm")
        + " Uhr";

    [JsonIgnore]
    public string CompletedAtText =>
        CompletedAt.HasValue
            ? CompletedAt.Value.ToString(
                "dd.MM.yyyy HH:mm")
              + " Uhr"
            : "Noch nicht abgeschlossen";

    [JsonIgnore]
    public string NextCheckupDateText =>
        NextCheckupDate.HasValue
            ? NextCheckupDate.Value.ToString(
                "dd.MM.yyyy")
            : "Noch nicht festgelegt";

    public static CustomerCheckupVisit Start(
        CheckupSession beforeCheckup)
    {
        ArgumentNullException.ThrowIfNull(
            beforeCheckup);

        if (!beforeCheckup.ScanDate.HasValue)
        {
            throw new ArgumentException(
                "Ein Kundencheckup kann nur mit einem "
                + "abgeschlossenen Eingangsscan gestartet werden.",
                nameof(beforeCheckup));
        }

        return new CustomerCheckupVisit
        {
            StartedAt =
                beforeCheckup.ScanDate.Value,

            Status =
                CustomerCheckupVisitStatus.InProgress,

            BeforeCheckup =
                CheckupSnapshot.Capture(
                    beforeCheckup)
        };
    }

    public void Complete(
        CheckupSession afterCheckup,
        string technicianSummary,
        string nextSteps,
        DateTime? nextCheckupDate)
    {
        ArgumentNullException.ThrowIfNull(
            afterCheckup);

        if (!IsInProgress)
        {
            throw new InvalidOperationException(
                "Nur ein laufender Kundencheckup kann "
                + "abgeschlossen werden.");
        }

        if (!afterCheckup.ScanDate.HasValue)
        {
            throw new ArgumentException(
                "Der Kundencheckup kann nur mit einem "
                + "abgeschlossenen Kontrollscan beendet werden.",
                nameof(afterCheckup));
        }

        AfterCheckup =
            CheckupSnapshot.Capture(
                afterCheckup);

        TechnicianSummary =
            technicianSummary?.Trim()
            ?? string.Empty;

        NextSteps =
            nextSteps?.Trim()
            ?? string.Empty;

        NextCheckupDate =
            nextCheckupDate;

        CompletedAt =
            afterCheckup.ScanDate.Value;

        CancellationReason =
            string.Empty;

        Status =
            CustomerCheckupVisitStatus.Completed;
    }

    public void Cancel(
        string reason)
    {
        if (!IsInProgress)
        {
            throw new InvalidOperationException(
                "Nur ein laufender Kundencheckup kann "
                + "abgebrochen werden.");
        }

        if (string.IsNullOrWhiteSpace(
                reason))
        {
            throw new ArgumentException(
                "Für den Abbruch des Kundencheckups "
                + "ist eine Begründung erforderlich.",
                nameof(reason));
        }

        CancellationReason =
            reason.Trim();

        CompletedAt =
            DateTime.Now;

        Status =
            CustomerCheckupVisitStatus.Cancelled;
    }
}
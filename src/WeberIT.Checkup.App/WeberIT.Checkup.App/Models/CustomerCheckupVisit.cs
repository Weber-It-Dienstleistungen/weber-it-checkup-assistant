using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public sealed class CustomerCheckupVisit
{
    public const int CurrentVisitModelVersion =
        3;

    public int VisitModelVersion { get; set; } =
        CurrentVisitModelVersion;

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

    public CustomerCheckupComparison? Comparison { get; set; }

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
    public bool HasComparison =>
        Comparison is not null;

    [JsonIgnore]
    public bool IsCompletionPrepared =>
        IsInProgress
        && HasAfterCheckup
        && HasComparison;

    [JsonIgnore]
    public string StatusText =>
        Status switch
        {
            CustomerCheckupVisitStatus.Completed =>
                "Abgeschlossen",

            CustomerCheckupVisitStatus.Cancelled =>
                "Abgebrochen",

            _ =>
                IsCompletionPrepared
                    ? "Abschluss vorbereitet"
                    : "In Bearbeitung"
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

    public void StoreComparison(
        CustomerCheckupComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(
            comparison);

        EnsureInProgress();

        ValidateComparison(
            comparison,
            null);

        VisitModelVersion =
            CurrentVisitModelVersion;

        Comparison =
            comparison;
    }

    public void PrepareCompletion(
        CheckupSession afterCheckup,
        CustomerCheckupComparison comparison,
        string technicianSummary,
        string nextSteps,
        DateTime? nextCheckupDate)
    {
        ArgumentNullException.ThrowIfNull(
            afterCheckup);

        ArgumentNullException.ThrowIfNull(
            comparison);

        EnsureInProgress();

        if (!afterCheckup.ScanDate.HasValue)
        {
            throw new ArgumentException(
                "Der Abschlussentwurf benötigt einen "
                + "abgeschlossenen Nachher-Scan.",
                nameof(afterCheckup));
        }

        ValidateComparison(
            comparison,
            afterCheckup.ScanDate.Value);

        ValidateCompletionDetails(
            technicianSummary,
            nextSteps,
            nextCheckupDate,
            afterCheckup.ScanDate.Value);

        VisitModelVersion =
            CurrentVisitModelVersion;

        AfterCheckup =
            CheckupSnapshot.Capture(
                afterCheckup);

        Comparison =
            comparison;

        ApplyCompletionDetails(
            technicianSummary,
            nextSteps,
            nextCheckupDate!.Value);

        CompletedAt =
            null;

        CancellationReason =
            string.Empty;

        Status =
            CustomerCheckupVisitStatus.InProgress;
    }

    public void UpdateCompletionDetails(
        string technicianSummary,
        string nextSteps,
        DateTime? nextCheckupDate)
    {
        EnsureInProgress();

        if (!IsCompletionPrepared
            || AfterCheckup?.ScanDate is null)
        {
            throw new InvalidOperationException(
                "Die Technikerangaben können erst geändert werden, "
                + "wenn Nachher-Scan und Vergleich als "
                + "Abschlussentwurf vorliegen.");
        }

        var afterScanDate =
            AfterCheckup.ScanDate.Value;

        ValidateCompletionDetails(
            technicianSummary,
            nextSteps,
            nextCheckupDate,
            afterScanDate);

        VisitModelVersion =
            CurrentVisitModelVersion;

        ApplyCompletionDetails(
            technicianSummary,
            nextSteps,
            nextCheckupDate!.Value);
    }

    public void CompletePrepared()
    {
        EnsureInProgress();

        if (!IsCompletionPrepared
            || AfterCheckup?.ScanDate is null)
        {
            throw new InvalidOperationException(
                "Der Kundencheckup kann erst abgeschlossen werden, "
                + "wenn Nachher-Scan und Vergleich vollständig "
                + "vorbereitet wurden.");
        }

        var afterScanDate =
            AfterCheckup.ScanDate.Value;

        ValidateCompletionDetails(
            TechnicianSummary,
            NextSteps,
            NextCheckupDate,
            afterScanDate);

        VisitModelVersion =
            CurrentVisitModelVersion;

        CompletedAt =
            afterScanDate;

        CancellationReason =
            string.Empty;

        Status =
            CustomerCheckupVisitStatus.Completed;
    }

    public void Cancel(
        string reason)
    {
        EnsureInProgress();

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

    private void EnsureInProgress()
    {
        if (IsInProgress)
        {
            return;
        }

        throw new InvalidOperationException(
            "Nur ein laufender Kundencheckup kann "
            + "bearbeitet werden.");
    }

    private void ValidateComparison(
        CustomerCheckupComparison comparison,
        DateTime? expectedAfterScanDate)
    {
        if (comparison.CustomerCheckupVisitId
            != Id)
        {
            throw new ArgumentException(
                "Das Vergleichsergebnis gehört nicht zu diesem "
                + "Kundencheckup.",
                nameof(comparison));
        }

        if (comparison.BeforeScanDate
            != BeforeCheckup.ScanDate)
        {
            throw new ArgumentException(
                "Das Vergleichsergebnis verwendet nicht den "
                + "gesicherten Eingangsscan dieses Vorgangs.",
                nameof(comparison));
        }

        if (expectedAfterScanDate.HasValue
            && comparison.AfterScanDate
                != expectedAfterScanDate)
        {
            throw new ArgumentException(
                "Das Vergleichsergebnis gehört nicht zum "
                + "übergebenen Nachher-Scan.",
                nameof(comparison));
        }
    }

    private static void ValidateCompletionDetails(
        string technicianSummary,
        string nextSteps,
        DateTime? nextCheckupDate,
        DateTime afterScanDate)
    {
        if (string.IsNullOrWhiteSpace(
                technicianSummary))
        {
            throw new ArgumentException(
                "Für den Abschlussentwurf ist eine "
                + "Technikerzusammenfassung erforderlich.",
                nameof(technicianSummary));
        }

        if (string.IsNullOrWhiteSpace(
                nextSteps))
        {
            throw new ArgumentException(
                "Für den Abschlussentwurf müssen die nächsten "
                + "Schritte dokumentiert werden.",
                nameof(nextSteps));
        }

        if (!nextCheckupDate.HasValue)
        {
            throw new ArgumentException(
                "Für den Abschlussentwurf muss der nächste "
                + "Checkup-Termin festgelegt werden.",
                nameof(nextCheckupDate));
        }

        if (nextCheckupDate.Value.Date
            <= afterScanDate.Date)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextCheckupDate),
                "Der nächste Checkup-Termin muss nach dem "
                + "Nachher-Scan liegen.");
        }
    }

    private void ApplyCompletionDetails(
        string technicianSummary,
        string nextSteps,
        DateTime nextCheckupDate)
    {
        TechnicianSummary =
            technicianSummary.Trim();

        NextSteps =
            nextSteps.Trim();

        NextCheckupDate =
            nextCheckupDate.Date;
    }
}
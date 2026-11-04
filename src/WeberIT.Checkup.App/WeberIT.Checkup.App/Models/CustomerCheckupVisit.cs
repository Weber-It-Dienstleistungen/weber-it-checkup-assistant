
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public sealed class CustomerCheckupVisit
{
    private List<ProgramUpdateException>
        _programUpdateExceptions = new();

    private List<ProgramUpdateIntermediateScan>
        _programUpdateIntermediateScans = new();

    // =============================================
    // SPRINT 55.14.34b
    // BEREINIGUNGS-ZWISCHENPRÜFUNGEN
    // =============================================

    private List<CleanupPotentialIntermediateScan>
        _cleanupPotentialIntermediateScans = new();

    public const int CurrentVisitModelVersion = 6;

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

    // =============================================
    // PROGRAMMUPDATE-AUSNAHMEN
    // =============================================

    /// <summary>
    /// Bewusst nicht durchgeführte Programmupdates
    /// in genau diesem Kundencheckup.
    ///
    /// Die Liste wird gemeinsam mit dem Kundencheckup
    /// im Datenbank-JSON gespeichert.
    /// </summary>
    public List<ProgramUpdateException>
        ProgramUpdateExceptions
    {
        get => _programUpdateExceptions;

        set => _programUpdateExceptions =
            value ?? new List<ProgramUpdateException>();
    }

    // =============================================
    // WINGet-ZWISCHENPRÜFUNGEN
    // =============================================

    /// <summary>
    /// Gespeicherte WinGet-Zwischenprüfungen dieses
    /// Kundencheckups.
    ///
    /// Jede Zwischenprüfung besitzt eine eigene
    /// Kennung und einen unabhängigen Scanbefund.
    ///
    /// Der ursprüngliche Eingangsscan,
    /// die Aktionsprotokolle und die spätere
    /// Abschlusskontrolle bleiben unverändert.
    ///
    /// Die Liste wird mit dem Kundencheckup
    /// im bestehenden Datenbank-JSON gespeichert.
    ///
    /// Historische Vorgänge ohne diese Eigenschaft
    /// erhalten automatisch eine leere Liste.
    /// </summary>
    public List<ProgramUpdateIntermediateScan>
        ProgramUpdateIntermediateScans
    {
        get => _programUpdateIntermediateScans;

        set => _programUpdateIntermediateScans =
            value
            ?? new List<ProgramUpdateIntermediateScan>();
    }

    [JsonIgnore]
    public bool HasProgramUpdateIntermediateScans =>
        ProgramUpdateIntermediateScans.Count > 0;

    [JsonIgnore]
    public int ProgramUpdateIntermediateScanCount =>
        ProgramUpdateIntermediateScans.Count;

    // =============================================
    // BEREINIGUNGS-ZWISCHENPRÜFUNGEN
    // =============================================

    /// <summary>
    /// Dokumentierte Neumessungen einzelner
    /// Bereinigungskategorien.
    ///
    /// Jede Zwischenprüfung erhält:
    /// - eine eigene Kennung,
    /// - die Kennung dieses Kundencheckups,
    /// - die ursprüngliche Eingangsmessung,
    /// - eine eigenständige Nachmessung,
    /// - optional das technische Löschresultat.
    ///
    /// Diese Daten ersetzen weder den
    /// Eingangsscan noch einen späteren
    /// vollständigen Abschlussscan.
    ///
    /// Die Speicherung erfolgt über die
    /// bestehende JSON-Persistierung des
    /// Kundencheckups.
    ///
    /// Historische Vorgänge ohne dieses Feld
    /// erhalten eine leere Liste.
    /// </summary>
    public List<CleanupPotentialIntermediateScan>
        CleanupPotentialIntermediateScans
    {
        get => _cleanupPotentialIntermediateScans;

        set => _cleanupPotentialIntermediateScans =
            value
            ?? new List<CleanupPotentialIntermediateScan>();
    }

    [JsonIgnore]
    public bool HasCleanupPotentialIntermediateScans =>
        CleanupPotentialIntermediateScans.Count > 0;

    [JsonIgnore]
    public int CleanupPotentialIntermediateScanCount =>
        CleanupPotentialIntermediateScans.Count;

    // =============================================
    // VORGANGSSTATUS
    // =============================================

    [JsonIgnore]
    public bool IsInProgress =>
        Status == CustomerCheckupVisitStatus.InProgress;

    [JsonIgnore]
    public bool IsCompleted =>
        Status == CustomerCheckupVisitStatus.Completed;

    [JsonIgnore]
    public bool IsCancelled =>
        Status == CustomerCheckupVisitStatus.Cancelled;

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

    // =============================================
    // BERICHTSARCHIV
    // =============================================

    /// <summary>
    /// Gibt an, ob aus diesem bereits abgeschlossenen
    /// Kundencheckup erneut ein PDF-Bericht erzeugt
    /// werden kann.
    ///
    /// Die Prüfung verändert weder den archivierten
    /// Vorgang noch seine gespeicherten Prüfdaten.
    ///
    /// Die Eigenschaft wird nicht in der Datenbank
    /// beziehungsweise im JSON gespeichert.
    /// </summary>
    [JsonIgnore]
    public bool CanRegenerateReport
    {
        get
        {
            if (!IsCompleted)
            {
                return false;
            }

            if (BeforeCheckup?.ScanDate is null
                || AfterCheckup?.ScanDate is null
                || Comparison is null)
            {
                return false;
            }

            if (Comparison.CustomerCheckupVisitId != Id
                || Comparison.BeforeScanDate
                    != BeforeCheckup.ScanDate
                || Comparison.AfterScanDate
                    != AfterCheckup.ScanDate)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    TechnicianSummary)
                || string.IsNullOrWhiteSpace(
                    NextSteps)
                || !NextCheckupDate.HasValue)
            {
                return false;
            }

            return true;
        }
    }

    [JsonIgnore]
    public string ReportArchiveStatusText =>
        !IsCompleted
            ? "Kein abgeschlossener Kundencheckup"
            : CanRegenerateReport
                ? "Bericht erneut erstellbar"
                : "Berichtsdaten unvollständig";

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
        StartedAt.ToString("dd.MM.yyyy HH:mm")
        + " Uhr";

    [JsonIgnore]
    public string CompletedAtText =>
        CompletedAt.HasValue
            ? CompletedAt.Value.ToString("dd.MM.yyyy HH:mm")
              + " Uhr"
            : "Noch nicht abgeschlossen";

    [JsonIgnore]
    public string NextCheckupDateText =>
        NextCheckupDate.HasValue
            ? NextCheckupDate.Value.ToString("dd.MM.yyyy")
            : "Noch nicht festgelegt";

    // =============================================
    // NEUEN KUNDENCHECKUP BEGINNEN
    // =============================================

    public static CustomerCheckupVisit Start(
        CheckupSession beforeCheckup)
    {
        ArgumentNullException.ThrowIfNull(beforeCheckup);

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
                CheckupSnapshot.Capture(beforeCheckup)
        };
    }

    // =============================================
    // ZENTRALE KOPIERMETHODE
    // =============================================

    /// <summary>
    /// Erstellt eine neue Instanz des Kundencheckups
    /// für die weitere Vorgangsbearbeitung.
    ///
    /// Die bestehenden Scan- und Vergleichsobjekte
    /// werden wie bisher übernommen.
    ///
    /// Programmupdate-Ausnahmen sowie sämtliche
    /// Zwischenprüfungen werden einzeln kopiert.
    ///
    /// Die Vorgangs-ID und die archivierten
    /// Entscheidungsdaten bleiben unverändert.
    /// </summary>
    public CustomerCheckupVisit CreateIndependentCopy()
    {
        return new CustomerCheckupVisit
        {
            VisitModelVersion =
                VisitModelVersion,

            Id =
                Id,

            StartedAt =
                StartedAt,

            CompletedAt =
                CompletedAt,

            Status =
                Status,

            BeforeCheckup =
                BeforeCheckup,

            AfterCheckup =
                AfterCheckup,

            Comparison =
                Comparison,

            TechnicianSummary =
                TechnicianSummary,

            NextSteps =
                NextSteps,

            NextCheckupDate =
                NextCheckupDate,

            CancellationReason =
                CancellationReason,

            ProgramUpdateExceptions =
                ProgramUpdateExceptions
                    .Select(CloneProgramUpdateException)
                    .ToList(),

            ProgramUpdateIntermediateScans =
                ProgramUpdateIntermediateScans
                    .Select(scan =>
                        scan.CreateIndependentCopy())
                    .ToList(),

            CleanupPotentialIntermediateScans =
                CleanupPotentialIntermediateScans
                    .Select(scan =>
                        scan.CreateIndependentCopy())
                    .ToList()
        };
    }

    // =============================================
    // PROGRAMMUPDATE-AUSNAHME KOPIEREN
    // =============================================

    private static ProgramUpdateException
        CloneProgramUpdateException(
            ProgramUpdateException source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new ProgramUpdateException
        {
            Id =
                source.Id,

            CheckupVisitId =
                source.CheckupVisitId,

            PackageId =
                source.PackageId,

            PackageName =
                source.PackageName,

            InstalledVersion =
                source.InstalledVersion,

            AvailableVersion =
                source.AvailableVersion,

            Source =
                source.Source,

            Reason =
                source.Reason,

            TechnicianNote =
                source.TechnicianNote,

            ApprovedAt =
                source.ApprovedAt
        };
    }

    // =============================================
    // WINGet-ZWISCHENPRÜFUNG ERFASSEN
    // =============================================

    /// <summary>
    /// Übernimmt ein neues WinGet-Analyseergebnis
    /// als unabhängige Zwischenprüfung.
    ///
    /// Auch fehlgeschlagene Zwischenprüfungen
    /// können dokumentiert werden. Sie gelten
    /// jedoch nicht als bestätigter Updatezustand.
    ///
    /// Nach einer vorbereiteten Abschlusskontrolle
    /// oder einem endgültigen Abschluss sind keine
    /// weiteren Zwischenprüfungen zulässig.
    /// </summary>
    public ProgramUpdateIntermediateScan
        RecordProgramUpdateIntermediateScan(
            ProgramUpdateInformation information)
    {
        ArgumentNullException.ThrowIfNull(information);

        EnsureInProgress();

        if (IsCompletionPrepared)
        {
            throw new InvalidOperationException(
                "Nach der vorbereiteten Abschlusskontrolle "
                + "dürfen keine weiteren WinGet-"
                + "Zwischenprüfungen erfasst werden.");
        }

        var intermediateScan =
            ProgramUpdateIntermediateScan.Capture(
                Id,
                information);

        ProgramUpdateIntermediateScans.Add(
            intermediateScan);

        VisitModelVersion =
            CurrentVisitModelVersion;

        return intermediateScan.CreateIndependentCopy();
    }

    // =============================================
    // BEREINIGUNGS-ZWISCHENPRÜFUNG ERFASSEN
    // =============================================

    /// <summary>
    /// Erfasst eine eigenständige Nachmessung
    /// einer Bereinigungskategorie.
    ///
    /// Die ursprüngliche Eingangsmessung
    /// wird ausschließlich aus dem bereits
    /// gesicherten Eingangsscan übernommen.
    ///
    /// Dadurch kann der aufrufende Dienst
    /// keine beliebige Vorher-Messung
    /// als ursprünglichen Zustand ausgeben.
    ///
    /// Die neue Zwischenprüfung enthält
    /// unabhängige Kopien aller Messergebnisse.
    ///
    /// Die eigentliche Datenbankspeicherung
    /// erfolgt anschließend durch den
    /// aufrufenden Anwendungsdienst.
    /// </summary>
    public CleanupPotentialIntermediateScan
        RecordCleanupPotentialIntermediateScan(
            CleanupCategoryResult afterMeasurement,
            DateTime? afterMeasuredAt,
            CleanupActionCategoryExecutionResult?
                recordedExecution = null)
    {
        ArgumentNullException.ThrowIfNull(
            afterMeasurement);

        EnsureInProgress();

        // =========================================
        // VORBEREITETEN ABSCHLUSS SCHÜTZEN
        // =========================================

        if (IsCompletionPrepared)
        {
            throw new InvalidOperationException(
                "Nach der vorbereiteten Abschlusskontrolle "
                + "dürfen keine neuen Bereinigungs-"
                + "Zwischenprüfungen erfasst werden.");
        }

        // =========================================
        // ORIGINALEN EINGANGSSCAN HERANZIEHEN
        // =========================================

        var beforeInformation =
            BeforeCheckup?.CleanupPotentialInformation;

        if (beforeInformation is null
            || !beforeInformation.HasAnalysis
            || beforeInformation.Categories is null)
        {
            throw new InvalidOperationException(
                "Der ursprüngliche Eingangsscan "
                + "enthält keine verwertbare "
                + "Bereinigungsanalyse.");
        }

        /*
         * Der Eingangswert wird bewusst
         * nicht als Parameter übergeben.
         *
         * Nur der gespeicherte ursprüngliche
         * Kundencheckup gilt als Quelle.
         */
        var beforeMeasurement =
            beforeInformation.Categories
                .SingleOrDefault(
                    category =>
                        category.Category
                        == afterMeasurement.Category);

        if (beforeMeasurement is null)
        {
            throw new InvalidOperationException(
                "Die gewählte Bereinigungskategorie "
                + "ist im ursprünglichen Eingangsscan "
                + "nicht vorhanden.");
        }

        // =========================================
        // TECHNISCHE AKTION ZUORDNEN
        // =========================================

        if (recordedExecution is not null
            && recordedExecution.Category
                != afterMeasurement.Category)
        {
            throw new InvalidOperationException(
                "Die technische Bereinigungsaktion "
                + "gehört nicht zur ausgewählten "
                + "Bereinigungskategorie.");
        }

        // =========================================
        // UNABHÄNGIGEN MESSDATENSATZ ERSTELLEN
        // =========================================

        var intermediateScan =
            CleanupPotentialIntermediateScan.Capture(
                Id,
                beforeMeasurement,
                beforeInformation.AnalysisDate,
                afterMeasurement,
                afterMeasuredAt,
                recordedExecution);

        CleanupPotentialIntermediateScans.Add(
            intermediateScan);

        VisitModelVersion =
            CurrentVisitModelVersion;

        return intermediateScan.CreateIndependentCopy();
    }

    // =============================================
    // PROGRAMMUPDATE-AUSNAHMEN
    // =============================================

    /// <summary>
    /// Dokumentiert eine begründete Ausnahme
    /// für die exakt vorliegende Paketversion.
    ///
    /// Ein anderer Versionsstand wird dadurch
    /// ausdrücklich nicht freigegeben.
    /// </summary>
    public void ApproveProgramUpdateException(
        AvailableProgramUpdate update,
        string reason,
        string technicianNote)
    {
        EnsureInProgress();

        if (IsCompletionPrepared)
        {
            throw new InvalidOperationException(
                "Nach der vorbereiteten Abschlusskontrolle "
                + "dürfen keine neuen Programmupdate-Ausnahmen "
                + "mehr erfasst werden.");
        }

        ArgumentNullException.ThrowIfNull(update);

        if (string.IsNullOrWhiteSpace(update.PackageId)
            || string.IsNullOrWhiteSpace(update.InstalledVersion)
            || string.IsNullOrWhiteSpace(update.AvailableVersion)
            || string.IsNullOrWhiteSpace(update.Source))
        {
            throw new ArgumentException(
                "Für eine Programmupdate-Ausnahme sind "
                + "Paketkennung, Quelle und beide "
                + "Versionsstände erforderlich.",
                nameof(update));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "Für die bewusste Nichtaktualisierung "
                + "muss eine fachliche Begründung "
                + "dokumentiert werden.",
                nameof(reason));
        }

        // Eine erneute Freigabe desselben Updates
        // ersetzt nur dessen vorherige Freigabe.
        // Historische Entscheidungen zu anderen
        // Paketversionen bleiben erhalten.
        var existingApproval =
            ProgramUpdateExceptions.FirstOrDefault(
                approval =>
                    approval.AppliesTo(Id, update));

        if (existingApproval is not null)
        {
            ProgramUpdateExceptions.Remove(
                existingApproval);
        }

        ProgramUpdateExceptions.Add(
            new ProgramUpdateException
            {
                Id = existingApproval?.Id
                    ?? Guid.NewGuid(),

                CheckupVisitId = Id,

                PackageId = update.PackageId.Trim(),

                PackageName = update.Name?.Trim()
                    ?? string.Empty,

                InstalledVersion =
                    update.InstalledVersion.Trim(),

                AvailableVersion =
                    update.AvailableVersion.Trim(),

                Source = update.Source.Trim(),

                Reason = reason.Trim(),

                TechnicianNote =
                    technicianNote?.Trim()
                    ?? string.Empty,

                ApprovedAt = DateTime.Now
            });

        VisitModelVersion =
            CurrentVisitModelVersion;
    }

    // =============================================
    // PROGRAMMUPDATE-AUSNAHME WIDERRUFEN
    // =============================================

    /// <summary>
    /// Widerruft eine Freigabe im laufenden
    /// Kundencheckup.
    ///
    /// Abgeschlossene Vorgänge bleiben
    /// unveränderlich.
    /// </summary>
    public bool RevokeProgramUpdateException(
        Guid approvalId)
    {
        EnsureInProgress();

        if (IsCompletionPrepared)
        {
            throw new InvalidOperationException(
                "Nach der vorbereiteten Abschlusskontrolle "
                + "dürfen Programmupdate-Ausnahmen "
                + "nicht mehr geändert werden.");
        }

        var removed =
            ProgramUpdateExceptions.RemoveAll(
                approval =>
                    approval.Id == approvalId
                    && approval.CheckupVisitId == Id) > 0;

        if (removed)
        {
            VisitModelVersion =
                CurrentVisitModelVersion;
        }

        return removed;
    }

    // =============================================
    // VERGLEICH UND ABSCHLUSS
    // =============================================

    public void StoreComparison(
        CustomerCheckupComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);

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
        ArgumentNullException.ThrowIfNull(afterCheckup);
        ArgumentNullException.ThrowIfNull(comparison);

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
            CheckupSnapshot.Capture(afterCheckup);

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

    public void Cancel(string reason)
    {
        EnsureInProgress();

        if (string.IsNullOrWhiteSpace(reason))
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

    // =============================================
    // VORGANGSSTATUS PRÜFEN
    // =============================================

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

    // =============================================
    // VERGLEICHSERGEBNIS VALIDIEREN
    // =============================================

    private void ValidateComparison(
        CustomerCheckupComparison comparison,
        DateTime? expectedAfterScanDate)
    {
        if (comparison.CustomerCheckupVisitId != Id)
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

    // =============================================
    // ABSCHLUSSDATEN VALIDIEREN
    // =============================================

    private static void ValidateCompletionDetails(
        string technicianSummary,
        string nextSteps,
        DateTime? nextCheckupDate,
        DateTime afterScanDate)
    {
        if (string.IsNullOrWhiteSpace(technicianSummary))
        {
            throw new ArgumentException(
                "Für den Abschlussentwurf ist eine "
                + "Technikerzusammenfassung erforderlich.",
                nameof(technicianSummary));
        }

        if (string.IsNullOrWhiteSpace(nextSteps))
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

    // =============================================
    // ABSCHLUSSDATEN ÜBERNEHMEN
    // =============================================

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

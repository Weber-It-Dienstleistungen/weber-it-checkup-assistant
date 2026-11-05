
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

/// <summary>
/// Sprint 55.14.34r
///
/// Dokumentiert eine eigenständige
/// Zwischenprüfung einer Bereinigungskategorie.
///
/// Eingangsmessung, Nachmessung und optional
/// das technische Ausführungsergebnis werden
/// als voneinander unabhängige Kopien erfasst.
///
/// Ergänzung:
/// Eine zugeordnete Bereinigungsaktion kann
/// durch Aktions-ID, Plan-ID und vollständigen
/// Ausführungszeitraum identifiziert werden.
///
/// Die bloße Existenz eines Kategorieergebnisses
/// gilt nicht als vollständige Nachweisreferenz.
///
/// Diese Klasse führt keine Scans und
/// keine Dateisystemänderungen aus.
///
/// Eine Verringerung zwischen zwei Messungen
/// wird nicht automatisch vollständig einer
/// bestimmten Löschaktion zugeschrieben.
/// </summary>
public sealed class CleanupPotentialIntermediateScan
{
    public Guid Id { get; set; } =
        Guid.NewGuid();

    public Guid CustomerCheckupVisitId { get; set; }

    public CleanupCategoryType Category { get; set; }

    public DateTimeOffset RecordedAt { get; set; } =
        DateTimeOffset.Now;

    public DateTime? BeforeMeasuredAt { get; set; }

    public DateTime? AfterMeasuredAt { get; set; }

    public CleanupCategoryResult BeforeMeasurement
    {
        get;
        set;
    } = new();

    public CleanupCategoryResult AfterMeasurement
    {
        get;
        set;
    } = new();

    // =============================================
    // TECHNISCHES BEREINIGUNGSERGEBNIS
    // =============================================

    /// <summary>
    /// Optional zugeordnetes Kategorieergebnis.
    ///
    /// Eine Zwischenprüfung darf auch ohne
    /// vorherige Löschaktion durchgeführt werden.
    ///
    /// Fehlt dieses Ergebnis, dürfen keine
    /// tatsächlich gelöschten Mengen behauptet
    /// werden.
    /// </summary>
    public CleanupActionCategoryExecutionResult?
        RecordedExecution
    {
        get;
        set;
    }

    // =============================================
    // SPRINT 55.14.34r
    // VOLLSTÄNDIGE NACHWEISREFERENZ
    // =============================================

    /// <summary>
    /// Kennung des ursprünglichen Eintrags
    /// im technischen Aufgabenprotokoll.
    ///
    /// Null bei historischen oder nicht
    /// eindeutig zugeordneten Ergebnissen.
    /// </summary>
    public Guid? RecordedActionResultId
    {
        get;
        set;
    }

    /// <summary>
    /// Kennung des bestätigten Bereinigungsplans.
    /// </summary>
    public Guid? RecordedPlanId
    {
        get;
        set;
    }

    /// <summary>
    /// Tatsächlicher Startzeitpunkt
    /// der dokumentierten Bereinigungsaktion.
    /// </summary>
    public DateTimeOffset? RecordedActionStartedAt
    {
        get;
        set;
    }

    /// <summary>
    /// Tatsächlicher Abschlusszeitpunkt
    /// der dokumentierten Bereinigungsaktion.
    /// </summary>
    public DateTimeOffset? RecordedActionFinishedAt
    {
        get;
        set;
    }

    // =============================================
    // VERLÄSSLICHKEIT DER MESSUNGEN
    // =============================================

    [JsonIgnore]
    public bool IsBeforeReliable =>
        IsReliableMeasurement(
            BeforeMeasurement);

    [JsonIgnore]
    public bool IsAfterReliable =>
        IsReliableMeasurement(
            AfterMeasurement);

    [JsonIgnore]
    public bool IsComparisonReliable =>
        BeforeMeasurement is not null
        && AfterMeasurement is not null
        && BeforeMeasurement.Category == Category
        && AfterMeasurement.Category == Category
        && IsBeforeReliable
        && IsAfterReliable;

    // =============================================
    // VERLÄSSLICHKEIT DES LÖSCHNACHWEISES
    // =============================================

    /// <summary>
    /// Ein Kategorieergebnis liegt vor.
    ///
    /// Dies allein bestätigt noch keine
    /// vollständig nachvollziehbare
    /// Aktionszuordnung.
    /// </summary>
    [JsonIgnore]
    public bool HasRecordedExecution =>
        RecordedExecution is not null
        && RecordedExecution.Category == Category;

    /// <summary>
    /// Alle zur Identifizierung einer
    /// Bereinigungsaktion erforderlichen
    /// Referenzdaten sind vorhanden.
    ///
    /// Die Eigenschaft prüft die innere
    /// Konsistenz dieses Datensatzes.
    ///
    /// Eine zusätzliche Überprüfung gegen
    /// das ursprüngliche Aufgabenprotokoll
    /// bleibt Aufgabe des Anwendungsdienstes.
    /// </summary>
    [JsonIgnore]
    public bool HasCompleteExecutionReference =>
        HasRecordedExecution
        && RecordedExecution!.WasStarted
        && RecordedActionResultId is Guid actionId
        && actionId != Guid.Empty
        && RecordedPlanId is Guid planId
        && planId != Guid.Empty
        && RecordedActionStartedAt
            is DateTimeOffset startedAt
        && RecordedActionFinishedAt
            is DateTimeOffset finishedAt
        && finishedAt >= startedAt;

    // =============================================
    // GEMESSENE VERÄNDERUNGEN
    // =============================================

    [JsonIgnore]
    public bool? HasSizeDecreased =>
        !IsComparisonReliable
            ? null
            : BeforeMeasurement.SizeBytes!.Value
              > AfterMeasurement.SizeBytes!.Value;

    [JsonIgnore]
    public bool? HasSizeIncreased =>
        !IsComparisonReliable
            ? null
            : AfterMeasurement.SizeBytes!.Value
              > BeforeMeasurement.SizeBytes!.Value;

    [JsonIgnore]
    public ulong? AbsoluteSizeChangeBytes
    {
        get
        {
            if (!IsComparisonReliable)
            {
                return null;
            }

            var before =
                BeforeMeasurement.SizeBytes!.Value;

            var after =
                AfterMeasurement.SizeBytes!.Value;

            return before >= after
                ? before - after
                : after - before;
        }
    }

    [JsonIgnore]
    public decimal? FileCountChange
    {
        get
        {
            if (!IsComparisonReliable)
            {
                return null;
            }

            return
                (decimal)BeforeMeasurement.FileCount!.Value
                - AfterMeasurement.FileCount!.Value;
        }
    }

    // =============================================
    // VERSTÄNDLICHER MESSVERGLEICH
    // =============================================

    [JsonIgnore]
    public string ComparisonSummary
    {
        get
        {
            if (!IsComparisonReliable)
            {
                return
                    "Die Messwerte sind nicht vollständig "
                    + "vergleichbar. Es wird keine "
                    + "bestätigte Größenänderung ausgewiesen.";
            }

            var difference =
                AbsoluteSizeChangeBytes ?? 0;

            var fileDifference =
                FileCountChange ?? 0;

            var fileDescription =
                fileDifference > 0
                    ? $"{fileDifference:N0} Dateien weniger"
                    : fileDifference < 0
                        ? $"{Math.Abs(fileDifference):N0} "
                          + "Dateien mehr"
                        : "Dateianzahl unverändert";

            const string measurementNotice =
                "Zwischen den Messzeitpunkten können "
                + "Dateien auch durch Windows oder "
                + "andere Anwendungen verändert worden sein.";

            // =====================================
            // UNVERÄNDERTE DATENMENGE
            // =====================================

            if (difference == 0)
            {
                if (fileDifference == 0)
                {
                    return
                        "Gemessene Datenmenge und "
                        + "Dateianzahl sind unverändert. "
                        + measurementNotice;
                }

                return
                    "Die gemessene Datenmenge ist "
                    + "unverändert. "
                    + fileDescription
                    + ". "
                    + measurementNotice;
            }

            // =====================================
            // VERRINGERUNG ODER VERGRÖSSERUNG
            // =====================================

            var direction =
                HasSizeDecreased == true
                    ? "verringert"
                    : "erhöht";

            return
                "Der gemessene Datenbestand hat sich "
                + $"um {FormatBytes(difference)} "
                + $"{direction}. "
                + fileDescription
                + ". "
                + measurementNotice;
        }
    }

    // =============================================
    // ZWISCHENPRÜFUNG ERFASSEN
    // =============================================

    public static CleanupPotentialIntermediateScan Capture(
        Guid customerCheckupVisitId,
        CleanupCategoryResult beforeMeasurement,
        DateTime? beforeMeasuredAt,
        CleanupCategoryResult afterMeasurement,
        DateTime? afterMeasuredAt,
        CleanupActionCategoryExecutionResult?
            recordedExecution = null,
        Guid? recordedActionResultId = null,
        Guid? recordedPlanId = null,
        DateTimeOffset? recordedActionStartedAt = null,
        DateTimeOffset? recordedActionFinishedAt = null)
    {
        // =========================================
        // KUNDENCHECKUP VALIDIEREN
        // =========================================

        if (customerCheckupVisitId == Guid.Empty)
        {
            throw new ArgumentException(
                "Eine gültige Kundencheckup-ID "
                + "ist erforderlich.",
                nameof(customerCheckupVisitId));
        }

        ArgumentNullException.ThrowIfNull(
            beforeMeasurement);

        ArgumentNullException.ThrowIfNull(
            afterMeasurement);

        // =========================================
        // KATEGORIEN PRÜFEN
        // =========================================

        if (beforeMeasurement.Category
            != afterMeasurement.Category)
        {
            throw new InvalidOperationException(
                "Die Vorher- und Nachher-Messung "
                + "gehören nicht zur selben "
                + "Bereinigungskategorie.");
        }

        if (recordedExecution is not null
            && recordedExecution.Category
                != beforeMeasurement.Category)
        {
            throw new InvalidOperationException(
                "Die technische Aktion gehört nicht "
                + "zur gemessenen Bereinigungskategorie.");
        }

        // =========================================
        // NACHWEISREFERENZ PRÜFEN
        // =========================================

        /*
         * Historische Datensätze dürfen
         * weiterhin ein Kategorieergebnis
         * ohne Aktions- und Plan-ID enthalten.
         *
         * Sobald allerdings auch nur eine
         * Referenzangabe übergeben wird,
         * müssen sämtliche Angaben gültig
         * und vollständig sein.
         *
         * Eine teilweise Referenz darf nicht
         * als Nachweis gespeichert werden.
         */
        var hasReferenceData =
            recordedActionResultId.HasValue
            || recordedPlanId.HasValue
            || recordedActionStartedAt.HasValue
            || recordedActionFinishedAt.HasValue;

        if (hasReferenceData)
        {
            if (recordedExecution is null
                || !recordedExecution.WasStarted
                || recordedActionResultId is not Guid actionId
                || actionId == Guid.Empty
                || recordedPlanId is not Guid planId
                || planId == Guid.Empty
                || recordedActionStartedAt
                    is not DateTimeOffset startedAt
                || recordedActionFinishedAt
                    is not DateTimeOffset finishedAt
                || finishedAt < startedAt)
            {
                throw new InvalidOperationException(
                    "Der strukturierte Bereinigungsnachweis "
                    + "besitzt keine vollständige und "
                    + "widerspruchsfreie Referenz auf "
                    + "Aktions-ID, Plan-ID und "
                    + "Ausführungszeitraum.");
            }
        }

        // =========================================
        // UNABHÄNGIGEN DATENSATZ ERZEUGEN
        // =========================================

        return new CleanupPotentialIntermediateScan
        {
            Id =
                Guid.NewGuid(),

            CustomerCheckupVisitId =
                customerCheckupVisitId,

            Category =
                beforeMeasurement.Category,

            RecordedAt =
                DateTimeOffset.Now,

            BeforeMeasuredAt =
                beforeMeasuredAt,

            AfterMeasuredAt =
                afterMeasuredAt,

            BeforeMeasurement =
                CopyMeasurement(
                    beforeMeasurement),

            AfterMeasurement =
                CopyMeasurement(
                    afterMeasurement),

            RecordedExecution =
                recordedExecution is null
                    ? null
                    : CopyExecution(
                        recordedExecution),

            RecordedActionResultId =
                recordedActionResultId,

            RecordedPlanId =
                recordedPlanId,

            RecordedActionStartedAt =
                recordedActionStartedAt,

            RecordedActionFinishedAt =
                recordedActionFinishedAt
        };
    }

    // =============================================
    // UNABHÄNGIGE KOPIE
    // =============================================

    public CleanupPotentialIntermediateScan
        CreateIndependentCopy()
    {
        return new CleanupPotentialIntermediateScan
        {
            Id =
                Id,

            CustomerCheckupVisitId =
                CustomerCheckupVisitId,

            Category =
                Category,

            RecordedAt =
                RecordedAt,

            BeforeMeasuredAt =
                BeforeMeasuredAt,

            AfterMeasuredAt =
                AfterMeasuredAt,

            BeforeMeasurement =
                CopyMeasurement(
                    BeforeMeasurement),

            AfterMeasurement =
                CopyMeasurement(
                    AfterMeasurement),

            RecordedExecution =
                RecordedExecution is null
                    ? null
                    : CopyExecution(
                        RecordedExecution),

            RecordedActionResultId =
                RecordedActionResultId,

            RecordedPlanId =
                RecordedPlanId,

            RecordedActionStartedAt =
                RecordedActionStartedAt,

            RecordedActionFinishedAt =
                RecordedActionFinishedAt
        };
    }

    // =============================================
    // MESSQUALITÄT PRÜFEN
    // =============================================

    private static bool IsReliableMeasurement(
        CleanupCategoryResult? measurement)
    {
        return measurement is not null
            && measurement.MeasurementStatus
                == CleanupMeasurementStatus.Measured
            && measurement.SizeBytes.HasValue
            && measurement.FileCount.HasValue
            && measurement.FileCount.Value >= 0;
    }

    // =============================================
    // MESSERGEBNIS KOPIEREN
    // =============================================

    private static CleanupCategoryResult CopyMeasurement(
        CleanupCategoryResult source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new CleanupCategoryResult
        {
            Category =
                source.Category,

            Classification =
                source.Classification,

            MeasurementStatus =
                source.MeasurementStatus,

            SizeBytes =
                source.SizeBytes,

            FileCount =
                source.FileCount,

            Description =
                source.Description
        };
    }

    // =============================================
    // TECHNISCHES ERGEBNIS KOPIEREN
    // =============================================

    private static CleanupActionCategoryExecutionResult
        CopyExecution(
            CleanupActionCategoryExecutionResult source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new CleanupActionCategoryExecutionResult
        {
            Category =
                source.Category,

            CategoryTitle =
                source.CategoryTitle,

            TargetPath =
                source.TargetPath,

            WasStarted =
                source.WasStarted,

            WasCancelled =
                source.WasCancelled,

            DeletedFileCount =
                source.DeletedFileCount,

            DeletedDirectoryCount =
                source.DeletedDirectoryCount,

            DeletedSizeBytes =
                source.DeletedSizeBytes,

            FailedEntryCount =
                source.FailedEntryCount,

            SkippedEntryCount =
                source.SkippedEntryCount,

            ErrorMessage =
                source.ErrorMessage,

            SkippedEntryReasons =
                (source.SkippedEntryReasons
                 ?? new List<CleanupSkippedEntryReasonCount>())
                    .Select(reason =>
                        new CleanupSkippedEntryReasonCount
                        {
                            Reason =
                                reason.Reason,

                            Count =
                                reason.Count
                        })
                    .ToList()
        };
    }

    // =============================================
    // DATEIGRÖSSE FORMATIEREN
    // =============================================

    private static string FormatBytes(ulong bytes)
    {
        const double kilobyte = 1024d;
        const double megabyte = kilobyte * 1024d;
        const double gigabyte = megabyte * 1024d;

        if (bytes >= (ulong)gigabyte)
        {
            return $"{bytes / gigabyte:0.##} GB";
        }

        if (bytes >= (ulong)megabyte)
        {
            return $"{bytes / megabyte:0.##} MB";
        }

        if (bytes >= (ulong)kilobyte)
        {
            return $"{bytes / kilobyte:0.##} KB";
        }

        return $"{bytes:N0} Byte";
    }
}

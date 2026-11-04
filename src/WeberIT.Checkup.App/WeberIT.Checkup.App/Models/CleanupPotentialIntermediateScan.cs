
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

/// <summary>
/// Sprint 55.14.34a
///
/// Dokumentiert eine eigenständige
/// Zwischenprüfung einer Bereinigungskategorie.
///
/// Eingangsmessung, Nachmessung und optional
/// das technische Ausführungsergebnis werden
/// als voneinander unabhängige Kopien erfasst.
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

    /// <summary>
    /// Optional zugeordnete technische Aktion.
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

    [JsonIgnore]
    public bool HasRecordedExecution =>
        RecordedExecution is not null
        && RecordedExecution.Category == Category;

    // =============================================
    // GEMESSENE VERÄNDERUNGEN
    // =============================================

    /// <summary>
    /// Positive Werte bedeuten eine Verringerung.
    ///
    /// Null bedeutet einen unveränderten Bestand.
    ///
    /// Ein negativer Änderungswert wird nicht
    /// durch einen Überlauf von ulong erzeugt:
    /// Die Richtung wird separat bereitgestellt.
    /// </summary>
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

            var direction =
                HasSizeDecreased == true
                    ? "verringert"
                    : HasSizeIncreased == true
                        ? "erhöht"
                        : "nicht verändert";

            var fileDifference =
                FileCountChange ?? 0;

            var fileDescription =
                fileDifference > 0
                    ? $"{fileDifference:N0} Dateien weniger"
                    : fileDifference < 0
                        ? $"{Math.Abs(fileDifference):N0} "
                          + "Dateien mehr"
                        : "Dateianzahl unverändert";

            return
                "Der gemessene Datenbestand hat sich "
                + $"um {FormatBytes(difference)} "
                + $"{direction}. "
                + $"{fileDescription}. "
                + "Zwischen den Messzeitpunkten können "
                + "Dateien auch durch Windows oder "
                + "andere Anwendungen verändert worden sein.";
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
            recordedExecution = null)
    {
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
                        recordedExecution)
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
                        RecordedExecution)
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

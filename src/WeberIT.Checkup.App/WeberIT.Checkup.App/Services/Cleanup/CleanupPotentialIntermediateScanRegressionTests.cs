
#if DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Cleanup;

/// <summary>
/// Sprint 55.14.34j
///
/// Isolierte Regressionstests für die
/// Bereinigungs-Zwischenprüfungen.
///
/// Ausschließlich künstliche Messdaten.
///
/// Keine echten Dateisystemzugriffe.
/// Keine Bereinigung.
/// Keine Datenbankzugriffe.
/// Keine Administratorrechte erforderlich.
///
/// Die Tests werden ausschließlich durch
/// einen ausdrücklichen RunAll()-Aufruf
/// ausgeführt.
/// </summary>
public static class CleanupPotentialIntermediateScanRegressionTests
{
    private static readonly DateTime EntranceDate =
        new(
            2026, 11, 4, 10, 0, 0,
            DateTimeKind.Local);

    private static readonly DateTime ControlDate =
        EntranceDate.AddMinutes(30);

    private const CleanupCategoryType TestCategory =
        CleanupCategoryType.WindowsTemporaryFiles;

    // =============================================
    // TESTSAMMLUNG
    // =============================================

    public static string RunAll()
    {
        var results = new List<string>();

        Execute(
            "Verringerung korrekt berechnet",
            TestDecreasedSize,
            results);

        Execute(
            "Unveraenderter Bestand korrekt erkannt",
            TestUnchangedSize,
            results);

        Execute(
            "Vergroesserung ohne Unterlauf erkannt",
            TestIncreasedSize,
            results);

        Execute(
            "Teilmessung verhindert bestaetigten Vergleich",
            TestPartialMeasurement,
            results);

        Execute(
            "Fehlende Messwerte verhindern Nullbefund",
            TestMissingMeasurement,
            results);

        Execute(
            "Eingangsscan und Eingabeobjekt bleiben unabhaengig",
            TestSourceIndependence,
            results);

        Execute(
            "Rueckgabekopie aendert Speicherung nicht",
            TestReturnedCopyIndependence,
            results);

        Execute(
            "Mehrere Zwischenpruefungen bleiben erhalten",
            TestMultipleScans,
            results);

        Execute(
            "Vorgangskopie besitzt unabhaengige Messungen",
            TestVisitCopy,
            results);

        Execute(
            "JSON erhaelt Messungen und Zuordnung",
            TestJsonRoundTrip,
            results);

        Execute(
            "Historischer Kundencheckup bleibt lesbar",
            TestHistoricalVisit,
            results);

        Execute(
            "Abschluss- und Kategorie-Schutz greifen",
            TestGuards,
            results);

        Execute(
            "Optionales Loeschprotokoll wird tief kopiert",
            TestExecutionCopy,
            results);

        return
            $"Alle {results.Count} Regressionstests "
            + "fuer Bereinigungs-Zwischenpruefungen "
            + "erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =============================================
    // 1. VERRINGERUNG
    // =============================================

    private static void TestDecreasedSize()
    {
        var visit = CreateVisit();

        var scan =
            visit.RecordCleanupPotentialIntermediateScan(
                CreateMeasurement(
                    4_000_000,
                    27),
                ControlDate);

        Require(
            scan.IsComparisonReliable,
            "Beide Messungen muessen vergleichbar sein.");

        Require(
            scan.HasSizeDecreased == true,
            "Die Verringerung wurde nicht erkannt.");

        Require(
            scan.HasSizeIncreased == false,
            "Es darf keine Vergroesserung gemeldet werden.");

        Require(
            scan.AbsoluteSizeChangeBytes == 12_000_000,
            "Die Groessendifferenz stimmt nicht.");

        Require(
            scan.FileCountChange == 130,
            "Die Dateianzahldifferenz stimmt nicht.");

        Require(
            scan.CustomerCheckupVisitId == visit.Id,
            "Die Kundencheckup-Zuordnung fehlt.");

        Require(
            !scan.HasRecordedExecution,
            "Eine Messung ohne Loeschprotokoll darf "
            + "keine technische Aktion behaupten.");
    }

    // =============================================
    // 2. UNVERAENDERTER BESTAND
    // =============================================

    private static void TestUnchangedSize()
    {
        var scan =
            CreateVisit()
                .RecordCleanupPotentialIntermediateScan(
                    CreateMeasurement(
                        16_000_000,
                        157),
                    ControlDate);

        Require(
            scan.IsComparisonReliable,
            "Die Messungen muessen vergleichbar sein.");

        Require(
            scan.AbsoluteSizeChangeBytes == 0,
            "Ein unveraenderter Bestand hat "
            + "keine Groessendifferenz.");

        Require(
            scan.FileCountChange == 0,
            "Die Dateianzahl muss unveraendert sein.");

        Require(
            scan.HasSizeDecreased == false
            && scan.HasSizeIncreased == false,
            "Ein Nullbefund darf keine "
            + "Groessenaenderung melden.");
    }

    // =============================================
    // 3. VERGROESSERUNG
    // =============================================

    private static void TestIncreasedSize()
    {
        var scan =
            CreateVisit()
                .RecordCleanupPotentialIntermediateScan(
                    CreateMeasurement(
                        17_000_000,
                        160),
                    ControlDate);

        Require(
            scan.HasSizeIncreased == true,
            "Die Vergroesserung wurde nicht erkannt.");

        Require(
            scan.HasSizeDecreased == false,
            "Es darf keine Verringerung gemeldet werden.");

        Require(
            scan.AbsoluteSizeChangeBytes == 1_000_000,
            "Die Groessendifferenz stimmt nicht.");

        Require(
            scan.FileCountChange == -3,
            "Drei zusaetzliche Dateien "
            + "muessen negativ bilanziert werden.");
    }

    // =============================================
    // 4. TEILMESSUNG
    // =============================================

    private static void TestPartialMeasurement()
    {
        var scan =
            CreateVisit()
                .RecordCleanupPotentialIntermediateScan(
                    CreateMeasurement(
                        0,
                        0,
                        CleanupMeasurementStatus.PartiallyMeasured),
                    ControlDate);

        Require(
            !scan.IsComparisonReliable,
            "Eine Teilmessung darf keinen "
            + "bestaetigten Vergleich liefern.");

        Require(
            scan.AbsoluteSizeChangeBytes is null,
            "Die Groessendifferenz muss unbekannt bleiben.");

        Require(
            scan.FileCountChange is null,
            "Die Dateidifferenz muss unbekannt bleiben.");

        Require(
            scan.HasSizeDecreased is null,
            "Eine Verringerung darf nicht "
            + "als bestaetigt gelten.");
    }

    // =============================================
    // 5. FEHLENDE MESSWERTE
    // =============================================

    private static void TestMissingMeasurement()
    {
        var scan =
            CreateVisit()
                .RecordCleanupPotentialIntermediateScan(
                    CreateMeasurement(
                        null,
                        null),
                    ControlDate);

        Require(
            !scan.IsAfterReliable,
            "Fehlende Werte duerfen nicht "
            + "als vollstaendige Messung gelten.");

        Require(
            !scan.IsComparisonReliable,
            "Ohne Werte ist kein Vergleich moeglich.");

        Require(
            scan.AbsoluteSizeChangeBytes is null,
            "Fehlende Werte duerfen nicht "
            + "als null Byte interpretiert werden.");
    }

    // =============================================
    // 6. EINGANGS- UND QUELLOBJEKTE
    // =============================================

    private static void TestSourceIndependence()
    {
        var visit = CreateVisit();

        var source =
            CreateMeasurement(
                4_000_000,
                27);

        visit.RecordCleanupPotentialIntermediateScan(
            source,
            ControlDate);

        source.SizeBytes = 999;
        source.FileCount = 999;
        source.Description = "Nachtraeglich veraendert";

        var stored =
            visit.CleanupPotentialIntermediateScans[0];

        Require(
            stored.AfterMeasurement.SizeBytes == 4_000_000,
            "Die gespeicherte Nachmessung wurde "
            + "durch das Quellobjekt veraendert.");

        Require(
            stored.AfterMeasurement.FileCount == 27,
            "Die gespeicherte Dateianzahl wurde veraendert.");

        Require(
            stored.BeforeMeasurement.SizeBytes
                == 16_000_000,
            "Die Eingangsmessung wurde veraendert.");

        Require(
            visit.BeforeCheckup
                .CleanupPotentialInformation
                .Categories[0]
                .SizeBytes == 16_000_000,
            "Der urspruengliche Eingangsscan "
            + "muss unveraendert bleiben.");
    }

    // =============================================
    // 7. RUECKGABEKOPIE
    // =============================================

    private static void TestReturnedCopyIndependence()
    {
        var visit = CreateVisit();

        var returned =
            visit.RecordCleanupPotentialIntermediateScan(
                CreateMeasurement(
                    4_000_000,
                    27),
                ControlDate);

        returned.AfterMeasurement.SizeBytes = 123;
        returned.BeforeMeasurement.SizeBytes = 456;

        var stored =
            visit.CleanupPotentialIntermediateScans[0];

        Require(
            stored.AfterMeasurement.SizeBytes == 4_000_000,
            "Die Rueckgabe darf den gespeicherten "
            + "Nachher-Wert nicht veraendern.");

        Require(
            stored.BeforeMeasurement.SizeBytes == 16_000_000,
            "Die Rueckgabe darf den gespeicherten "
            + "Vorher-Wert nicht veraendern.");
    }

    // =============================================
    // 8. MEHRERE ZWISCHENPRUEFUNGEN
    // =============================================

    private static void TestMultipleScans()
    {
        var visit = CreateVisit();

        var first =
            visit.RecordCleanupPotentialIntermediateScan(
                CreateMeasurement(
                    8_000_000,
                    70),
                ControlDate);

        var second =
            visit.RecordCleanupPotentialIntermediateScan(
                CreateMeasurement(
                    4_000_000,
                    27),
                ControlDate.AddMinutes(10));

        Require(
            visit.CleanupPotentialIntermediateScanCount == 2,
            "Beide Messungen muessen erhalten bleiben.");

        Require(
            first.Id != second.Id,
            "Jede Zwischenpruefung braucht eine eigene ID.");

        Require(
            visit.CleanupPotentialIntermediateScans[0]
                .AfterMeasurement.SizeBytes == 8_000_000,
            "Der fruehere Messstand fehlt.");

        Require(
            visit.CleanupPotentialIntermediateScans[1]
                .AfterMeasurement.SizeBytes == 4_000_000,
            "Der spaetere Messstand fehlt.");

        Require(
            visit.CleanupPotentialIntermediateScans.All(
                scan =>
                    scan.CustomerCheckupVisitId == visit.Id),
            "Die Vorgangszuordnung ist nicht einheitlich.");
    }

    // =============================================
    // 9. VORGANGSKOPIE
    // =============================================

    private static void TestVisitCopy()
    {
        var original = CreateVisit();

        original.RecordCleanupPotentialIntermediateScan(
            CreateMeasurement(
                4_000_000,
                27),
            ControlDate);

        var copy =
            original.CreateIndependentCopy();

        Require(
            copy.Id == original.Id,
            "Die Kundencheckup-ID muss erhalten bleiben.");

        Require(
            copy.CleanupPotentialIntermediateScanCount == 1,
            "Die Zwischenpruefung wurde nicht kopiert.");

        Require(
            !ReferenceEquals(
                original.CleanupPotentialIntermediateScans,
                copy.CleanupPotentialIntermediateScans),
            "Die Listen duerfen nicht identisch sein.");

        Require(
            !ReferenceEquals(
                original.CleanupPotentialIntermediateScans[0],
                copy.CleanupPotentialIntermediateScans[0]),
            "Die Scanobjekte muessen unabhaengig sein.");

        copy.CleanupPotentialIntermediateScans[0]
            .AfterMeasurement.SizeBytes = 1;

        Require(
            original.CleanupPotentialIntermediateScans[0]
                .AfterMeasurement.SizeBytes == 4_000_000,
            "Eine Kopie darf das Original nicht veraendern.");

        copy.CleanupPotentialIntermediateScans.Clear();

        Require(
            original.CleanupPotentialIntermediateScanCount == 1,
            "Das Original muss seinen Scan behalten.");
    }

    // =============================================
    // 10. JSON-SPEICHERUNG
    // =============================================

    private static void TestJsonRoundTrip()
    {
        var visit = CreateVisit();

        var original =
            visit.RecordCleanupPotentialIntermediateScan(
                CreateMeasurement(
                    4_000_000,
                    27),
                ControlDate);

        var json =
            JsonSerializer.Serialize(visit);

        var restored =
            JsonSerializer.Deserialize<CustomerCheckupVisit>(
                json);

        Require(
            restored is not null,
            "Der gespeicherte Kundencheckup "
            + "muss wieder lesbar sein.");

        Require(
            restored!.CleanupPotentialIntermediateScanCount == 1,
            "Die Zwischenpruefung fehlt nach dem Laden.");

        var restoredScan =
            restored.CleanupPotentialIntermediateScans[0];

        Require(
            restoredScan.Id == original.Id,
            "Die Messungs-ID wurde nicht erhalten.");

        Require(
            restoredScan.CustomerCheckupVisitId == restored.Id,
            "Die Kundencheckup-Zuordnung ging verloren.");

        Require(
            restoredScan.IsComparisonReliable,
            "Die geladenen Messungen muessen "
            + "weiterhin vergleichbar sein.");

        Require(
            restoredScan.AbsoluteSizeChangeBytes == 12_000_000,
            "Die Differenz stimmt nach dem Laden nicht.");

        Require(
            restoredScan.BeforeMeasuredAt == EntranceDate
            && restoredScan.AfterMeasuredAt == ControlDate,
            "Die Messzeitpunkte wurden nicht erhalten.");
    }

    // =============================================
    // 11. HISTORISCHE DATEN
    // =============================================

    private static void TestHistoricalVisit()
    {
        const string historicalJson =
            """
            {
              "VisitModelVersion": 5,
              "Status": 0
            }
            """;

        var restored =
            JsonSerializer.Deserialize<CustomerCheckupVisit>(
                historicalJson);

        Require(
            restored is not null,
            "Historische Kundendaten muessen lesbar sein.");

        Require(
            restored!.VisitModelVersion == 5,
            "Die historische Modellversion "
            + "darf nicht stillschweigend geaendert werden.");

        Require(
            restored.CleanupPotentialIntermediateScanCount == 0,
            "Fehlende Zwischenpruefungen muessen "
            + "als leere Liste gelten.");

        restored.BeforeCheckup =
            CreateVisit().BeforeCheckup;

        restored.RecordCleanupPotentialIntermediateScan(
            CreateMeasurement(
                4_000_000,
                27),
            ControlDate);

        Require(
            restored.VisitModelVersion
                == CustomerCheckupVisit.CurrentVisitModelVersion,
            "Ein bearbeiteter Vorgang muss auf "
            + "die aktuelle Modellversion wechseln.");

        Require(
            restored.CleanupPotentialIntermediateScanCount == 1,
            "Historische Vorgange muessen "
            + "erweiterbar bleiben.");
    }

    // =============================================
    // 12. SICHERHEITSGRENZEN
    // =============================================

    private static void TestGuards()
    {
        var completed = CreateVisit();

        completed.Status =
            CustomerCheckupVisitStatus.Completed;

        RequireThrows<InvalidOperationException>(
            () => completed.RecordCleanupPotentialIntermediateScan(
                CreateMeasurement(0, 0),
                ControlDate),
            "Abgeschlossene Checkups muessen gesperrt sein.");

        var cancelled = CreateVisit();

        cancelled.Status =
            CustomerCheckupVisitStatus.Cancelled;

        RequireThrows<InvalidOperationException>(
            () => cancelled.RecordCleanupPotentialIntermediateScan(
                CreateMeasurement(0, 0),
                ControlDate),
            "Abgebrochene Checkups muessen gesperrt sein.");

        var prepared = CreateVisit();

        prepared.AfterCheckup = new CheckupSnapshot();

        prepared.Comparison =
            new CustomerCheckupComparison();

        RequireThrows<InvalidOperationException>(
            () => prepared.RecordCleanupPotentialIntermediateScan(
                CreateMeasurement(0, 0),
                ControlDate),
            "Vorbereitete Abschluesse muessen gesperrt sein.");

        var mismatched = CreateVisit();

        RequireThrows<InvalidOperationException>(
            () => mismatched.RecordCleanupPotentialIntermediateScan(
                CreateMeasurement(
                    100,
                    1,
                    category: CleanupCategoryType.BrowserCache),
                ControlDate),
            "Eine fremde Kategorie darf nicht "
            + "als Windows-Temp gespeichert werden.");

        Require(
            mismatched.CleanupPotentialIntermediateScanCount == 0,
            "Abgewiesene Messungen duerfen "
            + "nicht gespeichert werden.");

        RequireThrows<ArgumentException>(
            () => CleanupPotentialIntermediateScan.Capture(
                Guid.Empty,
                CreateMeasurement(100, 1),
                EntranceDate,
                CreateMeasurement(0, 0),
                ControlDate),
            "Eine leere Vorgangs-ID muss "
            + "zurueckgewiesen werden.");
    }

    // =============================================
    // 13. TECHNISCHES LOESCHPROTOKOLL
    // =============================================

    private static void TestExecutionCopy()
    {
        var visit = CreateVisit();

        var execution =
            new CleanupActionCategoryExecutionResult
            {
                Category = TestCategory,

                CategoryTitle = "Windows-Temp",

                WasStarted = true,

                DeletedFileCount = 130,

                DeletedDirectoryCount = 3,

                DeletedSizeBytes = 12_000_000,

                SkippedEntryCount = 6,

                SkippedEntryReasons =
                    new List<CleanupSkippedEntryReasonCount>
                    {
                        new()
                        {
                            Reason =
                                CleanupSkippedEntryReason.LockedByProcess,

                            Count = 6
                        }
                    }
            };

        var returned =
            visit.RecordCleanupPotentialIntermediateScan(
                CreateMeasurement(
                    4_000_000,
                    27),
                ControlDate,
                execution);

        execution.SkippedEntryReasons.Clear();

        var stored =
            visit.CleanupPotentialIntermediateScans[0];

        Require(
            stored.HasRecordedExecution,
            "Das technische Protokoll muss zugeordnet sein.");

        Require(
            stored.RecordedExecution?.DeletedFileCount == 130,
            "Die dokumentierte Loeschmenge ging verloren.");

        Require(
            stored.RecordedExecution?.SkippedEntryReasons.Count == 1,
            "Die Schutzursachen wurden nicht kopiert.");

        returned.RecordedExecution!
            .SkippedEntryReasons.Clear();

        Require(
            stored.RecordedExecution.SkippedEntryReasons.Count == 1,
            "Die Rueckgabe darf das gespeicherte "
            + "Loeschprotokoll nicht veraendern.");

        RequireThrows<InvalidOperationException>(
            () => visit.RecordCleanupPotentialIntermediateScan(
                CreateMeasurement(4_000_000, 27),
                ControlDate,
                new CleanupActionCategoryExecutionResult
                {
                    Category =
                        CleanupCategoryType.BrowserCache
                }),
            "Ein Protokoll einer fremden Kategorie "
            + "muss abgewiesen werden.");
    }

    // =============================================
    // KUNSTLICHEN KUNDENCHECKUP ERZEUGEN
    // =============================================

    private static CustomerCheckupVisit CreateVisit()
    {
        return new CustomerCheckupVisit
        {
            Status =
                CustomerCheckupVisitStatus.InProgress,

            BeforeCheckup =
                new CheckupSnapshot
                {
                    ScanDate =
                        EntranceDate,

                    CleanupPotentialInformation =
                        new CleanupPotentialInformation
                        {
                            AnalysisDate =
                                EntranceDate,

                            AnalysisStatus =
                                CleanupMeasurementStatus.Measured,

                            Categories =
                                new List<CleanupCategoryResult>
                                {
                                    CreateMeasurement(
                                        16_000_000,
                                        157)
                                }
                        }
                }
        };
    }

    // =============================================
    // KUNSTLICHEN MESSBEFUND ERZEUGEN
    // =============================================

    private static CleanupCategoryResult CreateMeasurement(
        ulong? sizeBytes,
        long? fileCount,
        CleanupMeasurementStatus status =
            CleanupMeasurementStatus.Measured,
        CleanupCategoryType category = TestCategory)
    {
        return new CleanupCategoryResult
        {
            Category =
                category,

            Classification =
                CleanupCategoryClassification.SafePotential,

            MeasurementStatus =
                status,

            SizeBytes =
                sizeBytes,

            FileCount =
                fileCount,

            Description =
                "Ausschliesslich kuenstlicher Testbefund."
        };
    }

    // =============================================
    // TESTAUSFUEHRUNG
    // =============================================

    private static void Execute(
        string name,
        Action test,
        ICollection<string> results)
    {
        try
        {
            test();

            results.Add(
                "OK: " + name);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Regressionstest fehlgeschlagen: "
                + name
                + Environment.NewLine
                + exception.Message,
                exception);
        }
    }

    private static void Require(
        bool condition,
        string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                message);
        }
    }

    private static void RequireThrows<TException>(
        Action action,
        string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(
            message);
    }
}

#endif

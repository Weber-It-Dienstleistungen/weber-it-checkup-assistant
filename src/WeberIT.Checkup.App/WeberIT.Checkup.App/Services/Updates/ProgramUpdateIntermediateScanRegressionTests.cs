
#if DEBUG

using System.Text.Json;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Updates;

/// <summary>
/// Isolierte Regressionstests für gespeicherte
/// WinGet-Zwischenprüfungen.
///
/// Ausschließlich in Debug-Builds verfügbar.
/// Keine Datenbank, keine Kundendaten,
/// keine Dateizugriffe und keine WinGet-Aufrufe.
///
/// Alle Tests verwenden künstliche Objekte.
/// Die Ausführung erfolgt ausschließlich
/// durch einen ausdrücklichen RunAll()-Aufruf.
/// </summary>
public static class ProgramUpdateIntermediateScanRegressionTests
{
    private static readonly DateTime TestScanDate =
        new(
            2026, 10, 28, 9, 0, 0,
            DateTimeKind.Local);

    public static string RunAll()
    {
        var results = new List<string>();

        Execute(
            "Neuer Kundencheckup beginnt ohne Zwischenprüfungen",
            TestNewVisitWithoutScans,
            results);

        Execute(
            "Erfolgreiche Prüfung mit null Updates",
            TestReliableZeroUpdates,
            results);

        Execute(
            "Erfolgreiche Prüfung mit offenen Updates",
            TestReliableUpdates,
            results);

        Execute(
            "Fehlgeschlagene Prüfung bestätigt keine null Updates",
            TestFailedScan,
            results);

        Execute(
            "Abgeschnittene Ergebnisse sind nicht zuverlässig",
            TestTruncatedScan,
            results);

        Execute(
            "Widersprüchliche Paketanzahl wird erkannt",
            TestCountMismatch,
            results);

        Execute(
            "Ursprüngliches Scanobjekt bleibt unabhängig",
            TestSourceIndependence,
            results);

        Execute(
            "Zurückgegebene Kopie verändert Speicherung nicht",
            TestReturnedCopyIndependence,
            results);

        Execute(
            "Mehrere Zwischenprüfungen bleiben erhalten",
            TestMultipleScans,
            results);

        Execute(
            "Kundencheckup-Kopie enthält unabhängige Scans",
            TestVisitCopy,
            results);

        Execute(
            "JSON-Speicherung erhält Zwischenprüfungen",
            TestJsonRoundTrip,
            results);

        Execute(
            "Historischer Kundencheckup bleibt kompatibel",
            TestHistoricalVisit,
            results);

        Execute(
            "Abgeschlossener Checkup ist geschützt",
            TestCompletedVisit,
            results);

        Execute(
            "Abgebrochener Checkup ist geschützt",
            TestCancelledVisit,
            results);

        Execute(
            "Vorbereiteter Abschluss ist geschützt",
            TestPreparedCompletion,
            results);

        Execute(
            "Eingangsscan bleibt unverändert",
            TestOriginalBeforeScan,
            results);

        return
            $"Alle {results.Count} Regressionstests "
            + "für WinGet-Zwischenprüfungen erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =========================================
    // GRUNDZUSTÄNDE
    // =========================================

    private static void TestNewVisitWithoutScans()
    {
        var visit = CreateVisit();

        Require(
            !visit.HasProgramUpdateIntermediateScans,
            "Ein neuer Vorgang darf keine "
            + "Zwischenprüfungen enthalten.");

        Require(
            visit.ProgramUpdateIntermediateScanCount == 0,
            "Die Anzahl muss zunächst null sein.");
    }

    private static void TestReliableZeroUpdates()
    {
        var visit = CreateVisit();

        var scan =
            visit.RecordProgramUpdateIntermediateScan(
                CreateInformation(0));

        Require(
            scan.IsReliable,
            "Ein vollständiger Nullbefund "
            + "muss zuverlässig sein.");

        Require(
            scan.ConfirmedAvailableUpdateCount == 0,
            "Null Updates müssen ausdrücklich "
            + "bestätigt werden.");

        Require(
            scan.CustomerCheckupVisitId == visit.Id,
            "Die Vorgangszuordnung muss stimmen.");

        Require(
            scan.Id != Guid.Empty,
            "Die Zwischenprüfung benötigt "
            + "eine gültige Kennung.");
    }

    private static void TestReliableUpdates()
    {
        var visit = CreateVisit();

        var scan =
            visit.RecordProgramUpdateIntermediateScan(
                CreateInformation(3));

        Require(
            scan.IsReliable,
            "Der vollständige Scan muss "
            + "auswertbar sein.");

        Require(
            scan.ConfirmedAvailableUpdateCount == 3,
            "Drei verfügbare Updates "
            + "müssen erhalten bleiben.");

        Require(
            scan.ScanInformation.AvailableUpdates.Count == 3,
            "Die Paketliste muss vollständig sein.");
    }

    private static void TestFailedScan()
    {
        var information = CreateInformation(0);

        information.IsAnalysisSuccessful = false;

        var scan =
            CreateVisit()
                .RecordProgramUpdateIntermediateScan(
                    information);

        Require(
            !scan.IsReliable,
            "Ein fehlgeschlagener Scan "
            + "darf nicht zuverlässig sein.");

        Require(
            scan.ConfirmedAvailableUpdateCount is null,
            "Ein fehlgeschlagener Scan "
            + "darf kein bestätigter Nullbefund sein.");
    }

    private static void TestTruncatedScan()
    {
        var information = CreateInformation(2);

        information.IsResultTruncated = true;

        var scan =
            CreateVisit()
                .RecordProgramUpdateIntermediateScan(
                    information);

        Require(
            !scan.IsReliable,
            "Abgeschnittene Scanergebnisse "
            + "dürfen nicht zuverlässig sein.");

        Require(
            scan.ConfirmedAvailableUpdateCount is null,
            "Die Anzahl darf nicht als "
            + "abschließend bestätigt gelten.");
    }

    private static void TestCountMismatch()
    {
        var information = CreateInformation(2);

        information.AvailableUpdateCount = 5;

        var scan =
            CreateVisit()
                .RecordProgramUpdateIntermediateScan(
                    information);

        Require(
            !scan.IsReliable,
            "Eine widersprüchliche Paketanzahl "
            + "muss die Verlässlichkeit aufheben.");

        Require(
            scan.ConfirmedAvailableUpdateCount is null,
            "Widersprüchliche Daten dürfen keine "
            + "bestätigte Anzahl liefern.");
    }

    // =========================================
    // UNABHÄNGIGE KOPIEN
    // =========================================

    private static void TestSourceIndependence()
    {
        var visit = CreateVisit();

        var information = CreateInformation(1);

        visit.RecordProgramUpdateIntermediateScan(
            information);

        information.AvailableUpdates[0].Name =
            "Nachträglich veränderte Testanwendung";

        information.AvailableUpdates.Clear();

        var stored =
            visit.ProgramUpdateIntermediateScans[0];

        Require(
            stored.ScanInformation.AvailableUpdates.Count == 1,
            "Die gespeicherte Paketliste darf "
            + "nicht nachträglich verändert werden.");

        Require(
            stored.ScanInformation.AvailableUpdates[0].Name
                == "Künstliche Anwendung 1",
            "Die gespeicherte Paketinformation "
            + "muss unabhängig bleiben.");
    }

    private static void TestReturnedCopyIndependence()
    {
        var visit = CreateVisit();

        var returned =
            visit.RecordProgramUpdateIntermediateScan(
                CreateInformation(1));

        returned.ScanInformation
            .AvailableUpdates[0].Name =
                "Veränderte Rückgabe";

        returned.ScanInformation
            .AvailableUpdates.Clear();

        var stored =
            visit.ProgramUpdateIntermediateScans[0];

        Require(
            stored.ScanInformation.AvailableUpdates.Count == 1,
            "Änderungen an der Rückgabe dürfen "
            + "die gespeicherten Daten nicht beeinflussen.");

        Require(
            stored.ScanInformation.AvailableUpdates[0].Name
                == "Künstliche Anwendung 1",
            "Die gespeicherte Paketbezeichnung "
            + "muss unverändert bleiben.");
    }

    private static void TestMultipleScans()
    {
        var visit = CreateVisit();

        var first =
            visit.RecordProgramUpdateIntermediateScan(
                CreateInformation(4));

        var second =
            visit.RecordProgramUpdateIntermediateScan(
                CreateInformation(0));

        Require(
            visit.ProgramUpdateIntermediateScanCount == 2,
            "Beide Zwischenprüfungen "
            + "müssen gespeichert bleiben.");

        Require(
            first.Id != second.Id,
            "Jede Prüfung benötigt "
            + "eine eigene Kennung.");

        Require(
            visit.ProgramUpdateIntermediateScans[0]
                .ConfirmedAvailableUpdateCount == 4,
            "Der frühere Befund muss erhalten bleiben.");

        Require(
            visit.ProgramUpdateIntermediateScans[1]
                .ConfirmedAvailableUpdateCount == 0,
            "Der spätere Nullbefund "
            + "muss erhalten bleiben.");

        Require(
            visit.ProgramUpdateIntermediateScans.All(
                scan =>
                    scan.CustomerCheckupVisitId == visit.Id),
            "Alle Prüfungen müssen zum "
            + "gleichen Kundencheckup gehören.");
    }

    private static void TestVisitCopy()
    {
        var original = CreateVisit();

        original.RecordProgramUpdateIntermediateScan(
            CreateInformation(1));

        var copy =
            original.CreateIndependentCopy();

        Require(
            copy.Id == original.Id,
            "Die Vorgangs-ID muss erhalten bleiben.");

        Require(
            copy.ProgramUpdateIntermediateScanCount == 1,
            "Die Zwischenprüfung muss "
            + "mitkopiert werden.");

        Require(
            !ReferenceEquals(
                original.ProgramUpdateIntermediateScans,
                copy.ProgramUpdateIntermediateScans),
            "Die Listen dürfen nicht "
            + "dieselbe Instanz verwenden.");

        Require(
            !ReferenceEquals(
                original.ProgramUpdateIntermediateScans[0],
                copy.ProgramUpdateIntermediateScans[0]),
            "Die Scanobjekte müssen "
            + "unabhängig sein.");

        Require(
            original.ProgramUpdateIntermediateScans[0].Id
                == copy.ProgramUpdateIntermediateScans[0].Id,
            "Die historische Scan-ID "
            + "muss erhalten bleiben.");

        copy.ProgramUpdateIntermediateScans[0]
            .ScanInformation.AvailableUpdates[0].Name =
                "Nur in der Kopie geändert";

        Require(
            original.ProgramUpdateIntermediateScans[0]
                .ScanInformation.AvailableUpdates[0].Name
                == "Künstliche Anwendung 1",
            "Die Kopie darf das Original "
            + "nicht verändern.");
    }

    // =========================================
    // JSON UND HISTORISCHE DATEN
    // =========================================

    private static void TestJsonRoundTrip()
    {
        var original = CreateVisit();

        original.RecordProgramUpdateIntermediateScan(
            CreateInformation(2));

        original.RecordProgramUpdateIntermediateScan(
            CreateInformation(0));

        var json =
            JsonSerializer.Serialize(original);

        var restored =
            JsonSerializer.Deserialize<CustomerCheckupVisit>(
                json);

        Require(
            restored is not null,
            "Der JSON-Datensatz "
            + "muss wiederherstellbar sein.");

        Require(
            restored!.Id == original.Id,
            "Die Vorgangs-ID muss "
            + "die Serialisierung überstehen.");

        Require(
            restored.ProgramUpdateIntermediateScanCount == 2,
            "Beide Zwischenprüfungen "
            + "müssen erhalten bleiben.");

        Require(
            restored.ProgramUpdateIntermediateScans[0]
                .ConfirmedAvailableUpdateCount == 2,
            "Der ursprüngliche Updatebedarf "
            + "muss erhalten bleiben.");

        Require(
            restored.ProgramUpdateIntermediateScans[1]
                .ConfirmedAvailableUpdateCount == 0,
            "Der spätere Nullbefund "
            + "muss erhalten bleiben.");

        Require(
            restored.ProgramUpdateIntermediateScans.All(
                scan =>
                    scan.CustomerCheckupVisitId == restored.Id),
            "Die Vorgangszuordnung muss "
            + "nach dem Laden stimmen.");
    }

    private static void TestHistoricalVisit()
    {
        const string historicalJson =
            """
            {
              "VisitModelVersion": 4,
              "Status": 0
            }
            """;

        var restored =
            JsonSerializer.Deserialize<CustomerCheckupVisit>(
                historicalJson);

        Require(
            restored is not null,
            "Der historische Vorgang "
            + "muss lesbar bleiben.");

        Require(
            restored!.VisitModelVersion == 4,
            "Die historische Modellversion "
            + "darf beim Laden nicht verändert werden.");

        Require(
            restored.ProgramUpdateIntermediateScanCount == 0,
            "Fehlende Zwischenprüfungen "
            + "müssen als leere Liste gelten.");

        restored.RecordProgramUpdateIntermediateScan(
            CreateInformation(1));

        Require(
            restored.VisitModelVersion
                == CustomerCheckupVisit.CurrentVisitModelVersion,
            "Ein bearbeiteter Vorgang muss auf "
            + "die aktuelle Modellversion wechseln.");

        Require(
            restored.ProgramUpdateIntermediateScanCount == 1,
            "Der historische Vorgang muss "
            + "erweiterbar bleiben.");
    }

    // =========================================
    // ABSCHLUSSSCHUTZ
    // =========================================

    private static void TestCompletedVisit()
    {
        var visit = CreateVisit();

        visit.Status =
            CustomerCheckupVisitStatus.Completed;

        RequireThrows<InvalidOperationException>(
            () => visit.RecordProgramUpdateIntermediateScan(
                CreateInformation(1)),
            "Ein abgeschlossener Kundencheckup "
            + "darf keine neuen Prüfungen erhalten.");

        Require(
            visit.ProgramUpdateIntermediateScanCount == 0,
            "Der abgeschlossene Vorgang "
            + "muss unverändert bleiben.");
    }

    private static void TestCancelledVisit()
    {
        var visit = CreateVisit();

        visit.Status =
            CustomerCheckupVisitStatus.Cancelled;

        RequireThrows<InvalidOperationException>(
            () => visit.RecordProgramUpdateIntermediateScan(
                CreateInformation(1)),
            "Ein abgebrochener Kundencheckup "
            + "darf nicht verändert werden.");

        Require(
            visit.ProgramUpdateIntermediateScanCount == 0,
            "Der abgebrochene Vorgang "
            + "muss unverändert bleiben.");
    }

    private static void TestPreparedCompletion()
    {
        var visit = CreateVisit();

        visit.AfterCheckup =
            new CheckupSnapshot();

        visit.Comparison =
            new CustomerCheckupComparison();

        Require(
            visit.IsCompletionPrepared,
            "Der künstliche Abschlussentwurf "
            + "muss erkannt werden.");

        RequireThrows<InvalidOperationException>(
            () => visit.RecordProgramUpdateIntermediateScan(
                CreateInformation(1)),
            "Ein vorbereiteter Abschluss "
            + "darf keine Zwischenprüfung zulassen.");

        Require(
            visit.ProgramUpdateIntermediateScanCount == 0,
            "Die Prüfung darf nicht "
            + "nachträglich gespeichert werden.");
    }

    // =========================================
    // EINGANGSSCAN SCHÜTZEN
    // =========================================

    private static void TestOriginalBeforeScan()
    {
        var visit = CreateVisit();

        var originalScanDate =
            visit.BeforeCheckup.ScanDate;

        var originalPackageId =
            visit.BeforeCheckup
                .ProgramUpdateInformation
                .AvailableUpdates[0]
                .PackageId;

        visit.RecordProgramUpdateIntermediateScan(
            CreateInformation(5));

        visit.RecordProgramUpdateIntermediateScan(
            CreateInformation(0));

        Require(
            visit.BeforeCheckup.ScanDate
                == originalScanDate,
            "Der Eingangsscan-Zeitpunkt "
            + "darf nicht verändert werden.");

        Require(
            visit.BeforeCheckup
                .ProgramUpdateInformation
                .AvailableUpdateCount == 1,
            "Der Eingangsbefund "
            + "muss unverändert bleiben.");

        Require(
            visit.BeforeCheckup
                .ProgramUpdateInformation
                .AvailableUpdates[0]
                .PackageId == originalPackageId,
            "Die ursprüngliche Paket-ID "
            + "muss erhalten bleiben.");

        Require(
            visit.AfterCheckup is null,
            "Zwischenprüfungen dürfen keinen "
            + "Nachher-Scan erzeugen.");

        Require(
            visit.Comparison is null,
            "Zwischenprüfungen dürfen keinen "
            + "Abschlussvergleich erzeugen.");
    }

    // =========================================
    // KÜNSTLICHE TESTOBJEKTE
    // =========================================

    private static CustomerCheckupVisit CreateVisit()
    {
        var session = new CheckupSession
        {
            ScanDate = TestScanDate,

            ProgramUpdateInformation =
                CreateInformation(1)
        };

        return CustomerCheckupVisit.Start(session);
    }

    private static ProgramUpdateInformation
        CreateInformation(int updateCount)
    {
        var updates =
            Enumerable.Range(1, updateCount)
                .Select(index =>
                    new AvailableProgramUpdate
                    {
                        PackageId =
                            $"Example.TestPackage{index}",

                        Name =
                            $"Künstliche Anwendung {index}",

                        InstalledVersion =
                            "1.0.0",

                        AvailableVersion =
                            "2.0.0",

                        Source =
                            "winget"
                    })
                .ToList();

        return new ProgramUpdateInformation
        {
            IsWingetAvailable = true,

            WingetVersion = "v1.0-test",

            IsAnalysisPerformed = true,

            IsAnalysisSuccessful = true,

            AnalysisDate = TestScanDate.AddMinutes(5),

            AnalysisDetails =
                "Ausschließlich künstlicher Testbefund.",

            AvailableUpdateCount = updateCount,

            IsResultTruncated = false,

            AvailableUpdates = updates
        };
    }

    // =========================================
    // TESTAUSFÜHRUNG
    // =========================================

    private static void Execute(
        string name,
        Action test,
        ICollection<string> results)
    {
        try
        {
            test();

            results.Add("OK: " + name);
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
            throw new InvalidOperationException(message);
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

        throw new InvalidOperationException(message);
    }
}

#endif


#if DEBUG

using System.Globalization;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Infrastructure.Converters;

/// <summary>
/// Isolierte Regressionstests für die
/// dynamische Programmupdate-Fortschrittsanzeige.
///
/// Keine Datenbank, keine Kundendaten,
/// keine Netzwerkzugriffe und kein WinGet.
///
/// Die Tests werden ausschließlich durch
/// einen ausdrücklichen RunAll()-Aufruf
/// ausgeführt.
/// </summary>
public static class ProgramUpdateTaskDescriptionRegressionTests
{
    private const string ProgramUpdateTaskCode =
        "task.program-updates.available";

    private static readonly DateTime TestDate =
        new(2026, 10, 28, 11, 47, 0);

    public static string RunAll()
    {
        var results = new List<string>();

        Execute(
            "Ursprüngliche Darstellung ohne Zwischenprüfung",
            TestWithoutIntermediateScan,
            results);

        Execute(
            "Zwischenprüfung erkennt drei statt vier Updates",
            TestSuccessfulIntermediateScan,
            results);

        Execute(
            "Fehlgeschlagene Zwischenprüfung bestätigt keinen aktuellen Bestand",
            TestUnreliableIntermediateScan,
            results);

        Execute(
            "Erfolgreiche Wiederherstellung nach Fehler",
            TestRecoveryAfterFailure,
            results);

        Execute(
            "Zuverlässiger Nullbefund",
            TestReliableZeroUpdates,
            results);

        Execute(
            "Neu erkanntes Programmupdate",
            TestNewlyAvailableUpdate,
            results);

        Execute(
            "Geänderte Zielversion bleibt dasselbe Paket",
            TestChangedTargetVersion,
            results);

        Execute(
            "Andere Aufgaben bleiben unverändert",
            TestUnrelatedTask,
            results);

        Execute(
            "Ursprünglicher Eingangsbefund bleibt unverändert",
            TestOriginalScanPreserved,
            results);

        return
            $"Alle {results.Count} Regressionstests "
            + "für die WinGet-Fortschrittsanzeige "
            + "erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =============================================
    // TEST 1: OHNE ZWISCHENPRÜFUNG
    // =============================================

    private static void TestWithoutIntermediateScan()
    {
        var session = CreateSession(
            out var task);

        var description =
            GetDescription(session, task);

        RequireContains(
            description,
            "NOCH ZU BEARBEITEN (4)",
            "Alle vier ursprünglichen Updates "
            + "müssen weiterhin angeboten werden.");

        RequireNotContains(
            description,
            "LETZTE ZUVERLÄSSIGE ZWISCHENPRÜFUNG",
            "Ohne Zwischenprüfung darf keine "
            + "solche behauptet werden.");
    }

    // =============================================
    // TEST 2: VIER AUF DREI
    // =============================================

    private static void TestSuccessfulIntermediateScan()
    {
        var session = CreateSession(
            out var task);

        AddIntermediateScan(
            session,
            CreateThreeUpdatesInformation(),
            60);

        var description =
            GetDescription(session, task);

        RequireContains(
            description,
            "EINGANGSBEFUND: 4 Programmupdates",
            "Der Eingangsbefund muss sichtbar bleiben.");

        RequireContains(
            description,
            "WinGet meldet 3 verfügbare Programmupdates.",
            "Der neue Updatebestand muss "
            + "korrekt angezeigt werden.");

        RequireContains(
            description,
            "NOCH ZU BEARBEITEN (3)",
            "Drei Updates müssen offen bleiben.");

        RequireContains(
            description,
            "IM ZWISCHENBEFUND NICHT MEHR OFFEN (1)",
            "Der weggefallene Befund "
            + "muss separat erscheinen.");

        RequireContains(
            description,
            "Microsoft Edge",
            "Das nicht mehr angebotene Update "
            + "muss identifizierbar sein.");
    }

    // =============================================
    // TEST 3: FEHLGESCHLAGENER SCAN
    // =============================================

    private static void TestUnreliableIntermediateScan()
    {
        var session = CreateSession(
            out var task);

        AddIntermediateScan(
            session,
            CreateThreeUpdatesInformation(),
            60);

        AddIntermediateScan(
            session,
            CreateFailedInformation(),
            70);

        var description =
            GetDescription(session, task);

        RequireContains(
            description,
            "ZWISCHENPRÜFUNG NICHT ZUVERLÄSSIG",
            "Der Fehler muss sichtbar sein.");

        RequireContains(
            description,
            "kein bestätigter aktueller Updatebestand",
            "Ein fehlgeschlagener Scan darf "
            + "keinen aktuellen Bestand bestätigen.");

        RequireNotContains(
            description,
            "NOCH ZU BEARBEITEN (3)",
            "Der ältere zuverlässige Befund "
            + "darf nicht als aktuell erscheinen.");
    }

    // =============================================
    // TEST 4: WIEDERHERSTELLUNG
    // =============================================

    private static void TestRecoveryAfterFailure()
    {
        var session = CreateSession(
            out var task);

        AddIntermediateScan(
            session,
            CreateThreeUpdatesInformation(),
            60);

        AddIntermediateScan(
            session,
            CreateFailedInformation(),
            70);

        AddIntermediateScan(
            session,
            CreateThreeUpdatesInformation(),
            80);

        var description =
            GetDescription(session, task);

        RequireContains(
            description,
            "NOCH ZU BEARBEITEN (3)",
            "Nach erfolgreicher Prüfung muss "
            + "der aktuelle Bestand erscheinen.");

        RequireNotContains(
            description,
            "ZWISCHENPRÜFUNG NICHT ZUVERLÄSSIG",
            "Die ältere Fehlermeldung darf "
            + "nicht bestehen bleiben.");
    }

    // =============================================
    // TEST 5: NULL UPDATES
    // =============================================

    private static void TestReliableZeroUpdates()
    {
        var session = CreateSession(
            out var task);

        AddIntermediateScan(
            session,
            CreateInformation(),
            60);

        var description =
            GetDescription(session, task);

        RequireContains(
            description,
            "WinGet meldet 0 verfügbare Programmupdates.",
            "Der zuverlässige Nullbefund "
            + "muss erkennbar sein.");

        RequireContains(
            description,
            "Noch zur Installation angeboten: keine.",
            "Es dürfen keine offenen "
            + "Installationen behauptet werden.");

        RequireContains(
            description,
            "IM ZWISCHENBEFUND NICHT MEHR OFFEN (4)",
            "Alle vier ursprünglichen Pakete "
            + "müssen historisch nachvollziehbar sein.");
    }

    // =============================================
    // TEST 6: NEUES UPDATE
    // =============================================

    private static void TestNewlyAvailableUpdate()
    {
        var session = CreateSession(
            out var task);

        var information =
            CreateThreeUpdatesInformation();

        information.AvailableUpdates.Add(
            CreatePackage(
                "Neue Testanwendung",
                "Example.NewApplication"));

        information.AvailableUpdateCount =
            information.AvailableUpdates.Count;

        AddIntermediateScan(
            session,
            information,
            60);

        var description =
            GetDescription(session, task);

        RequireContains(
            description,
            "SEIT DEM EINGANGSSCAN NEU ERKANNT (1)",
            "Ein neues Paket muss "
            + "separat erscheinen.");

        RequireContains(
            description,
            "Neue Testanwendung",
            "Der neue Paketname muss sichtbar sein.");
    }

    // =============================================
    // TEST 7: GEÄNDERTE ZIELVERSION
    // =============================================

    private static void TestChangedTargetVersion()
    {
        var session = CreateSession(
            out var task);

        var information =
            CreateOriginalInformation();

        information.AvailableUpdates[0]
            .AvailableVersion = "3.0.0";

        AddIntermediateScan(
            session,
            information,
            60);

        var description =
            GetDescription(session, task);

        RequireContains(
            description,
            "Microsoft Edge: 1.0.0 → 3.0.0",
            "Die neue Zielversion muss "
            + "angezeigt werden.");

        RequireNotContains(
            description,
            "SEIT DEM EINGANGSSCAN NEU ERKANNT",
            "Eine andere Zielversion darf "
            + "nicht als neues Paket gelten.");

        RequireNotContains(
            description,
            "IM ZWISCHENBEFUND NICHT MEHR OFFEN",
            "Das weiterhin vorhandene Paket "
            + "darf nicht als weggefallen gelten.");
    }

    // =============================================
    // TEST 8: ANDERE AUFGABEN
    // =============================================

    private static void TestUnrelatedTask()
    {
        var session = CreateSession(
            out _);

        var task = new CheckupTask
        {
            TaskCode = "task.example.other",
            Description =
                "Ursprüngliche Beschreibung."
        };

        var description =
            GetDescription(session, task);

        Require(
            description == task.Description,
            "Andere Aufgabentypen dürfen "
            + "nicht verändert werden.");
    }

    // =============================================
    // TEST 9: EINGANGSSCAN SCHÜTZEN
    // =============================================

    private static void TestOriginalScanPreserved()
    {
        var session = CreateSession(
            out var task);

        var originalCount =
            session.ProgramUpdateInformation
                .AvailableUpdateCount;

        var originalVersion =
            session.ProgramUpdateInformation
                .AvailableUpdates[0]
                .AvailableVersion;

        var visit =
            session.CurrentCustomerCheckupVisit!;

        AddIntermediateScan(
            session,
            CreateThreeUpdatesInformation(),
            60);

        _ = GetDescription(
            session,
            task);

        Require(
            session.ProgramUpdateInformation
                .AvailableUpdateCount == originalCount,
            "Die Anzeige darf den ursprünglichen "
            + "Scan nicht verändern.");

        Require(
            session.ProgramUpdateInformation
                .AvailableUpdates[0]
                .AvailableVersion == originalVersion,
            "Die ursprüngliche Zielversion "
            + "muss erhalten bleiben.");

        Require(
            visit.BeforeCheckup
                .ProgramUpdateInformation
                .AvailableUpdateCount == 4,
            "Der archivierte Eingangsbefund "
            + "muss erhalten bleiben.");

        Require(
            task.Status == CheckupTaskStatus.Open,
            "Die Fortschrittsanzeige darf "
            + "die Aufgabe nicht abschließen.");
    }

    // =============================================
    // KÜNSTLICHE TESTDATEN
    // =============================================

    private static CheckupSession CreateSession(
        out CheckupTask task)
    {
        task = new CheckupTask
        {
            TaskCode = ProgramUpdateTaskCode,
            Title = "Verfügbare Programmupdates prüfen",
            Description = "Ursprünglicher Aufgabentext.",
            Status = CheckupTaskStatus.Open
        };

        var session = new CheckupSession
        {
            ScanDate = TestDate,

            ProgramUpdateInformation =
                CreateOriginalInformation(),

            TaskList = new CheckupTaskList
            {
                TaskListVersion = 1,

                Tasks = new List<CheckupTask>
                {
                    task
                }
            }
        };

        session.CustomerCheckupVisits.Add(
            CustomerCheckupVisit.Start(session));

        return session;
    }

    private static ProgramUpdateInformation
        CreateOriginalInformation()
    {
        return CreateInformation(
            CreatePackage(
                "Microsoft Edge",
                "Example.Edge"),

            CreatePackage(
                "Logitech G HUB",
                "Example.Logitech"),

            CreatePackage(
                "NVIDIA GeForce NOW",
                "Example.Nvidia"),

            CreatePackage(
                "TeamViewer",
                "Example.TeamViewer"));
    }

    private static ProgramUpdateInformation
        CreateThreeUpdatesInformation()
    {
        return CreateInformation(
            CreatePackage(
                "Logitech G HUB",
                "Example.Logitech"),

            CreatePackage(
                "NVIDIA GeForce NOW",
                "Example.Nvidia"),

            CreatePackage(
                "TeamViewer",
                "Example.TeamViewer"));
    }

    private static ProgramUpdateInformation
        CreateFailedInformation()
    {
        var information =
            CreateInformation();

        information.IsAnalysisSuccessful = false;
        information.IsAnalysisPerformed = false;
        information.AnalysisDate = null;
        information.AnalysisDetails =
            "Künstlicher Netzwerkfehler.";

        return information;
    }

    private static ProgramUpdateInformation
        CreateInformation(
            params AvailableProgramUpdate[] updates)
    {
        return new ProgramUpdateInformation
        {
            IsWingetAvailable = true,
            WingetVersion = "v1.0-test",
            IsAnalysisPerformed = true,
            IsAnalysisSuccessful = true,

            AnalysisDate =
                TestDate.AddMinutes(30),

            AvailableUpdateCount = updates.Length,

            AvailableUpdates =
                updates.ToList(),

            IsResultTruncated = false,

            AnalysisDetails =
                "Künstlicher Regressionstest."
        };
    }

    private static AvailableProgramUpdate CreatePackage(
        string name,
        string packageId)
    {
        return new AvailableProgramUpdate
        {
            Name = name,
            PackageId = packageId,
            InstalledVersion = "1.0.0",
            AvailableVersion = "2.0.0",
            Source = "winget"
        };
    }

    private static void AddIntermediateScan(
        CheckupSession session,
        ProgramUpdateInformation information,
        int minutesAfterOriginal)
    {
        var visit =
            session.CurrentCustomerCheckupVisit
            ?? throw new InvalidOperationException(
                "Künstlicher Kundencheckup fehlt.");

        visit.RecordProgramUpdateIntermediateScan(
            information);

        /*
         * Nur künstliche Testdaten:
         * Wir vergeben eindeutige Zeitstempel,
         * damit die chronologische Auswahl
         * zuverlässig geprüft werden kann.
         */
        visit.ProgramUpdateIntermediateScans[^1]
            .RecordedAt =
                new DateTimeOffset(
                    TestDate.AddMinutes(
                        minutesAfterOriginal));
    }

    // =============================================
    // KONVERTER AUSFÜHREN
    // =============================================

    private static string GetDescription(
        CheckupSession session,
        CheckupTask task)
    {
        var converter =
            new ProgramUpdateTaskDescriptionConverter();

        var result =
            converter.Convert(
                new object[]
                {
                    task,
                    session,
                    task.ActionResultCount
                },
                typeof(string),
                null!,
                CultureInfo.GetCultureInfo("de-DE"));

        return result as string
               ?? throw new InvalidOperationException(
                   "Der Konverter hat keine "
                   + "Textbeschreibung geliefert.");
    }

    // =============================================
    // TESTAUSFÜHRUNG
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

    private static void RequireContains(
        string text,
        string expected,
        string message)
    {
        Require(
            text.Contains(
                expected,
                StringComparison.Ordinal),
            message);
    }

    private static void RequireNotContains(
        string text,
        string unexpected,
        string message)
    {
        Require(
            !text.Contains(
                unexpected,
                StringComparison.Ordinal),
            message);
    }
}

#endif

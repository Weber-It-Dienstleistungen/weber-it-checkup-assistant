
#if DEBUG

using System;
using System.Collections.Generic;
using System.IO;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// SCHRITT 55.13.2
///
/// Regressionstests für die zentrale
/// Klassifizierung von Dateiproblemen.
///
/// Ausschließlich künstliche Testdaten.
/// Keine Dateizugriffe.
/// Keine Löschoperationen.
/// Keine Änderungen am Betriebssystem.
/// </summary>
public static class CleanupEntryIssueClassifierRegressionTests
{
    public static string RunAll()
    {
        var results = new List<string>();

        Execute(
            "Normale Dateien bleiben freigegeben",
            TestNormalAttributes,
            results);

        Execute(
            "Schreibschutz und Systemschutz erkennen",
            TestProtectedAttributes,
            results);

        Execute(
            "Dateisystemverweise sicher überspringen",
            TestReparsePoint,
            results);

        Execute(
            "Bereits entfernte Einträge überspringen",
            TestAlreadyMissing,
            results);

        Execute(
            "Prozess- und Dateisperren erkennen",
            TestLockViolations,
            results);

        Execute(
            "Nicht leere Ordner nicht erzwingen",
            TestDirectoryNotEmpty,
            results);

        Execute(
            "Unklare Zugriffsverweigerung bleibt Fehler",
            TestAccessDenied,
            results);

        Execute(
            "Unbekannte E/A-Fehler bleiben Fehler",
            TestUnknownIoFailure,
            results);

        Execute(
            "Unerwartete Fehler bleiben Fehler",
            TestUnexpectedFailure,
            results);

        Execute(
            "Bekannter Schreibschutz bleibt erwarteter Schutzfall",
            TestKnownReadOnlyAttribute,
            results);

        Execute(
            "Abbruch darf nicht als Dateifehler gelten",
            TestCancellation,
            results);

        return
            $"Alle {results.Count} Regressionstests "
            + "für die Dateiproblem-Klassifizierung erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =============================================
    // 1. NORMALE DATEIATTRIBUTE
    // =============================================

    private static void TestNormalAttributes()
    {
        var result =
            CleanupEntryIssueClassifier
                .ClassifyProtectedFileAttributes(
                    FileAttributes.Normal);

        Require(
            result is null,
            "Normale Dateien dürfen nicht "
            + "automatisch übersprungen werden.");
    }

    // =============================================
    // 2. SCHREIBSCHUTZ UND SYSTEMSCHUTZ
    // =============================================

    private static void TestProtectedAttributes()
    {
        var readOnly =
            CleanupEntryIssueClassifier
                .ClassifyProtectedFileAttributes(
                    FileAttributes.ReadOnly);

        RequireIssue(
            readOnly,
            CleanupEntryIssueKind.ReadOnly,
            CleanupEntryDisposition.ExpectedSkip);

        var system =
            CleanupEntryIssueClassifier
                .ClassifyProtectedFileAttributes(
                    FileAttributes.System);

        RequireIssue(
            system,
            CleanupEntryIssueKind.SystemProtected,
            CleanupEntryDisposition.ExpectedSkip);
    }

    // =============================================
    // 3. REPARSE POINTS
    // =============================================

    private static void TestReparsePoint()
    {
        var result =
            CleanupEntryIssueClassifier
                .ClassifyProtectedFileAttributes(
                    FileAttributes.ReparsePoint
                    | FileAttributes.ReadOnly);

        RequireIssue(
            result,
            CleanupEntryIssueKind.ReparsePoint,
            CleanupEntryDisposition.ExpectedSkip);
    }

    // =============================================
    // 4. BEREITS ENTFERNTE DATEIEN
    // =============================================

    private static void TestAlreadyMissing()
    {
        var missingFile =
            CleanupEntryIssueClassifier
                .ClassifyException(
                    new FileNotFoundException(
                        "Simulierter Testfall."));

        RequireIssue(
            missingFile,
            CleanupEntryIssueKind.AlreadyMissing,
            CleanupEntryDisposition.ExpectedSkip);

        var missingDirectory =
            CleanupEntryIssueClassifier
                .ClassifyException(
                    new DirectoryNotFoundException(
                        "Simulierter Testfall."));

        RequireIssue(
            missingDirectory,
            CleanupEntryIssueKind.AlreadyMissing,
            CleanupEntryDisposition.ExpectedSkip);
    }

    // =============================================
    // 5. PROZESS- UND DATEISPERREN
    // =============================================

    private static void TestLockViolations()
    {
        var sharingViolation =
            CleanupEntryIssueClassifier
                .ClassifyException(
                    new IOException(
                        "Simulierte Dateisperre.",
                        unchecked((int)0x80070020)));

        RequireIssue(
            sharingViolation,
            CleanupEntryIssueKind.LockedByProcess,
            CleanupEntryDisposition.ExpectedSkip);

        var lockViolation =
            CleanupEntryIssueClassifier
                .ClassifyException(
                    new IOException(
                        "Simulierter Sperrkonflikt.",
                        unchecked((int)0x80070021)));

        RequireIssue(
            lockViolation,
            CleanupEntryIssueKind.LockedByProcess,
            CleanupEntryDisposition.ExpectedSkip);
    }

    // =============================================
    // 6. NICHT LEERE ORDNER
    // =============================================

    private static void TestDirectoryNotEmpty()
    {
        var result =
            CleanupEntryIssueClassifier
                .ClassifyException(
                    new IOException(
                        "Simulierter nicht leerer Ordner.",
                        unchecked((int)0x80070091)));

        RequireIssue(
            result,
            CleanupEntryIssueKind.DirectoryNotEmpty,
            CleanupEntryDisposition.ExpectedSkip);
    }

    // =============================================
    // 7. UNKLARE ZUGRIFFSVERWEIGERUNG
    // =============================================

    private static void TestAccessDenied()
    {
        var result =
            CleanupEntryIssueClassifier
                .ClassifyException(
                    new UnauthorizedAccessException(
                        "Simulierter Zugriffsfehler."));

        RequireIssue(
            result,
            CleanupEntryIssueKind.AccessDenied,
            CleanupEntryDisposition.TechnicalFailure);
    }

    // =============================================
    // 8. UNBEKANNTER E/A-FEHLER
    // =============================================

    private static void TestUnknownIoFailure()
    {
        var result =
            CleanupEntryIssueClassifier
                .ClassifyException(
                    new IOException(
                        "Simulierter unbekannter E/A-Fehler.",
                        unchecked((int)0x80070017)));

        RequireIssue(
            result,
            CleanupEntryIssueKind.OtherIoFailure,
            CleanupEntryDisposition.TechnicalFailure);
    }

    // =============================================
    // 9. UNERWARTETER FEHLER
    // =============================================

    private static void TestUnexpectedFailure()
    {
        var result =
            CleanupEntryIssueClassifier
                .ClassifyException(
                    new InvalidOperationException(
                        "Simulierter unerwarteter Fehler."));

        RequireIssue(
            result,
            CleanupEntryIssueKind.UnexpectedFailure,
            CleanupEntryDisposition.TechnicalFailure);
    }

    // =============================================
    // 10. BEREITS BEKANNTER SCHREIBSCHUTZ
    // =============================================

    private static void TestKnownReadOnlyAttribute()
    {
        var result =
            CleanupEntryIssueClassifier
                .ClassifyException(
                    new UnauthorizedAccessException(
                        "Simulierter Zugriffsfehler."),
                    FileAttributes.ReadOnly);

        RequireIssue(
            result,
            CleanupEntryIssueKind.ReadOnly,
            CleanupEntryDisposition.ExpectedSkip);
    }

    // =============================================
    // 11. ABBRUCH BLEIBT ABBRUCH
    // =============================================

    private static void TestCancellation()
    {
        var rejected = false;

        try
        {
            CleanupEntryIssueClassifier
                .ClassifyException(
                    new OperationCanceledException(
                        "Simulierter Abbruch."));
        }
        catch (ArgumentException)
        {
            rejected = true;
        }

        Require(
            rejected,
            "Ein Abbruch darf nicht als "
            + "Dateiproblem klassifiziert werden.");
    }

    // =============================================
    // GEMEINSAME PRÜFUNGEN
    // =============================================

    private static void RequireIssue(
        CleanupEntryIssue? issue,
        CleanupEntryIssueKind expectedKind,
        CleanupEntryDisposition expectedDisposition)
    {
        Require(
            issue is not null,
            "Es wurde kein Klassifizierungsergebnis "
            + "zurückgegeben.");

        Require(
            issue.Kind == expectedKind,
            "Falsche Ursache: erwartet "
            + expectedKind
            + ", erhalten "
            + issue.Kind
            + ".");

        Require(
            issue.Disposition == expectedDisposition,
            "Falsche Behandlung für "
            + issue.Kind
            + ": erwartet "
            + expectedDisposition
            + ", erhalten "
            + issue.Disposition
            + ".");
    }

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
}

#endif

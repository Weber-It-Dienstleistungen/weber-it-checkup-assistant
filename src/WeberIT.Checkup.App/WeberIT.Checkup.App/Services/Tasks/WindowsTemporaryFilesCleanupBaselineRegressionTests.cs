
#if DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Principal;
using WeberIT.Checkup.App.Models;

using EngineResult =
    WeberIT.Checkup.App.Services.Tasks
        .WindowsTemporaryFilesHandleCleanupEngine.ExecutionResult;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.28a
///
/// Zwölf Basistests für die neue direkte
/// C#-Ergebnisübertragung.
///
/// Ersetzt die zwölf veralteten Tests
/// der PowerShell-Ausführungsarchitektur.
///
/// Sicherheitsregeln:
/// - Keine Administratorrechte.
/// - Keine produktive Bereinigung.
/// - Keine Löschoperationen.
/// - Keine externen Prozesse.
/// - Kein Dateisystemzugriff.
/// - Ausschließlich künstliche Ergebnisse.
///
/// Aufgerufen wird ausschließlich die
/// private, reine Konvertierungsfunktion
/// des Handle-Adapters.
///
/// Die produktive Execute-Methode des
/// Adapters wird niemals aufgerufen.
/// </summary>
public static class
    WindowsTemporaryFilesCleanupBaselineRegressionTests
{
    private const string TestCategoryTitle =
        "Windows-Temp";

    /*
     * Absichtlich kein echter Dateipfad.
     *
     * Dieser Wert wird nur innerhalb
     * künstlicher Ergebnisobjekte verwendet.
     */
    private const string TestTarget =
        "TEST_ONLY_WINDOWS_TEMP_TARGET";

    private static readonly MethodInfo
        ResultConversionMethod =
            typeof(WindowsTemporaryFilesHandleCleanupAdapter)
                .GetMethod(
                    "CreateCategoryResult",
                    BindingFlags.NonPublic
                    | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "Die Ergebnisübertragung des "
                + "Handle-Adapters wurde nicht gefunden.");

    // =============================================
    // TESTS AUSFÜHREN
    // =============================================

    public static string RunAll()
    {
        EnsureNonElevated();

        var results = new List<string>();

        Run(
            "Erfolg und Ergebniszähler vollständig übertragen",
            TestSuccessfulResult,
            results);

        Run(
            "Überspringgründe unverändert und getrennt übertragen",
            TestSkippedReasons,
            results);

        Run(
            "Teilweise aufgeschlüsselte Ausschlüsse bleiben gültig",
            TestPartialSkipBreakdown,
            results);

        Run(
            "Teilweise erfolgreiche Bereinigung korrekt bewertet",
            TestPartialTechnicalFailure,
            results);

        Run(
            "Abbruch nach Ausführungsbeginn bleibt dokumentiert",
            TestCancellationResult,
            results);

        Run(
            "Nicht gestarteter Lauf erhält eindeutigen Fehler",
            TestMissingStartStatus,
            results);

        Run(
            "Löschungen ohne Ausführungsbeginn zurückgewiesen",
            TestChangesWithoutStart,
            results);

        Run(
            "Negative Ergebniszähler zurückgewiesen",
            TestNegativeCounters,
            results);

        Run(
            "Unbekannte Überspringgründe zurückgewiesen",
            TestUnknownSkipReason,
            results);

        Run(
            "Ungültige Ursachenzähler zurückgewiesen",
            TestInvalidReasonCounts,
            results);

        Run(
            "Doppelte Überspringgründe zurückgewiesen",
            TestDuplicateReasons,
            results);

        Run(
            "Widersprüchliche Ursachensummen zurückgewiesen",
            TestInvalidReasonTotals,
            results);

        return
            $"Alle {results.Count} C#-"
            + "Ergebnisbasistests erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =============================================
    // TESTUMGEBUNG
    // =============================================

    private static void EnsureNonElevated()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Die Windows-Temp-Basistests "
                + "benötigen Windows.");
        }

        using var identity =
            WindowsIdentity.GetCurrent();

        var principal =
            new WindowsPrincipal(identity);

        if (principal.IsInRole(
                WindowsBuiltInRole.Administrator))
        {
            throw new InvalidOperationException(
                "SICHERHEITSSTOPP: Diese Tests "
                + "dürfen nicht mit "
                + "Administratorrechten laufen.");
        }
    }

    // =============================================
    // TEST 1: ERFOLGREICHES ERGEBNIS
    // =============================================

    private static void TestSuccessfulResult()
    {
        var result = ConvertResult(
            CreateEngineResult(
                files: 3,
                directories: 1,
                bytes: 4096));

        Require(
            result.Category ==
                CleanupCategoryType.WindowsTemporaryFiles,
            "Die Bereinigungskategorie ist falsch.");

        Require(
            result.CategoryTitle == TestCategoryTitle
            && result.TargetPath == TestTarget,
            "Die Ergebnisidentität wurde verändert.");

        Require(
            result.WasStarted
            && !result.WasCancelled
            && result.IsSuccessful,
            "Der erfolgreiche Lauf wurde "
            + "falsch bewertet.");

        Require(
            result.DeletedFileCount == 3
            && result.DeletedDirectoryCount == 1
            && result.DeletedSizeBytes == 4096,
            "Die Löschzähler wurden verändert.");

        Require(
            result.FailedEntryCount == 0
            && result.SkippedEntryCount == 0
            && result.SkippedEntryReasons.Count == 0,
            "Der erfolgreiche Lauf enthält "
            + "unerwartete Fehler oder Ausschlüsse.");
    }

    // =============================================
    // TEST 2: AUSSCHLUSSGRÜNDE
    // =============================================

    private static void TestSkippedReasons()
    {
        var sourceReasons =
            new List<CleanupSkippedEntryReasonCount>
            {
                Reason(
                    CleanupSkippedEntryReason.ReadOnly,
                    2),

                Reason(
                    CleanupSkippedEntryReason.LockedByProcess,
                    1)
            };

        var result = ConvertResult(
            CreateEngineResult(
                skipped: 3,
                reasons: sourceReasons));

        Require(
            result.SkippedEntryCount == 3
            && result.SkippedEntryReasons.Count == 2,
            "Die Ausschlusszähler "
            + "wurden nicht vollständig übertragen.");

        Require(
            result.SkippedEntryReasons.Any(
                entry =>
                    entry.Reason ==
                        CleanupSkippedEntryReason.ReadOnly
                    && entry.Count == 2),
            "Schreibschutz wurde nicht "
            + "korrekt übertragen.");

        Require(
            result.SkippedEntryReasons.Any(
                entry =>
                    entry.Reason ==
                        CleanupSkippedEntryReason.LockedByProcess
                    && entry.Count == 1),
            "Prozesssperren wurden nicht "
            + "korrekt übertragen.");

        Require(
            !ReferenceEquals(
                sourceReasons,
                result.SkippedEntryReasons),
            "Die Ergebnisliste wurde nicht "
            + "eigenständig erzeugt.");

        Require(
            result.IsSuccessful,
            "Kontrollierte Ausschlüsse wurden "
            + "fälschlich als Fehler bewertet.");
    }

    // =============================================
    // TEST 3: TEILWEISE AUFSCHLÜSSELUNG
    // =============================================

    private static void TestPartialSkipBreakdown()
    {
        var result = ConvertResult(
            CreateEngineResult(
                skipped: 5,
                reasons:
                [
                    Reason(
                        CleanupSkippedEntryReason.ReadOnly,
                        2)
                ]));

        Require(
            result.SkippedEntryCount == 5,
            "Die Gesamtzahl wurde verändert.");

        Require(
            result.SkippedEntryReasons.Count == 1
            && result.SkippedEntryReasons[0].Count == 2,
            "Die vorhandene Aufschlüsselung "
            + "ging verloren.");

        Require(
            result.IsSuccessful,
            "Eine unvollständige "
            + "Ursachenaufschlüsselung wurde "
            + "als technischer Fehler bewertet.");
    }

    // =============================================
    // TEST 4: TECHNISCHER TEILERFOLG
    // =============================================

    private static void TestPartialTechnicalFailure()
    {
        const string expectedError =
            "Ein Eintrag konnte technisch "
            + "nicht verarbeitet werden.";

        var result = ConvertResult(
            CreateEngineResult(
                files: 1,
                failures: 1,
                error: expectedError));

        Require(
            result.WasStarted
            && result.DeletedFileCount == 1
            && result.FailedEntryCount == 1,
            "Die Teilverarbeitung wurde "
            + "nicht vollständig dokumentiert.");

        Require(
            result.ErrorMessage == expectedError,
            "Die technische Fehlermeldung "
            + "wurde verändert.");

        Require(
            !result.IsSuccessful
            && result.IsPartiallySuccessful,
            "Der technische Teilerfolg "
            + "wurde falsch bewertet.");
    }

    // =============================================
    // TEST 5: ABBRUCH NACH BEGINN
    // =============================================

    private static void TestCancellationResult()
    {
        var result = ConvertResult(
            CreateEngineResult(
                cancelled: true,
                files: 1,
                bytes: 128));

        Require(
            result.WasStarted
            && result.WasCancelled,
            "Der Abbruchstatus ging verloren.");

        Require(
            result.DeletedFileCount == 1
            && result.DeletedSizeBytes == 128,
            "Bereits erfolgte Änderungen "
            + "wurden beim Abbruch verworfen.");

        Require(
            !result.IsSuccessful,
            "Ein abgebrochener Lauf "
            + "wurde als Erfolg bewertet.");
    }

    // =============================================
    // TEST 6: NICHT GESTARTETER LAUF
    // =============================================

    private static void TestMissingStartStatus()
    {
        var result = ConvertResult(
            CreateEngineResult(
                started: false));

        Require(
            !result.WasStarted
            && !result.WasCancelled,
            "Der Ausführungsstatus wurde verändert.");

        Require(
            !string.IsNullOrWhiteSpace(
                result.ErrorMessage),
            "Ein nicht gestarteter Lauf "
            + "besitzt keine Fehlermeldung.");

        Require(
            !result.IsSuccessful,
            "Ein nicht gestarteter Lauf "
            + "wurde als Erfolg bewertet.");
    }

    // =============================================
    // TEST 7: ÄNDERUNGEN OHNE START
    // =============================================

    private static void TestChangesWithoutStart()
    {
        ExpectException<InvalidOperationException>(
            () => ConvertResult(
                CreateEngineResult(
                    started: false,
                    files: 1)));

        ExpectException<InvalidOperationException>(
            () => ConvertResult(
                CreateEngineResult(
                    started: false,
                    directories: 1)));

        ExpectException<InvalidOperationException>(
            () => ConvertResult(
                CreateEngineResult(
                    started: false,
                    bytes: 1)));
    }

    // =============================================
    // TEST 8: NEGATIVE ZÄHLER
    // =============================================

    private static void TestNegativeCounters()
    {
        ExpectException<InvalidOperationException>(
            () => ConvertResult(
                CreateEngineResult(
                    files: -1)));

        ExpectException<InvalidOperationException>(
            () => ConvertResult(
                CreateEngineResult(
                    directories: -1)));

        ExpectException<InvalidOperationException>(
            () => ConvertResult(
                CreateEngineResult(
                    failures: -1)));

        ExpectException<InvalidOperationException>(
            () => ConvertResult(
                CreateEngineResult(
                    skipped: -1)));
    }

    // =============================================
    // TEST 9: UNBEKANNTER URSACHENCODE
    // =============================================

    private static void TestUnknownSkipReason()
    {
        ExpectException<InvalidOperationException>(
            () => ConvertResult(
                CreateEngineResult(
                    skipped: 1,
                    reasons:
                    [
                        Reason(
                            (CleanupSkippedEntryReason)999,
                            1)
                    ])));
    }

    // =============================================
    // TEST 10: UNGÜLTIGE URSACHENZÄHLER
    // =============================================

    private static void TestInvalidReasonCounts()
    {
        ExpectException<InvalidOperationException>(
            () => ConvertResult(
                CreateEngineResult(
                    skipped: 1,
                    reasons:
                    [
                        Reason(
                            CleanupSkippedEntryReason.ReadOnly,
                            0)
                    ])));

        ExpectException<InvalidOperationException>(
            () => ConvertResult(
                CreateEngineResult(
                    skipped: 1,
                    reasons:
                    [
                        Reason(
                            CleanupSkippedEntryReason.ReadOnly,
                            -1)
                    ])));
    }

    // =============================================
    // TEST 11: DOPPELTE URSACHEN
    // =============================================

    private static void TestDuplicateReasons()
    {
        ExpectException<InvalidOperationException>(
            () => ConvertResult(
                CreateEngineResult(
                    skipped: 2,
                    reasons:
                    [
                        Reason(
                            CleanupSkippedEntryReason.ReadOnly,
                            1),

                        Reason(
                            CleanupSkippedEntryReason.ReadOnly,
                            1)
                    ])));
    }

    // =============================================
    // TEST 12: UNGÜLTIGE GESAMTSUMMEN
    // =============================================

    private static void TestInvalidReasonTotals()
    {
        /*
         * Die Summe dokumentierter Ursachen
         * darf den Gesamtzähler nicht
         * überschreiten.
         */
        ExpectException<InvalidOperationException>(
            () => ConvertResult(
                CreateEngineResult(
                    skipped: 1,
                    reasons:
                    [
                        Reason(
                            CleanupSkippedEntryReason.ReadOnly,
                            1),

                        Reason(
                            CleanupSkippedEntryReason.LockedByProcess,
                            1)
                    ])));

        /*
         * Auch ein Überlauf bei extremen
         * Ergebniswerten muss kontrolliert
         * zurückgewiesen werden.
         */
        ExpectException<OverflowException>(
            () => ConvertResult(
                CreateEngineResult(
                    skipped: long.MaxValue,
                    reasons:
                    [
                        Reason(
                            CleanupSkippedEntryReason.ReadOnly,
                            long.MaxValue),

                        Reason(
                            CleanupSkippedEntryReason.LockedByProcess,
                            1)
                    ])));
    }

    // =============================================
    // KÜNSTLICHES ENGINE-ERGEBNIS
    // =============================================

    private static EngineResult CreateEngineResult(
        bool started = true,
        bool cancelled = false,
        long files = 0,
        long directories = 0,
        ulong bytes = 0,
        long failures = 0,
        long skipped = 0,
        IReadOnlyList<CleanupSkippedEntryReasonCount>?
            reasons = null,
        string error = "")
    {
        return new EngineResult
        {
            WasStarted = started,
            WasCancelled = cancelled,

            DeletedFileCount = files,
            DeletedDirectoryCount = directories,
            DeletedSizeBytes = bytes,

            FailedEntryCount = failures,
            SkippedEntryCount = skipped,

            SkippedEntryReasons =
                reasons
                ?? Array.Empty<
                    CleanupSkippedEntryReasonCount>(),

            ErrorMessage = error
        };
    }

    private static CleanupSkippedEntryReasonCount Reason(
        CleanupSkippedEntryReason reason,
        long count)
    {
        return new CleanupSkippedEntryReasonCount
        {
            Reason = reason,
            Count = count
        };
    }

    // =============================================
    // NUR ERGEBNISKONVERTIERUNG AUFRUFEN
    // =============================================

    private static CleanupActionCategoryExecutionResult
        ConvertResult(
            EngineResult engineResult)
    {
        /*
         * Ausschließlich die private
         * Konvertierungsfunktion aufrufen.
         *
         * Niemals:
         * WindowsTemporaryFilesHandleCleanupAdapter
         *     .Execute(...)
         *
         * Keine echte Engine-Ausführung.
         */
        try
        {
            var converted =
                ResultConversionMethod.Invoke(
                    null,
                    [
                        TestCategoryTitle,
                        TestTarget,
                        engineResult
                    ]);

            return converted
                as CleanupActionCategoryExecutionResult
                ?? throw new InvalidOperationException(
                    "Die Ergebniskonvertierung "
                    + "lieferte kein gültiges "
                    + "Kategorieergebnis.");
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException is not null)
        {
            /*
             * Reflection verpackt Exceptions.
             *
             * Für die Tests wird die echte
             * fachliche Exception benötigt.
             */
            ExceptionDispatchInfo.Capture(
                exception.InnerException!).Throw();

            throw;
        }
    }

    // =============================================
    // ERWARTETE AUSNAHME PRÜFEN
    // =============================================

    private static void ExpectException<TException>(
        Action action)
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
            "Eine erwartete Ausnahme vom Typ "
            + typeof(TException).Name
            + " wurde nicht ausgelöst.");
    }

    // =============================================
    // TEST-HILFSMETHODEN
    // =============================================

    private static void Run(
        string description,
        Action action,
        List<string> results)
    {
        action();

        results.Add(
            "[OK] " + description);
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

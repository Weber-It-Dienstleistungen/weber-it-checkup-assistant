
#if DEBUG

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Regressionstests für die Einordnung von
/// Dateisystemproblemen in beiden
/// Bereinigungs-Executoren.
///
/// Ausschließlich künstliche Testdaten.
/// Keine Dateien werden geöffnet oder gelöscht.
///
/// Der Abbruchfall wird bereits in den
/// separaten Klassifizierungs- und
/// Ergebnistests geprüft.
///
/// Hier werden keine absichtlichen
/// Exceptions über Reflection geworfen.
/// </summary>
public static class CleanupExecutorClassificationRegressionTests
{
    private sealed record ExecutorDefinition(
        string Name,
        Type ExecutorType,
        string MetricsTypeName,
        bool IsBrowser);

    private static readonly ExecutorDefinition[] Executors =
    [
        new(
            "Standardbereinigung",
            typeof(CleanupActionExecutor),
            "DeletionMetrics",
            false),

        new(
            "Browsercache",
            typeof(BrowserCacheCleanupExecutor),
            "BrowserDeletionMetrics",
            true)
    ];

    public static string RunAll()
    {
        var results = new List<string>();

        var scenarios =
            new (string Name, Action<ExecutorDefinition> Test)[]
            {
                (
                    "Normale Attribute bleiben freigegeben",
                    TestNormalAttributes
                ),
                (
                    "Reparse Points werden übersprungen",
                    e => TestProtected(
                        e,
                        FileAttributes.ReparsePoint
                        | FileAttributes.ReadOnly,
                        CleanupSkippedEntryReason.ReparsePoint)
                ),
                (
                    "Schreibschutz wird dokumentiert",
                    e => TestProtected(
                        e,
                        FileAttributes.ReadOnly,
                        CleanupSkippedEntryReason.ReadOnly)
                ),
                (
                    "Systemschutz wird dokumentiert",
                    e => TestProtected(
                        e,
                        FileAttributes.System,
                        CleanupSkippedEntryReason.SystemProtected)
                ),
                (
                    "Prozesssperre wird übersprungen",
                    e => TestIssue(
                        e,
                        CreateIOException(0x80070020),
                        skipped: 1,
                        failed: 0,
                        reason:
                            CleanupSkippedEntryReason.LockedByProcess)
                ),
                (
                    "Dateisperre wird übersprungen",
                    e => TestIssue(
                        e,
                        CreateIOException(0x80070021),
                        skipped: 1,
                        failed: 0,
                        reason:
                            CleanupSkippedEntryReason.LockedByProcess)
                ),
                (
                    "Nicht leerer Ordner wird übersprungen",
                    e => TestIssue(
                        e,
                        CreateIOException(0x80070091),
                        skipped: 1,
                        failed: 0,
                        reason:
                            CleanupSkippedEntryReason.DirectoryNotEmpty)
                ),
                (
                    "Verschwundene Datei wird übersprungen",
                    e => TestIssue(
                        e,
                        new FileNotFoundException(
                            "Simulierter Testfall."),
                        skipped: 1,
                        failed: 0,
                        reason:
                            CleanupSkippedEntryReason.AlreadyMissing)
                ),
                (
                    "Bekannter Schreibschutz bei Zugriffsfehler",
                    e => TestIssue(
                        e,
                        new UnauthorizedAccessException(
                            "Simulierter Zugriffsschutz."),
                        skipped: 1,
                        failed: 0,
                        reason:
                            CleanupSkippedEntryReason.ReadOnly,
                        attributes:
                            FileAttributes.ReadOnly)
                ),
                (
                    "Unklarer Zugriffsfehler bleibt Fehler",
                    e => TestIssue(
                        e,
                        new UnauthorizedAccessException(
                            "Simulierter ungeklärter Zugriff."),
                        skipped: 0,
                        failed: 1)
                ),
                (
                    "Unbekannter E/A-Fehler bleibt Fehler",
                    e => TestIssue(
                        e,
                        CreateIOException(0x80070017),
                        skipped: 0,
                        failed: 1)
                ),
                (
                    "Unterschiedliche Ursachen bleiben getrennt",
                    TestCombinedReasons
                ),
                (
                    "Unerwarteter Fehler bleibt technischer Fehler",
                    e => TestIssue(
                        e,
                        new InvalidOperationException(
                            "Simulierter unerwarteter Fehler."),
                        skipped: 0,
                        failed: 1)
                )
            };

        foreach (var executor in Executors)
        {
            foreach (var scenario in scenarios)
            {
                Execute(
                    executor,
                    scenario.Name,
                    scenario.Test,
                    results);
            }
        }

        return
            $"Alle {results.Count} Integrationstests "
            + "für die Bereinigungs-Klassifizierung erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =============================================
    // NORMALE DATEIATTRIBUTE
    // =============================================

    private static void TestNormalAttributes(
        ExecutorDefinition executor)
    {
        var metrics = CreateMetrics(executor);

        var skipped = InvokeProtectedAttributes(
            executor,
            metrics,
            FileAttributes.Normal);

        Require(
            !skipped,
            "Eine normale Datei wurde übersprungen.");

        RequireCounters(
            metrics,
            skipped: 0,
            failed: 0);
    }

    // =============================================
    // GESCHÜTZTE DATEIATTRIBUTE
    // =============================================

    private static void TestProtected(
        ExecutorDefinition executor,
        FileAttributes attributes,
        CleanupSkippedEntryReason expectedReason)
    {
        var metrics = CreateMetrics(executor);

        var skipped = InvokeProtectedAttributes(
            executor,
            metrics,
            attributes);

        Require(
            skipped,
            "Die geschützten Attribute "
            + "wurden nicht erkannt.");

        RequireCounters(
            metrics,
            skipped: 1,
            failed: 0);

        RequireReason(
            metrics,
            expectedReason,
            expectedCount: 1);
    }

    // =============================================
    // SIMULIERTE DATEISYSTEMPROBLEME
    // =============================================

    private static void TestIssue(
        ExecutorDefinition executor,
        Exception exception,
        long skipped,
        long failed,
        CleanupSkippedEntryReason? reason = null,
        FileAttributes? attributes = null)
    {
        var metrics = CreateMetrics(executor);

        /*
         * Die Exception wird nur als
         * Testobjekt übergeben.
         *
         * Es wird hier kein künstlicher
         * Abbruch ausgelöst.
         */
        InvokeRecordIssue(
            executor,
            metrics,
            exception,
            attributes);

        RequireCounters(
            metrics,
            skipped,
            failed);

        if (reason.HasValue)
        {
            RequireReason(
                metrics,
                reason.Value,
                expectedCount: skipped);
        }

        var reasons = ReadReasons(metrics);

        Require(
            reasons.Values.Sum() == skipped,
            "Die Summe der Ursachenzähler "
            + "stimmt nicht mit den übersprungenen "
            + "Einträgen überein.");
    }

    // =============================================
    // GEMISCHTE AUSNAHMEN
    // =============================================

    private static void TestCombinedReasons(
        ExecutorDefinition executor)
    {
        var metrics = CreateMetrics(executor);

        InvokeProtectedAttributes(
            executor,
            metrics,
            FileAttributes.ReadOnly);

        InvokeRecordIssue(
            executor,
            metrics,
            CreateIOException(0x80070020));

        InvokeRecordIssue(
            executor,
            metrics,
            new FileNotFoundException(
                "Simulierter Testfall."));

        RequireCounters(
            metrics,
            skipped: 3,
            failed: 0);

        RequireReason(
            metrics,
            CleanupSkippedEntryReason.ReadOnly,
            expectedCount: 1);

        RequireReason(
            metrics,
            CleanupSkippedEntryReason.LockedByProcess,
            expectedCount: 1);

        RequireReason(
            metrics,
            CleanupSkippedEntryReason.AlreadyMissing,
            expectedCount: 1);

        var reasons = ReadReasons(metrics);

        Require(
            reasons.Count == 3,
            "Die drei verschiedenen Ursachen "
            + "wurden nicht getrennt gespeichert.");

        Require(
            reasons.Values.Sum() == 3,
            "Die Ursachenzähler stimmen nicht "
            + "mit dem Gesamtzähler überein.");
    }

    // =============================================
    // REFLECTION-HILFSMETHODEN
    // =============================================

    private static object CreateMetrics(
        ExecutorDefinition executor)
    {
        var metricsType =
            executor.ExecutorType.GetNestedType(
                executor.MetricsTypeName,
                BindingFlags.NonPublic);

        Require(
            metricsType is not null,
            "Interner Metriktyp nicht gefunden: "
            + executor.MetricsTypeName);

        return Activator.CreateInstance(
                   metricsType!,
                   nonPublic: true)
               ?? throw new InvalidOperationException(
                   "Interne Metriken konnten "
                   + "nicht erstellt werden.");
    }

    private static bool InvokeProtectedAttributes(
        ExecutorDefinition executor,
        object metrics,
        FileAttributes attributes)
    {
        var result = InvokePrivate(
            executor.ExecutorType,
            "SkipProtectedEntry",
            attributes,
            metrics);

        Require(
            result is bool,
            "Die Attributprüfung lieferte "
            + "kein boolesches Ergebnis.");

        return (bool)result!;
    }

    private static void InvokeRecordIssue(
        ExecutorDefinition executor,
        object metrics,
        Exception exception,
        FileAttributes? attributes = null)
    {
        if (executor.IsBrowser)
        {
            InvokePrivate(
                executor.ExecutorType,
                "RecordIssue",
                metrics,
                exception,
                attributes);

            return;
        }

        InvokePrivate(
            executor.ExecutorType,
            "RecordIssue",
            metrics,
            @"C:\SimulierteTestdaten\Datei.tmp",
            "Datei löschen",
            exception,
            attributes);
    }

    private static object? InvokePrivate(
        Type type,
        string methodName,
        params object?[] arguments)
    {
        var method = type.GetMethod(
            methodName,
            BindingFlags.NonPublic
            | BindingFlags.Static);

        Require(
            method is not null,
            "Interne Methode nicht gefunden: "
            + type.Name
            + "."
            + methodName);

        try
        {
            return method!.Invoke(
                null,
                arguments);
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException is not null)
        {
            /*
             * Unerwartete Testfehler
             * bleiben sichtbar.
             *
             * Der bewusst erzeugte
             * OperationCanceledException-
             * Test wurde entfernt.
             */
            ExceptionDispatchInfo
                .Capture(exception.InnerException)
                .Throw();

            throw;
        }
    }

    private static IOException CreateIOException(
        uint hResult)
    {
        return new IOException(
            "Simulierter Dateisystemfehler.",
            unchecked((int)hResult));
    }

    // =============================================
    // METRIKEN LESEN
    // =============================================

    private static long ReadCounter(
        object metrics,
        string propertyName)
    {
        var property =
            metrics.GetType().GetProperty(
                propertyName,
                BindingFlags.Public
                | BindingFlags.Instance);

        Require(
            property is not null,
            "Metrikzähler nicht gefunden: "
            + propertyName);

        var value = property!.GetValue(metrics);

        Require(
            value is long,
            "Metrikzähler besitzt "
            + "einen unerwarteten Typ.");

        return (long)value!;
    }

    private static Dictionary<
        CleanupSkippedEntryReason, long> ReadReasons(
            object metrics)
    {
        var property =
            metrics.GetType().GetProperty(
                "SkippedEntryReasons",
                BindingFlags.Public
                | BindingFlags.Instance);

        Require(
            property is not null,
            "Die Ursachenzähler fehlen.");

        var value = property!.GetValue(metrics);

        return value as Dictionary<
            CleanupSkippedEntryReason, long>
            ?? throw new InvalidOperationException(
                "Die Ursachenzähler besitzen "
                + "einen unerwarteten Typ.");
    }

    // =============================================
    // TESTBEWERTUNG
    // =============================================

    private static void RequireCounters(
        object metrics,
        long skipped,
        long failed)
    {
        var actualSkipped = ReadCounter(
            metrics,
            "SkippedEntryCount");

        var actualFailed = ReadCounter(
            metrics,
            "FailedEntryCount");

        Require(
            actualSkipped == skipped,
            "Falsche Anzahl übersprungener Einträge: "
            + $"erwartet {skipped}, "
            + $"erhalten {actualSkipped}.");

        Require(
            actualFailed == failed,
            "Falsche Anzahl technischer Fehler: "
            + $"erwartet {failed}, "
            + $"erhalten {actualFailed}.");
    }

    private static void RequireReason(
        object metrics,
        CleanupSkippedEntryReason reason,
        long expectedCount)
    {
        var reasons = ReadReasons(metrics);

        Require(
            reasons.TryGetValue(
                reason,
                out var actualCount),
            "Überspringursache fehlt: "
            + reason);

        Require(
            actualCount == expectedCount,
            "Falscher Ursachenzähler für "
            + reason
            + $": erwartet {expectedCount}, "
            + $"erhalten {actualCount}.");
    }

    private static void Execute(
        ExecutorDefinition executor,
        string testName,
        Action<ExecutorDefinition> test,
        ICollection<string> results)
    {
        try
        {
            test(executor);

            results.Add(
                "OK: "
                + executor.Name
                + " – "
                + testName);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Integrationstest fehlgeschlagen: "
                + executor.Name
                + " – "
                + testName
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
}

#endif

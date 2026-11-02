
#if DEBUG

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.13.29
///
/// Integrationstests für die JSON-Ergebnisübernahme
/// der erhöhten Windows-Temp-Bereinigung.
///
/// Geprüfte Verarbeitungskette:
///
/// Worker-JSON
/// -> Deserialisierung
/// -> Worker-Validierung
/// -> Kategorieergebnis
/// -> Persistierung und Anzeigetext
///
/// Keine PowerShell-Ausführung.
/// Keine Administratorrechte.
/// Keine Dateisystemänderungen.
/// Keine Löschvorgänge.
/// </summary>
public static class
    WindowsTemporaryFilesCleanupResultIntegrationRegressionTests
{
    private static readonly Type ExecutorType =
        typeof(WindowsTemporaryFilesCleanupExecutor);

    public static string RunAll()
    {
        var results = new List<string>();

        Execute(
            "Einzelne Ursache wird vollständig übernommen",
            TestSingleReason,
            results);

        Execute(
            "Mehrere Ursachen bleiben getrennt",
            TestMultipleReasons,
            results);

        Execute(
            "Teilweise dokumentierte Ursachen bleiben sichtbar",
            TestPartialBreakdown,
            results);

        Execute(
            "Historisches JSON bleibt kompatibel",
            TestLegacyJson,
            results);

        Execute(
            "Null-Ursachenliste bleibt kompatibel",
            TestNullReasons,
            results);

        Execute(
            "Leere Ursachenliste bleibt gültig",
            TestEmptyReasons,
            results);

        Execute(
            "Ursachen überstehen JSON-Persistierung",
            TestPersistence,
            results);

        Execute(
            "Standardausgabe übernimmt Ursachendetails",
            TestStandardOutputFallback,
            results);

        Execute(
            "Unbekannte Ursachen werden zurückgewiesen",
            TestUnknownReason,
            results);

        Execute(
            "Numerische Ursachencodes werden zurückgewiesen",
            TestNumericReason,
            results);

        Execute(
            "Abweichende Schreibweisen werden zurückgewiesen",
            TestIncorrectCasing,
            results);

        Execute(
            "Doppelte Ursachencodes werden zurückgewiesen",
            TestDuplicateReasons,
            results);

        Execute(
            "Widersprüchliche Zähler werden zurückgewiesen",
            TestExceededTotal,
            results);

        Execute(
            "Fremder Zielpfad wird zurückgewiesen",
            TestUnexpectedTarget,
            results);

        Execute(
            "Negativer Gesamtzähler wird zurückgewiesen",
            TestNegativeTotal,
            results);

        Execute(
            "Technische Fehler bleiben technische Fehler",
            TestTechnicalFailure,
            results);

        return
            $"Alle {results.Count} Windows-Temp-"
            + "Ergebnisintegrationstests erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =============================================
    // ERFOLGREICHE ERGEBNISÜBERNAHME
    // =============================================

    private static void TestSingleReason()
    {
        var result = ConvertJson(
            CreateJson(
                skipped: 2,
                reasons:
                [
                    Reason("LockedByProcess", 2)
                ]));

        Require(
            result.SkippedEntryCount == 2,
            "Der Gesamtzähler wurde verändert.");

        Require(
            result.SkippedEntryReasons.Count == 1,
            "Die Ursache wurde nicht übernommen.");

        Require(
            result.SkippedEntryReasons[0].Reason
                == CleanupSkippedEntryReason.LockedByProcess,
            "Die Prozesssperre wurde falsch klassifiziert.");

        Require(
            result.SkippedEntryReasons[0].Count == 2,
            "Der Ursachenzähler ist falsch.");

        Require(
            result.HasSkippedEntryReasons,
            "Der Anzeigeindikator fehlt.");

        Require(
            result.SkippedEntryReasonsText.Contains(
                "Durch Prozess oder Dateisperre blockiert",
                StringComparison.Ordinal),
            "Die verständliche Ursachenbeschreibung fehlt.");

        Require(
            result.IsSuccessful,
            "Erwartete Sperren wurden fälschlich "
            + "als technischen Fehler gewertet.");
    }

    private static void TestMultipleReasons()
    {
        var result = ConvertJson(
            CreateJson(
                skipped: 6,
                reasons:
                [
                    Reason("ReadOnly", 2),
                    Reason("AlreadyMissing", 1),
                    Reason("LockedByProcess", 3)
                ]));

        Require(
            result.SkippedEntryReasons.Count == 3,
            "Nicht alle Ursachen wurden übernommen.");

        Require(
            result.SkippedEntryReasons.Sum(
                entry => entry.Count) == 6,
            "Die Summe der Ursachenzähler ist falsch.");

        Require(
            result.SkippedEntryReasons[0].Reason
                == CleanupSkippedEntryReason.AlreadyMissing,
            "Die Ursachen wurden nicht stabil sortiert.");

        Require(
            result.SkippedEntryReasonsText.Contains(
                "Schreibgeschützt",
                StringComparison.Ordinal),
            "Schreibschutz fehlt im Anzeigetext.");

        Require(
            result.SkippedEntryReasonsText.Contains(
                "Bereits nicht mehr vorhanden",
                StringComparison.Ordinal),
            "Bereits entfernte Einträge fehlen "
            + "im Anzeigetext.");
    }

    private static void TestPartialBreakdown()
    {
        var result = ConvertJson(
            CreateJson(
                skipped: 7,
                reasons:
                [
                    Reason("ReadOnly", 2)
                ]));

        Require(
            result.SkippedEntryCount == 7,
            "Der Gesamtzähler ging verloren.");

        Require(
            result.SkippedEntryReasons.Count == 1,
            "Es wurden zusätzliche Ursachen erfunden.");

        Require(
            result.SkippedEntryReasonsText.Contains(
                "Nicht näher aufgeschlüsselt",
                StringComparison.Ordinal),
            "Die unvollständige Dokumentation "
            + "wird nicht gekennzeichnet.");

        Require(
            result.IsSuccessful,
            "Eine teilweise Ursachenaufschlüsselung "
            + "wurde fälschlich zum technischen Fehler.");
    }

    private static void TestLegacyJson()
    {
        /*
         * Historische JSON-Ergebnisse enthalten
         * das Feld SkippedEntryReasons nicht.
         */
        var json = JsonSerializer.Serialize(
            new
            {
                WasStarted = true,
                WasCancelled = false,
                TargetPath = ExpectedTargetPath(),
                DeletedFileCount = 1L,
                DeletedDirectoryCount = 0L,
                DeletedSizeBytes = 512UL,
                FailedEntryCount = 0L,
                SkippedEntryCount = 3L,
                ErrorMessage = ""
            });

        var result = ConvertJson(json);

        Require(
            result.SkippedEntryCount == 3,
            "Der historische Gesamtzähler "
            + "wurde nicht übernommen.");

        Require(
            result.SkippedEntryReasons.Count == 0,
            "Für ein historisches Ergebnis "
            + "wurden Ursachen erfunden.");

        Require(
            !result.HasSkippedEntryReasons,
            "Der Anzeigeindikator ist falsch.");

        Require(
            result.IsSuccessful,
            "Ein historisches Ergebnis "
            + "wurde fälschlich abgelehnt.");
    }

    private static void TestNullReasons()
    {
        var result = ConvertJson(
            CreateJson(
                skipped: 4,
                reasons: null));

        Require(
            result.SkippedEntryCount == 4,
            "Der Gesamtzähler wurde verworfen.");

        Require(
            result.SkippedEntryReasons.Count == 0,
            "Eine Null-Ursachenliste "
            + "wurde falsch verarbeitet.");
    }

    private static void TestEmptyReasons()
    {
        var result = ConvertJson(
            CreateJson(
                skipped: 0,
                reasons: []));

        Require(
            result.SkippedEntryCount == 0,
            "Der Gesamtzähler ist falsch.");

        Require(
            result.SkippedEntryReasons.Count == 0,
            "Die leere Liste wurde verändert.");

        Require(
            !result.HasSkippedEntryReasons,
            "Der Anzeigeindikator ist falsch.");

        Require(
            result.IsSuccessful,
            "Das fehlerfreie Ergebnis "
            + "wurde nicht als Erfolg erkannt.");
    }

    private static void TestPersistence()
    {
        var original = ConvertJson(
            CreateJson(
                skipped: 3,
                reasons:
                [
                    Reason("SystemProtected", 1),
                    Reason("LockedByProcess", 2)
                ]));

        var serialized =
            JsonSerializer.Serialize(original);

        var restored =
            JsonSerializer.Deserialize<
                CleanupActionCategoryExecutionResult>(
                serialized);

        Require(
            restored is not null,
            "Das Ergebnis konnte nicht "
            + "wiederhergestellt werden.");

        Require(
            restored!.SkippedEntryCount == 3,
            "Der Gesamtzähler ging beim "
            + "Speichern verloren.");

        Require(
            restored.SkippedEntryReasons.Count == 2,
            "Ursachendetails gingen beim "
            + "Speichern verloren.");

        Require(
            restored.SkippedEntryReasons.Any(
                entry =>
                    entry.Reason
                        == CleanupSkippedEntryReason.SystemProtected
                    && entry.Count == 1),
            "Der Systemschutz wurde nicht "
            + "korrekt wiederhergestellt.");

        Require(
            restored.SkippedEntryReasonsText.Contains(
                "Systemgeschützt",
                StringComparison.Ordinal),
            "Der Anzeigetext funktioniert "
            + "nach dem Wiederherstellen nicht.");
    }

    private static void TestStandardOutputFallback()
    {
        var json = CreateJson(
            skipped: 2,
            reasons:
            [
                Reason("ReparsePoint", 2)
            ]);

        object?[] arguments =
        [
            "PowerShell-Startmeldung"
            + Environment.NewLine
            + json,
            null
        ];

        var success = GetMethod(
                "TryParseWorkerResultFromOutput")
            .Invoke(null, arguments);

        Require(
            success is true,
            "Das JSON wurde in der "
            + "Standardausgabe nicht erkannt.");

        Require(
            arguments[1] is not null,
            "Das Worker-Ergebnis fehlt.");

        var result = ConvertWorker(
            arguments[1]!);

        Require(
            result.SkippedEntryReasons.Count == 1,
            "Die Ursachenliste ging beim "
            + "Fallback verloren.");

        Require(
            result.SkippedEntryReasons[0].Reason
                == CleanupSkippedEntryReason.ReparsePoint,
            "Der Reparse Point wurde "
            + "falsch übernommen.");
    }

    // =============================================
    // FEHLERHAFTE DATEN ABLEHNEN
    // =============================================

    private static void TestUnknownReason()
    {
        ExpectRejected(
            () => ConvertJson(
                CreateJson(
                    skipped: 1,
                    reasons:
                    [
                        Reason("UnknownReason", 1)
                    ])));
    }

    private static void TestNumericReason()
    {
        ExpectRejected(
            () => ConvertJson(
                CreateJson(
                    skipped: 1,
                    reasons:
                    [
                        Reason("4", 1)
                    ])));
    }

    private static void TestIncorrectCasing()
    {
        ExpectRejected(
            () => ConvertJson(
                CreateJson(
                    skipped: 1,
                    reasons:
                    [
                        Reason("readonly", 1)
                    ])));
    }

    private static void TestDuplicateReasons()
    {
        ExpectRejected(
            () => ConvertJson(
                CreateJson(
                    skipped: 2,
                    reasons:
                    [
                        Reason("ReadOnly", 1),
                        Reason("ReadOnly", 1)
                    ])));
    }

    private static void TestExceededTotal()
    {
        ExpectRejected(
            () => ConvertJson(
                CreateJson(
                    skipped: 2,
                    reasons:
                    [
                        Reason("ReadOnly", 2),
                        Reason("LockedByProcess", 1)
                    ])));
    }

    private static void TestUnexpectedTarget()
    {
        ExpectRejected(
            () => ConvertJson(
                CreateJson(
                    skipped: 0,
                    reasons: [],
                    targetPath:
                        @"C:\NotAllowed\Temp")));
    }

    private static void TestNegativeTotal()
    {
        ExpectRejected(
            () => ConvertJson(
                CreateJson(
                    skipped: -1,
                    reasons: [])));
    }

    private static void TestTechnicalFailure()
    {
        var result = ConvertJson(
            CreateJson(
                skipped: 1,
                reasons:
                [
                    Reason("LockedByProcess", 1)
                ],
                failed: 2));

        Require(
            result.FailedEntryCount == 2,
            "Technische Fehler wurden "
            + "nicht übernommen.");

        Require(
            result.SkippedEntryCount == 1,
            "Erwartete Ausschlüsse wurden "
            + "fälschlich als Fehler gezählt.");

        Require(
            result.IsPartiallySuccessful,
            "Das Ergebnis mit technischen "
            + "Fehlern und erfolgten Änderungen "
            + "wurde falsch bewertet.");

        Require(
            !result.IsSuccessful,
            "Das Ergebnis wurde trotz "
            + "technischer Fehler als "
            + "vollständiger Erfolg bewertet.");
    }

    // =============================================
    // JSON-TESTDATEN
    // =============================================

    private static WindowsTemporaryFilesSkipReasonRecord
        Reason(
            string name,
            long count)
    {
        return new WindowsTemporaryFilesSkipReasonRecord
        {
            Reason = name,
            Count = count
        };
    }

    private static string CreateJson(
        long skipped,
        WindowsTemporaryFilesSkipReasonRecord?[]?
            reasons,
        string? targetPath = null,
        long failed = 0)
    {
        return JsonSerializer.Serialize(
            new
            {
                WasStarted = true,
                WasCancelled = false,
                TargetPath =
                    targetPath ?? ExpectedTargetPath(),
                DeletedFileCount = 2L,
                DeletedDirectoryCount = 1L,
                DeletedSizeBytes = 2048UL,
                FailedEntryCount = failed,
                SkippedEntryCount = skipped,
                SkippedEntryReasons = reasons,
                ErrorMessage = ""
            });
    }

    private static string ExpectedTargetPath()
    {
        var windowsDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

        if (string.IsNullOrWhiteSpace(windowsDirectory))
        {
            throw new InvalidOperationException(
                "Das Windows-Verzeichnis konnte "
                + "nicht bestimmt werden.");
        }

        return Path.GetFullPath(
            Path.Combine(
                windowsDirectory,
                "Temp"));
    }

    // =============================================
    // EXECUTOR-INTEGRATION ÜBER REFLECTION
    // =============================================

    private static CleanupActionCategoryExecutionResult
        ConvertJson(
            string json)
    {
        var worker = DeserializeWorker(json);

        return ConvertWorker(worker);
    }

    private static object DeserializeWorker(
        string json)
    {
        object?[] arguments =
        [
            json,
            null
        ];

        var success = GetMethod(
                "TryDeserializeWorkerResult")
            .Invoke(
                null,
                arguments);

        Require(
            success is true,
            "Das Worker-JSON konnte "
            + "nicht deserialisiert werden.");

        return arguments[1]
            ?? throw new InvalidOperationException(
                "Das Worker-Ergebnis fehlt.");
    }

    private static CleanupActionCategoryExecutionResult
        ConvertWorker(
            object worker)
    {
        /*
         * Originale Validierung des Executors.
         * Hier wird kein Bereinigungsplan
         * ausgeführt.
         */
        GetMethod(
                "ValidateWorkerResult")
            .Invoke(
                null,
                [worker]);

        var category =
            new CleanupActionCategory
            {
                Category =
                    CleanupCategoryType.WindowsTemporaryFiles,

                Title =
                    "Windows-Temp"
            };

        var converted = GetMethod(
                "CreateCategoryResult")
            .Invoke(
                null,
                [
                    category,
                    worker
                ]);

        return converted
            as CleanupActionCategoryExecutionResult
            ?? throw new InvalidOperationException(
                "Der Executor hat kein gültiges "
                + "Kategorieergebnis erzeugt.");
    }

    private static MethodInfo GetMethod(
        string methodName)
    {
        return ExecutorType.GetMethod(
                   methodName,
                   BindingFlags.NonPublic
                   | BindingFlags.Static)
               ?? throw new InvalidOperationException(
                   "Die interne Executor-Methode "
                   + "wurde nicht gefunden: "
                   + methodName);
    }

    // =============================================
    // TESTPRÜFUNGEN
    // =============================================

    private static void ExpectRejected(
        Action operation)
    {
        var wasRejected = false;

        try
        {
            operation();
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException
                  is InvalidOperationException)
        {
            /*
             * Die echte Executor-Methode
             * hat das ungültige Ergebnis
             * wie erwartet zurückgewiesen.
             */
            wasRejected = true;
        }

        Require(
            wasRejected,
            "Das ungültige Worker-Ergebnis "
            + "wurde vom Executor akzeptiert.");
    }

    private static void Execute(
        string title,
        Action test,
        ICollection<string> results)
    {
        try
        {
            test();

            results.Add(
                "OK: " + title);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Windows-Temp-Ergebnisintegrationstest "
                + "fehlgeschlagen: "
                + title
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

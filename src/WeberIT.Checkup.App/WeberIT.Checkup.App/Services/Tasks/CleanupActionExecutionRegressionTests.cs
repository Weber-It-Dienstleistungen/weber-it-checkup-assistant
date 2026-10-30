
#if DEBUG

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// SCHRITT 55.13.6
///
/// Regressionstests für gespeicherte
/// Ergebnisse kontrollierter Bereinigungen.
///
/// Keine Dateizugriffe.
/// Keine Löschoperationen.
/// Keine Änderungen am Betriebssystem.
/// </summary>
public static class CleanupActionExecutionRegressionTests
{
    public static string RunAll()
    {
        var results = new List<string>();

        Execute(
            "Erwartete Ausnahmen verhindern keinen Erfolg",
            TestExpectedSkipsAreSuccessful,
            results);

        Execute(
            "Gesamtausführung übernimmt erfolgreichen Status",
            TestOverallSuccessWithSkips,
            results);

        Execute(
            "Ursachen werden getrennt dokumentiert",
            TestSeparateSkipReasons,
            results);

        Execute(
            "Fehlende Ursachendetails bleiben erkennbar",
            TestIncompleteReasonBreakdown,
            results);

        Execute(
            "Ursachendetails überstehen JSON-Speicherung",
            TestJsonRoundTrip,
            results);

        Execute(
            "Historische Ergebnisse bleiben lesbar",
            TestLegacyJsonCompatibility,
            results);

        Execute(
            "Echte technische Fehler bleiben Fehler",
            TestTechnicalFailure,
            results);

        Execute(
            "Teilerfolg verschweigt technische Fehler nicht",
            TestPartialSuccessWithFailure,
            results);

        Execute(
            "Abbruch gilt nicht als erfolgreicher Abschluss",
            TestCancellation,
            results);

        Execute(
            "Ohne Überspringen werden keine Gründe erfunden",
            TestNoSkippedEntries,
            results);

        return
            $"Alle {results.Count} Regressionstests "
            + "für die Bereinigungsergebnisse erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    private static void TestExpectedSkipsAreSuccessful()
    {
        var category = CreateCategory(
            skipped: 3,
            reasons:
            [
                Reason(
                    CleanupSkippedEntryReason.LockedByProcess,
                    2),
                Reason(
                    CleanupSkippedEntryReason.ReadOnly,
                    1)
            ]);

        Require(
            category.IsSuccessful,
            "Erwartete Sperren dürfen den Erfolg "
            + "nicht verhindern.");

        Require(
            !category.IsPartiallySuccessful,
            "Eine Bereinigung ohne technischen Fehler "
            + "darf kein Teilerfolg sein.");

        Require(
            category.FailedEntryCount == 0,
            "Erwartete Ausnahmen wurden als "
            + "technische Fehler gezählt.");
    }

    private static void TestOverallSuccessWithSkips()
    {
        var category = CreateCategory(
            skipped: 2,
            reasons:
            [
                Reason(
                    CleanupSkippedEntryReason.SystemProtected,
                    2)
            ]);

        var execution = CreateExecution(category);

        Require(
            execution.IsSuccessful,
            "Das Gesamtergebnis muss trotz "
            + "erwarteter Ausnahmen erfolgreich sein.");

        Require(
            !execution.HasFailures,
            "Es wurden fälschlich technische "
            + "Fehler gemeldet.");

        Require(
            execution.SkippedEntryCount == 2,
            "Die übersprungenen Einträge "
            + "wurden nicht übernommen.");
    }

    private static void TestSeparateSkipReasons()
    {
        var category = CreateCategory(
            skipped: 5,
            reasons:
            [
                Reason(
                    CleanupSkippedEntryReason.ReadOnly,
                    2),
                Reason(
                    CleanupSkippedEntryReason.LockedByProcess,
                    3)
            ]);

        Require(
            category.HasSkippedEntryReasons,
            "Die Ursachendetails wurden nicht erkannt.");

        var text = category.SkippedEntryReasonsText;

        Require(
            text.Contains(
                "Schreibgeschützt",
                StringComparison.Ordinal),
            "Schreibschutz fehlt in der Dokumentation.");

        Require(
            text.Contains(
                "Dateisperre",
                StringComparison.Ordinal),
            "Dateisperren fehlen in der Dokumentation.");

        Require(
            text.Contains(
                $"2",
                StringComparison.Ordinal)
            && text.Contains(
                $"3",
                StringComparison.Ordinal),
            "Die Ursachenzähler fehlen.");
    }

    private static void TestIncompleteReasonBreakdown()
    {
        var category = CreateCategory(
            skipped: 5,
            reasons:
            [
                Reason(
                    CleanupSkippedEntryReason.ReadOnly,
                    2)
            ]);

        Require(
            category.SkippedEntryReasonsText.Contains(
                "Nicht näher aufgeschlüsselt: 3",
                StringComparison.Ordinal),
            "Nicht dokumentierte Ursachen müssen "
            + "erkennbar bleiben.");
    }

    private static void TestJsonRoundTrip()
    {
        var original = CreateCategory(
            skipped: 4,
            reasons:
            [
                Reason(
                    CleanupSkippedEntryReason.ReparsePoint,
                    1),
                Reason(
                    CleanupSkippedEntryReason.LockedByProcess,
                    3)
            ]);

        var json =
            JsonSerializer.Serialize(original);

        var restored =
            JsonSerializer.Deserialize<
                CleanupActionCategoryExecutionResult>(
                    json);

        Require(
            restored is not null,
            "Das gespeicherte Ergebnis konnte "
            + "nicht wiederhergestellt werden.");

        Require(
            restored.SkippedEntryCount == 4,
            "Der Gesamtzähler ging verloren.");

        Require(
            restored.SkippedEntryReasons.Count == 2,
            "Die Ursachendetails gingen verloren.");

        Require(
            restored.SkippedEntryReasons.Exists(
                entry =>
                    entry.Reason
                        == CleanupSkippedEntryReason.LockedByProcess
                    && entry.Count == 3),
            "Die Dateisperren wurden nicht "
            + "korrekt wiederhergestellt.");
    }

    private static void TestLegacyJsonCompatibility()
    {
        const string historicalJson =
            """
            {
              "WasStarted": true,
              "SkippedEntryCount": 3,
              "FailedEntryCount": 0,
              "ErrorMessage": ""
            }
            """;

        var restored =
            JsonSerializer.Deserialize<
                CleanupActionCategoryExecutionResult>(
                    historicalJson);

        Require(
            restored is not null,
            "Das historische Ergebnis "
            + "konnte nicht gelesen werden.");

        Require(
            restored.IsSuccessful,
            "Der historische Erfolgsstatus "
            + "wurde verändert.");

        Require(
            !restored.HasSkippedEntryReasons,
            "Für historische Daten wurden "
            + "Ursachen erfunden.");

        Require(
            restored.SkippedEntryReasonsText.Contains(
                "nicht aufgeschlüsselt",
                StringComparison.Ordinal),
            "Der fehlende Detailstand "
            + "wird nicht erklärt.");
    }

    private static void TestTechnicalFailure()
    {
        var category = CreateCategory(
            failed: 1,
            errorMessage:
                "Unbekannter Dateisystemfehler.");

        var execution = CreateExecution(category);

        Require(
            !category.IsSuccessful,
            "Ein echter Dateisystemfehler "
            + "darf kein Erfolg sein.");

        Require(
            !category.IsPartiallySuccessful,
            "Ohne durchgeführte Änderung "
            + "liegt kein Teilerfolg vor.");

        Require(
            execution.HasFailures,
            "Der technische Fehler wurde "
            + "im Gesamtergebnis verschwiegen.");
    }

    private static void TestPartialSuccessWithFailure()
    {
        var category = CreateCategory(
            deleted: 2,
            failed: 1,
            skipped: 1,
            errorMessage:
                "Ein Eintrag konnte nicht gelöscht werden.",
            reasons:
            [
                Reason(
                    CleanupSkippedEntryReason.LockedByProcess,
                    1)
            ]);

        var execution = CreateExecution(category);

        Require(
            category.IsPartiallySuccessful,
            "Die Kategorie muss als Teilerfolg "
            + "erkannt werden.");

        Require(
            execution.IsPartiallySuccessful,
            "Der Teilerfolg wurde nicht "
            + "in das Gesamtergebnis übernommen.");

        Require(
            !execution.IsSuccessful,
            "Ein Teilerfolg darf nicht "
            + "als vollständig erfolgreich gelten.");

        Require(
            execution.FailedEntryCount == 1,
            "Der tatsächliche technische "
            + "Fehler wurde nicht erhalten.");

        Require(
            execution.SkippedEntryCount == 1,
            "Der erwartete Ausnahmefall "
            + "wurde nicht separat erhalten.");
    }

    private static void TestCancellation()
    {
        var category = CreateCategory(
            deleted: 1,
            cancelled: true);

        var execution = CreateExecution(
            category,
            cancelled: true);

        Require(
            !category.IsSuccessful,
            "Eine abgebrochene Kategorie "
            + "darf nicht erfolgreich sein.");

        Require(
            !execution.IsSuccessful
            && !execution.IsPartiallySuccessful,
            "Ein Abbruch darf nicht "
            + "als Abschluss gelten.");

        Require(
            execution.DeletedFileCount == 1,
            "Bereits ausgeführte Änderungen "
            + "dürfen nicht verloren gehen.");
    }

    private static void TestNoSkippedEntries()
    {
        var category = CreateCategory();

        Require(
            !category.HasSkippedEntryReasons,
            "Es wurden Ursachendetails "
            + "ohne Grundlage gemeldet.");

        Require(
            category.SkippedEntryReasonsText
                == "Keine Einträge übersprungen.",
            "Die Anzeige für null "
            + "übersprungene Einträge ist falsch.");
    }

    // =============================================
    // TESTDATEN
    // =============================================

    private static CleanupActionCategoryExecutionResult
        CreateCategory(
            long deleted = 0,
            long failed = 0,
            long skipped = 0,
            bool cancelled = false,
            string errorMessage = "",
            List<CleanupSkippedEntryReasonCount>? reasons = null)
    {
        return new CleanupActionCategoryExecutionResult
        {
            Category =
                CleanupCategoryType.UserTemporaryFiles,

            CategoryTitle =
                "Benutzer-Temp-Testdaten",

            TargetPath =
                @"C:\Testdaten\Temp",

            WasStarted = true,

            WasCancelled = cancelled,

            DeletedFileCount = deleted,

            FailedEntryCount = failed,

            SkippedEntryCount = skipped,

            ErrorMessage = errorMessage,

            SkippedEntryReasons =
                reasons ?? new()
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

    private static CleanupActionExecutionResult
        CreateExecution(
            CleanupActionCategoryExecutionResult category,
            bool cancelled = false)
    {
        return new CleanupActionExecutionResult
        {
            PlanId = Guid.NewGuid(),

            WasCancelled = cancelled,

            CategoryResults =
            [
                category
            ]
        };
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
}

#endif

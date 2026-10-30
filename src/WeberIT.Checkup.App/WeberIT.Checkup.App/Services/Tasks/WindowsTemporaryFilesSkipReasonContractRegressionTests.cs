
#if DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.13.18
///
/// Regressionstests für die Übernahme
/// von Überspringursachen aus dem
/// erhöhten Windows-Temp-Prozess.
///
/// Keine PowerShell-Ausführung.
/// Keine Dateisystemzugriffe.
/// Keine Administratorrechte.
/// Keine Löschvorgänge.
/// </summary>
public static class
    WindowsTemporaryFilesSkipReasonContractRegressionTests
{
    public static string RunAll()
    {
        var results = new List<string>();

        Execute(
            "Historische Ergebnisse ohne Ursachendetails",
            TestLegacyResult,
            results);

        Execute(
            "Leere Ursachenliste bleibt zulässig",
            TestEmptyReasonList,
            results);

        Execute(
            "Gültige Ursachen werden übernommen und sortiert",
            TestValidReasons,
            results);

        Execute(
            "Unvollständige Aufschlüsselung bleibt zulässig",
            TestPartialBreakdown,
            results);

        Execute(
            "Unbekannter Ursachencode wird abgelehnt",
            TestUnknownReason,
            results);

        Execute(
            "Numerischer Enumcode wird abgelehnt",
            TestNumericReason,
            results);

        Execute(
            "Abweichende Schreibweise wird abgelehnt",
            TestWrongCasing,
            results);

        Execute(
            "Leerer Ursachencode wird abgelehnt",
            TestEmptyReason,
            results);

        Execute(
            "Null-Eintrag wird abgelehnt",
            TestNullEntry,
            results);

        Execute(
            "Null als Ursachenzähler wird abgelehnt",
            TestZeroCount,
            results);

        Execute(
            "Negative Ursachenzähler werden abgelehnt",
            TestNegativeCount,
            results);

        Execute(
            "Doppelte Ursachen werden abgelehnt",
            TestDuplicateReason,
            results);

        Execute(
            "Widersprüchliche Gesamtzahl wird abgelehnt",
            TestExceededTotal,
            results);

        Execute(
            "Negativer Gesamtzähler wird abgelehnt",
            TestNegativeTotal,
            results);

        return
            $"Alle {results.Count} Regressionstests "
            + "für den Windows-Temp-Ursachenvertrag erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =============================================
    // GÜLTIGE EINGABEN
    // =============================================

    private static void TestLegacyResult()
    {
        var result =
            WindowsTemporaryFilesSkipReasonContract
                .Normalize(
                    reportedReasons: null,
                    skippedEntryCount: 5);

        Require(
            result.Count == 0,
            "Für historische Ergebnisse wurden "
            + "Ursachendetails erfunden.");
    }

    private static void TestEmptyReasonList()
    {
        var result =
            WindowsTemporaryFilesSkipReasonContract
                .Normalize(
                    Array.Empty<
                        WindowsTemporaryFilesSkipReasonRecord?>(),
                    skippedEntryCount: 3);

        Require(
            result.Count == 0,
            "Eine leere Ursachenliste wurde "
            + "unerwartet verändert.");
    }

    private static void TestValidReasons()
    {
        var reported =
            new WindowsTemporaryFilesSkipReasonRecord?[]
            {
                Reason("LockedByProcess", 3),
                Reason("AlreadyMissing", 1),
                Reason("ReadOnly", 2)
            };

        var result =
            WindowsTemporaryFilesSkipReasonContract
                .Normalize(
                    reported,
                    skippedEntryCount: 6);

        Require(
            result.Count == 3,
            "Nicht alle Ursachencodes wurden übernommen.");

        Require(
            result[0].Reason
                == CleanupSkippedEntryReason.AlreadyMissing,
            "Die Ursachencodes wurden nicht "
            + "stabil sortiert.");

        Require(
            result.Any(
                entry =>
                    entry.Reason
                        == CleanupSkippedEntryReason.LockedByProcess
                    && entry.Count == 3),
            "Die Prozesssperren fehlen.");

        Require(
            result.Any(
                entry =>
                    entry.Reason
                        == CleanupSkippedEntryReason.ReadOnly
                    && entry.Count == 2),
            "Der Schreibschutz fehlt.");

        Require(
            result.Sum(entry => entry.Count) == 6,
            "Die gültigen Ursachenzähler "
            + "wurden verändert.");
    }

    private static void TestPartialBreakdown()
    {
        var result =
            WindowsTemporaryFilesSkipReasonContract
                .Normalize(
                    new WindowsTemporaryFilesSkipReasonRecord?[]
                    {
                        Reason("LockedByProcess", 2)
                    },
                    skippedEntryCount: 7);

        Require(
            result.Count == 1
            && result[0].Count == 2,
            "Eine zulässige unvollständige "
            + "Aufschlüsselung wurde nicht übernommen.");

        /*
         * Die Differenz von fünf Einträgen
         * darf der Contract nicht erfinden.
         *
         * Das Ergebnisdatenmodell zeigt
         * diese Differenz später gesondert an.
         */
    }

    // =============================================
    // UNGÜLTIGE URSACHENCODES
    // =============================================

    private static void TestUnknownReason()
    {
        ExpectRejected(
            () => NormalizeSingle(
                "UnbekannteNeueUrsache",
                count: 1,
                total: 1));
    }

    private static void TestNumericReason()
    {
        /*
         * Enum.TryParse akzeptiert allein
         * auch numerische Zeichenfolgen.
         *
         * Unser Vertrag muss dies ablehnen.
         */
        ExpectRejected(
            () => NormalizeSingle(
                "4",
                count: 1,
                total: 1));
    }

    private static void TestWrongCasing()
    {
        ExpectRejected(
            () => NormalizeSingle(
                "readonly",
                count: 1,
                total: 1));
    }

    private static void TestEmptyReason()
    {
        ExpectRejected(
            () => NormalizeSingle(
                " ",
                count: 1,
                total: 1));
    }

    private static void TestNullEntry()
    {
        ExpectRejected(
            () =>
                WindowsTemporaryFilesSkipReasonContract
                    .Normalize(
                        new WindowsTemporaryFilesSkipReasonRecord?[]
                        {
                            null
                        },
                        skippedEntryCount: 1));
    }

    // =============================================
    // UNGÜLTIGE ZÄHLER
    // =============================================

    private static void TestZeroCount()
    {
        ExpectRejected(
            () => NormalizeSingle(
                "ReadOnly",
                count: 0,
                total: 1));
    }

    private static void TestNegativeCount()
    {
        ExpectRejected(
            () => NormalizeSingle(
                "ReadOnly",
                count: -1,
                total: 1));
    }

    private static void TestDuplicateReason()
    {
        ExpectRejected(
            () =>
                WindowsTemporaryFilesSkipReasonContract
                    .Normalize(
                        new WindowsTemporaryFilesSkipReasonRecord?[]
                        {
                            Reason("ReadOnly", 1),
                            Reason("ReadOnly", 1)
                        },
                        skippedEntryCount: 2));
    }

    private static void TestExceededTotal()
    {
        ExpectRejected(
            () =>
                WindowsTemporaryFilesSkipReasonContract
                    .Normalize(
                        new WindowsTemporaryFilesSkipReasonRecord?[]
                        {
                            Reason("ReadOnly", 2),
                            Reason("LockedByProcess", 2)
                        },
                        skippedEntryCount: 3));
    }

    private static void TestNegativeTotal()
    {
        ExpectRejected(
            () =>
                WindowsTemporaryFilesSkipReasonContract
                    .Normalize(
                        reportedReasons: null,
                        skippedEntryCount: -1));
    }

    // =============================================
    // TESTHILFSMETHODEN
    // =============================================

    private static WindowsTemporaryFilesSkipReasonRecord
        Reason(
            string reason,
            long count)
    {
        return new WindowsTemporaryFilesSkipReasonRecord
        {
            Reason = reason,
            Count = count
        };
    }

    private static void NormalizeSingle(
        string reason,
        long count,
        long total)
    {
        WindowsTemporaryFilesSkipReasonContract
            .Normalize(
                new WindowsTemporaryFilesSkipReasonRecord?[]
                {
                    Reason(reason, count)
                },
                skippedEntryCount: total);
    }

    private static void ExpectRejected(
        Action operation)
    {
        var wasRejected = false;

        try
        {
            operation();
        }
        catch (InvalidOperationException)
        {
            /*
             * Erwartete Ablehnung.
             *
             * Eine ungültige Eingabe muss
             * eine fachlich definierte
             * InvalidOperationException
             * verursachen.
             */
            wasRejected = true;
        }

        Require(
            wasRejected,
            "Die ungültigen Daten wurden "
            + "vom Windows-Temp-Vertrag akzeptiert.");
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
                "Windows-Temp-Vertragstest fehlgeschlagen: "
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

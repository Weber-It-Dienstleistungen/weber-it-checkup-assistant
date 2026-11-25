#if DEBUG
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>Reine Modelltests; keine Dateisystemzugriffe.</summary>
public static class CleanupScorePotentialRegressionTests
{
    private const ulong Gb = 1024UL * 1024UL * 1024UL;
    private static readonly Guid VisitId = Guid.NewGuid();
    private static readonly DateTimeOffset Entrance = new(2026, 11, 24, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Measurement = Entrance.AddHours(1);

    public static string RunAll()
    {
        var results = new List<string>();
        Run("Vollstaendiger Lauf mit Sperren und Einzelfehlern entlastet Bewertung", () =>
        {
            var measurement = MakeMeasurement();
            Require(Score(measurement, MakeAction()) == 0);
            Require(measurement.SafePotentialBytes == 2 * Gb);
            Require(measurement.Categories[0].SizeBytes == 2 * Gb);
        }, results);
        Run("Nicht bearbeitete Kategorie bleibt bewertungsrelevant", () =>
        {
            var measurement = MakeMeasurement();
            measurement.Categories.Add(new CleanupCategoryResult
            {
                Category = CleanupCategoryType.WindowsTemporaryFiles,
                Classification = CleanupCategoryClassification.SafePotential,
                MeasurementStatus = CleanupMeasurementStatus.Measured,
                SizeBytes = Gb
            });
            Require(Score(measurement, MakeAction()) == Gb);
        }, results);
        Run("Fremder oder nicht zugeordneter Vorgang erhaelt keine Entlastung", () =>
        {
            Require(Score(MakeMeasurement(), MakeAction(visit: Guid.NewGuid())) == 2 * Gb);
            Require(Score(MakeMeasurement(), MakeAction(visit: Guid.Empty)) == 2 * Gb);
        }, results);
        Run("Abbruch, Blockade, Altdaten und unvollstaendige Bearbeitung bleiben relevant", () =>
        {
            Require(Score(MakeMeasurement(), MakeAction(traversal: false)) == 2 * Gb);
            Require(Score(MakeMeasurement(), MakeAction(cancelled: true)) == 2 * Gb);
            Require(Score(MakeMeasurement(), MakeAction(blocked: true)) == 2 * Gb);
            Require(Score(MakeMeasurement(), MakeAction(status: CheckupTaskActionStatus.Failed)) == 2 * Gb);
        }, results);
        Run("Zeitlich unpassender Nachweis bleibt unberuecksichtigt", () =>
        {
            Require(Score(MakeMeasurement(), MakeAction(start: Entrance.AddMinutes(-10))) == 2 * Gb);
            Require(Score(MakeMeasurement(), MakeAction(start: Measurement)) == 2 * Gb);
        }, results);
        Run("Mehrdeutige Ausfuehrung oder Messung gibt keine Entlastung", () =>
        {
            Require(Score(MakeMeasurement(), MakeAction(), MakeAction()) == 2 * Gb);
            var measurement = MakeMeasurement();
            measurement.Categories.Add(measurement.Categories[0]);
            Require(Score(measurement, MakeAction()) == 4 * Gb);
        }, results);
        Run("Ungeklaerte Ausschluesse und unvollstaendige Messung bleiben vorsichtig bewertet", () =>
        {
            Require(Score(MakeMeasurement(), MakeAction(unknownSkip: true)) == 2 * Gb);
            var measurement = MakeMeasurement();
            measurement.AnalysisStatus = CleanupMeasurementStatus.PartiallyMeasured;
            Require(Score(measurement, MakeAction()) == measurement.SafePotentialBytes);
        }, results);

        return $"Alle {results.Count} Bewertungsgrundlagen-Regressionstests erfolgreich."
            + Environment.NewLine + string.Join(Environment.NewLine, results);
    }

    private static CleanupPotentialInformation MakeMeasurement() => new()
    {
        AnalysisStatus = CleanupMeasurementStatus.Measured,
        Categories = new()
        {
            new()
            {
                Category = CleanupCategoryType.UserTemporaryFiles,
                Classification = CleanupCategoryClassification.SafePotential,
                MeasurementStatus = CleanupMeasurementStatus.Measured,
                SizeBytes = 2 * Gb
            }
        }
    };

    private static CheckupTaskActionResult MakeAction(
        Guid? visit = null, bool traversal = true, bool cancelled = false,
        bool blocked = false, DateTimeOffset? start = null,
        bool unknownSkip = false,
        CheckupTaskActionStatus status = CheckupTaskActionStatus.Successful)
    {
        var started = start ?? Entrance.AddMinutes(10);
        var finished = started.AddMinutes(5);
        return new()
        {
            Id = Guid.NewGuid(),
            ActionCode = "action.cleanup.selected-safe-categories",
            CleanupCustomerCheckupVisitId = visit ?? VisitId,
            Status = status,
            StartedAt = started,
            FinishedAt = finished,
            CleanupExecutionResult = new()
            {
                PlanId = Guid.NewGuid(),
                WasCancelled = cancelled,
                WasBlocked = blocked,
                StartedAt = started,
                FinishedAt = finished,
                CategoryResults = new()
                {
                    new()
                    {
                        Category = CleanupCategoryType.UserTemporaryFiles,
                        WasStarted = true,
                        WasCancelled = cancelled,
                        WasTraversalCompleted = traversal,
                        DeletedFileCount = 42,
                        FailedEntryCount = 4,
                        SkippedEntryCount = 1,
                        SkippedEntryReasons = new()
                        {
                            new()
                            {
                                Reason = unknownSkip
                                    ? CleanupSkippedEntryReason.OtherSafetySkip
                                    : CleanupSkippedEntryReason.LockedByProcess,
                                Count = 1
                            }
                        }
                    }
                }
            }
        };
    }

    private static ulong Score(CleanupPotentialInformation measurement,
        params CheckupTaskActionResult[] actions)
    {
        var tasks = new CheckupTaskList
        {
            Tasks = new()
            {
                new()
                {
                    TaskCode = "task.storage.controlled-cleanup",
                    ActionResults = actions.ToList()
                }
            }
        };
        return CleanupScorePotentialService.GetScoreRelevantSafePotentialBytes(
            measurement, tasks, VisitId, Entrance, Measurement);
    }

    private static void Run(string name, Action test, ICollection<string> results)
    {
        try { test(); results.Add("OK: " + name); }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Bewertungsgrundlagen-Test fehlgeschlagen: " + name, exception);
        }
    }

    private static void Require(bool condition)
    {
        if (!condition)
            throw new InvalidOperationException("Unerwartetes bewertungsrelevantes Bereinigungspotenzial.");
    }
}
#endif

using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public class StartupInformation
{
    public DateTime? AnalysisDate { get; set; }

    public long? AnalysisDurationMilliseconds { get; set; }

    public StartupAnalysisStatus AnalysisStatus { get; set; } =
        StartupAnalysisStatus.NotAnalyzed;

    public string AnalysisMessage { get; set; } =
        string.Empty;

    public List<StartupEntryInformation> Entries { get; set; } =
        new();

    [JsonIgnore]
    public bool HasAnalysis =>
        AnalysisStatus != StartupAnalysisStatus.NotAnalyzed;

    [JsonIgnore]
    public int TotalEntryCount =>
        GetAvailableEntries().Count();

    [JsonIgnore]
    public int EnabledEntryCount =>
        GetAvailableEntries().Count(
            entry =>
                entry.State
                == StartupEntryState.Enabled);

    [JsonIgnore]
    public int DisabledEntryCount =>
        GetAvailableEntries().Count(
            entry =>
                entry.State
                == StartupEntryState.Disabled);

    [JsonIgnore]
    public int UnknownStateEntryCount =>
        GetAvailableEntries().Count(
            entry =>
                entry.State
                == StartupEntryState.Unknown);

    [JsonIgnore]
    public int ActiveNotEvaluableEntryCount =>
        GetAvailableEntries().Count(
            entry =>
                entry.State
                    == StartupEntryState.Enabled
                && entry.Classification
                    == StartupClassification.NotEvaluable);

    [JsonIgnore]
    public int OptionalReviewEntryCount =>
        GetAvailableEntries().Count(
            entry =>
                entry.State
                    == StartupEntryState.Enabled
                && entry.Classification
                    == StartupClassification.OptionalReview);

    [JsonIgnore]
    public int ConspicuousEntryCount =>
        GetAvailableEntries().Count(
            entry =>
                entry.State
                    == StartupEntryState.Enabled
                && entry.Classification
                    == StartupClassification.Conspicuous);

    [JsonIgnore]
    public IReadOnlyList<StartupEntryInformation>
        ActiveEntries =>
            GetAvailableEntries()
                .Where(
                    entry =>
                        entry.State
                            == StartupEntryState.Enabled
                        && entry.Classification
                            != StartupClassification.NotEvaluable)
                .OrderByDescending(
                    entry =>
                        GetActiveEntryPriority(
                            entry.Classification))
                .ThenBy(
                    entry =>
                        entry.DisplayNameText,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToList();

    [JsonIgnore]
    public int DisplayedActiveEntryCount =>
        ActiveEntries.Count;

    [JsonIgnore]
    public int HiddenEntryCount =>
        Math.Max(
            0,
            TotalEntryCount
            - DisplayedActiveEntryCount);

    [JsonIgnore]
    public bool HasActiveEntries =>
        DisplayedActiveEntryCount > 0;

    [JsonIgnore]
    public string ActiveEntriesOverviewText
    {
        get
        {
            var visibleText =
                DisplayedActiveEntryCount switch
                {
                    0 =>
                        "Keine auswertbaren aktiven "
                        + "Autostarteinträge werden angezeigt",

                    1 =>
                        "1 aktiver und auswertbarer, potenziell "
                        + "startrelevanter Eintrag wird angezeigt",

                    _ =>
                        $"{DisplayedActiveEntryCount} aktive und "
                        + "auswertbare, potenziell startrelevante "
                        + "Einträge werden angezeigt"
                };

            var hiddenParts =
                new List<string>();

            if (DisabledEntryCount > 0)
            {
                hiddenParts.Add(
                    DisabledEntryCount == 1
                        ? "1 deaktivierter Eintrag"
                        : $"{DisabledEntryCount} deaktivierte Einträge");
            }

            if (UnknownStateEntryCount > 0)
            {
                hiddenParts.Add(
                    UnknownStateEntryCount == 1
                        ? "1 Eintrag mit unklarem Status"
                        : $"{UnknownStateEntryCount} Einträge "
                          + "mit unklarem Status");
            }

            if (ActiveNotEvaluableEntryCount > 0)
            {
                hiddenParts.Add(
                    ActiveNotEvaluableEntryCount == 1
                        ? "1 aktiver, nicht auswertbarer Eintrag"
                        : $"{ActiveNotEvaluableEntryCount} aktive, "
                          + "nicht auswertbare Einträge");
            }

            if (hiddenParts.Count == 0)
            {
                return visibleText
                       + ".";
            }

            return visibleText
                   + ". Ausgeblendet: "
                   + string.Join(
                       ", ",
                       hiddenParts)
                   + ".";
        }
    }

    [JsonIgnore]
    public bool HasFailedOrIncompleteAnalysis =>
        AnalysisStatus
            is StartupAnalysisStatus.PartiallyAnalyzed
            or StartupAnalysisStatus.NotEvaluable
            or StartupAnalysisStatus.TimedOut;

    [JsonIgnore]
    public string AnalysisStatusText =>
        AnalysisStatus switch
        {
            StartupAnalysisStatus.NotAnalyzed =>
                "Nicht in diesem Checkup enthalten",

            StartupAnalysisStatus.Analyzed =>
                "Vollständig analysiert",

            StartupAnalysisStatus.PartiallyAnalyzed =>
                "Teilweise analysiert",

            StartupAnalysisStatus.NotEvaluable =>
                "Nicht auswertbar",

            StartupAnalysisStatus.TimedOut =>
                "Zeitlimit erreicht",

            _ =>
                "Unbekannter Analysestatus"
        };

    [JsonIgnore]
    public string AnalysisMessageText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(
                    AnalysisMessage))
            {
                return AnalysisMessage;
            }

            return HasAnalysis
                ? "Es sind keine weiteren Angaben zur "
                  + "Autostartanalyse verfügbar."
                : "Dieser gespeicherte Checkup wurde vor "
                  + "Einführung der Autostartanalyse erstellt.";
        }
    }

    [JsonIgnore]
    public string AnalysisDateText =>
        AnalysisDate.HasValue
            ? AnalysisDate.Value.ToString(
                "dd.MM.yyyy HH:mm")
            : "Nicht verfügbar";

    [JsonIgnore]
    public string AnalysisDurationText
    {
        get
        {
            if (!AnalysisDurationMilliseconds.HasValue)
            {
                return "Nicht verfügbar";
            }

            var duration =
                TimeSpan.FromMilliseconds(
                    AnalysisDurationMilliseconds.Value);

            return duration.TotalSeconds >= 1
                ? $"{duration.TotalSeconds:0.##} Sekunden"
                : $"{Math.Max(
                    0,
                    duration.TotalMilliseconds):0} ms";
        }
    }

    private IEnumerable<StartupEntryInformation>
        GetAvailableEntries()
    {
        return Entries
               ?? Enumerable.Empty<StartupEntryInformation>();
    }

    private static int GetActiveEntryPriority(
        StartupClassification classification)
    {
        return classification switch
        {
            StartupClassification.Conspicuous =>
                5,

            StartupClassification.OptionalReview =>
                4,

            StartupClassification.Unknown =>
                3,

            StartupClassification.ProbablyUseful =>
                2,

            StartupClassification.SystemOrDriverRelated =>
                1,

            _ =>
                0
        };
    }
}
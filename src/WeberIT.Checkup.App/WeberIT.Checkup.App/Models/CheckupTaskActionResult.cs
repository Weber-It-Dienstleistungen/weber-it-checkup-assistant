using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public class CheckupTaskActionResult
{
    public Guid Id { get; set; } =
        Guid.NewGuid();

    public string ActionCode { get; set; } =
        string.Empty;

    public string ActionTitle { get; set; } =
        string.Empty;

    public string TargetDescription { get; set; } =
        string.Empty;

    public CheckupTaskActionStatus Status { get; set; } =
        CheckupTaskActionStatus.Unknown;

    public string Summary { get; set; } =
        string.Empty;

    public string Details { get; set; } =
        string.Empty;

    public int? ExitCode { get; set; }

    public bool RestartRequired { get; set; }

    public bool RestartStatusWasConclusive { get; set; } =
        true;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    [JsonIgnore]
    public bool WasSuccessful =>
        Status == CheckupTaskActionStatus.Successful;

    [JsonIgnore]
    public bool WasCancelled =>
        Status == CheckupTaskActionStatus.Cancelled;

    [JsonIgnore]
    public string StatusText =>
        Status switch
        {
            CheckupTaskActionStatus.Successful =>
                "Technisch erfolgreich ausgeführt",

            CheckupTaskActionStatus.Failed =>
                "Fehlgeschlagen",

            CheckupTaskActionStatus.Cancelled =>
                "Abgebrochen",

            _ =>
                "Ergebnis nicht eindeutig"
        };

    [JsonIgnore]
    public string RestartRequirementText
    {
        get
        {
            if (!RestartStatusWasConclusive)
            {
                return
                    "Neustartstatus nicht eindeutig";
            }

            return RestartRequired
                ? "Neustart erforderlich"
                : "Kein Neustartbedarf gemeldet";
        }
    }

    [JsonIgnore]
    public string StartedAtText =>
        StartedAt.HasValue
            ? StartedAt.Value
                .ToLocalTime()
                .ToString("dd.MM.yyyy HH:mm")
              + " Uhr"
            : "Startzeitpunkt nicht verfügbar";

    [JsonIgnore]
    public string FinishedAtText =>
        FinishedAt.HasValue
            ? FinishedAt.Value
                .ToLocalTime()
                .ToString("dd.MM.yyyy HH:mm")
              + " Uhr"
            : "Abschlusszeitpunkt nicht verfügbar";

    [JsonIgnore]
    public string ExitCodeText =>
        ExitCode.HasValue
            ? ExitCode.Value.ToString()
            : "Nicht verfügbar";

    [JsonIgnore]
    public TimeSpan Duration =>
        StartedAt.HasValue
        && FinishedAt.HasValue
        && FinishedAt.Value > StartedAt.Value
            ? FinishedAt.Value - StartedAt.Value
            : TimeSpan.Zero;

    [JsonIgnore]
    public string DurationText
    {
        get
        {
            if (!StartedAt.HasValue
                || !FinishedAt.HasValue)
            {
                return "Nicht verfügbar";
            }

            if (Duration.TotalMinutes >= 1)
            {
                return
                    $"{(int)Duration.TotalMinutes} Min. "
                    + $"{Duration.Seconds} Sek.";
            }

            return
                $"{Math.Max(
                    1,
                    (int)Math.Ceiling(
                        Duration.TotalSeconds))} Sek.";
        }
    }
}
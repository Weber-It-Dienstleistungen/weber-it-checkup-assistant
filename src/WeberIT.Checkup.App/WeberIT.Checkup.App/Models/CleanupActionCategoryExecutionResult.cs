using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public sealed class CleanupActionCategoryExecutionResult
{
    public CleanupCategoryType Category { get; init; }

    public string CategoryTitle { get; init; } =
        string.Empty;

    public string TargetPath { get; init; } =
        string.Empty;

    public bool WasStarted { get; init; }

    public bool WasCancelled { get; init; }

    public long DeletedFileCount { get; init; }

    public long DeletedDirectoryCount { get; init; }

    public ulong DeletedSizeBytes { get; init; }

    public long FailedEntryCount { get; init; }

    public long SkippedEntryCount { get; init; }

    public string ErrorMessage { get; init; } =
        string.Empty;

    [JsonIgnore]
    public bool HasChanges =>
        DeletedFileCount > 0
        || DeletedDirectoryCount > 0;

    [JsonIgnore]
    public bool IsSuccessful =>
        WasStarted
        && !WasCancelled
        && FailedEntryCount == 0
        && string.IsNullOrWhiteSpace(
            ErrorMessage);

    [JsonIgnore]
    public bool IsPartiallySuccessful =>
        WasStarted
        && !WasCancelled
        && HasChanges
        && (FailedEntryCount > 0
            || !string.IsNullOrWhiteSpace(
                ErrorMessage));

    [JsonIgnore]
    public string DeletedSizeText =>
        FormatBytes(
            DeletedSizeBytes);

    [JsonIgnore]
    public string DeletedFileCountText =>
        DeletedFileCount switch
        {
            0 =>
                "Keine Datei gelöscht",

            1 =>
                "1 Datei gelöscht",

            _ =>
                $"{DeletedFileCount:N0} Dateien gelöscht"
        };

    [JsonIgnore]
    public string DeletedDirectoryCountText =>
        DeletedDirectoryCount switch
        {
            0 =>
                "Kein Unterordner entfernt",

            1 =>
                "1 leerer Unterordner entfernt",

            _ =>
                $"{DeletedDirectoryCount:N0} "
                + "leere Unterordner entfernt"
        };

    [JsonIgnore]
    public string FailureCountText =>
        FailedEntryCount switch
        {
            0 =>
                "Keine technischen Löschfehler",

            1 =>
                "1 Eintrag konnte nicht gelöscht werden",

            _ =>
                $"{FailedEntryCount:N0} Einträge konnten "
                + "nicht gelöscht werden"
        };

    [JsonIgnore]
    public string SkippedEntryCountText =>
        SkippedEntryCount switch
        {
            0 =>
                "Keine Einträge sicherheitsbedingt übersprungen",

            1 =>
                "1 Eintrag sicherheitsbedingt übersprungen",

            _ =>
                $"{SkippedEntryCount:N0} Einträge "
                + "sicherheitsbedingt übersprungen"
        };

    private static string FormatBytes(
        ulong sizeBytes)
    {
        const double oneKilobyte =
            1024d;

        const double oneMegabyte =
            oneKilobyte * 1024d;

        const double oneGigabyte =
            oneMegabyte * 1024d;

        if (sizeBytes >= oneGigabyte)
        {
            return
                $"{sizeBytes / oneGigabyte:0.##} GB";
        }

        if (sizeBytes >= oneMegabyte)
        {
            return
                $"{sizeBytes / oneMegabyte:0.##} MB";
        }

        if (sizeBytes >= oneKilobyte)
        {
            return
                $"{sizeBytes / oneKilobyte:0.##} KB";
        }

        return
            $"{sizeBytes:N0} Byte";
    }
}
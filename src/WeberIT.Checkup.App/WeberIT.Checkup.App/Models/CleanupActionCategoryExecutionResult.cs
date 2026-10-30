
using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

/// <summary>
/// Dokumentierte Gründe für Einträge,
/// die bei einer kontrollierten Bereinigung
/// bewusst nicht entfernt wurden.
///
/// Die Werte sind Bestandteil des
/// dauerhaft gespeicherten Ergebnisses.
/// </summary>
public enum CleanupSkippedEntryReason
{
    AlreadyMissing,
    ReparsePoint,
    ReadOnly,
    SystemProtected,
    LockedByProcess,
    DirectoryNotEmpty,
    OtherSafetySkip
}

/// <summary>
/// Anzahl übersprungener Einträge
/// für eine bestimmte Ursache.
/// </summary>
public sealed class CleanupSkippedEntryReasonCount
{
    public CleanupSkippedEntryReason Reason
    {
        get;
        init;
    }

    public long Count
    {
        get;
        init;
    }
}

/// <summary>
/// Persistiertes Ergebnis einer einzelnen
/// Bereinigungskategorie.
/// </summary>
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

    /*
     * Tatsächliche technische Fehler.
     *
     * Diese dürfen nicht pauschal als
     * erwartete Sperren behandelt werden.
     */
    public long FailedEntryCount { get; init; }

    /*
     * Gesamtzahl kontrolliert
     * übersprungener Einträge.
     *
     * Das bestehende Feld bleibt für
     * ältere gespeicherte Checkups erhalten.
     */
    public long SkippedEntryCount { get; init; }

    /*
     * SCHRITT 55.13.4
     *
     * Optionale, dauerhaft gespeicherte
     * Aufschlüsselung der übersprungenen
     * Einträge nach Ursache.
     *
     * Ältere Ergebnisse besitzen diese
     * Liste nicht und erhalten beim
     * Deserialisieren den leeren Standard.
     *
     * Die Gesamtzahl bleibt weiterhin
     * SkippedEntryCount.
     */
    public List<CleanupSkippedEntryReasonCount>
        SkippedEntryReasons
    {
        get;
        init;
    } = new();

    public string ErrorMessage { get; init; } =
        string.Empty;

    // =============================================
    // TECHNISCHER ERGEBNISSTATUS
    // =============================================

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

    /*
     * Übersprungene Einträge sind bewusst
     * kein eigenes Fehlerkriterium.
     *
     * Ein kontrolliert ausgelassener
     * Eintrag darf den technischen Erfolg
     * der übrigen Bereinigung nicht
     * automatisch verhindern.
     *
     * Der fachliche Aufgabenstatus wird
     * dadurch nicht automatisch geändert.
     */

    // =============================================
    // VORHANDENE ANZEIGETEXTE
    // =============================================

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

    // =============================================
    // NEUE DETAILLIERTE DOKUMENTATION
    // =============================================

    [JsonIgnore]
    public bool HasSkippedEntryReasons =>
        SkippedEntryReasons is { Count: > 0 }
        && SkippedEntryReasons.Any(
            entry => entry.Count > 0);

    /*
     * Der Anzeigetext wird aus den
     * persistierten Ursachenzählern
     * erzeugt.
     *
     * Er wird nicht zusätzlich gespeichert,
     * damit keine zweite Datenwahrheit
     * entsteht.
     */
    [JsonIgnore]
    public string SkippedEntryReasonsText
    {
        get
        {
            if (SkippedEntryCount <= 0)
            {
                return
                    "Keine Einträge übersprungen.";
            }

            var reasonCounts =
                (SkippedEntryReasons
                 ?? new List<CleanupSkippedEntryReasonCount>())
                    .Where(entry => entry.Count > 0)
                    .GroupBy(entry => entry.Reason)
                    .Select(group => new
                    {
                        Reason = group.Key,
                        Count = group.Sum(
                            entry => entry.Count)
                    })
                    .OrderBy(group => group.Reason)
                    .ToList();

            if (reasonCounts.Count == 0)
            {
                return
                    $"{SkippedEntryCount:N0} Einträge "
                    + "übersprungen. Die einzelnen "
                    + "Ursachen wurden in diesem "
                    + "Ergebnis nicht aufgeschlüsselt.";
            }

            var parts =
                reasonCounts
                    .Select(group =>
                        $"{GetReasonDescription(group.Reason)}: "
                        + $"{group.Count:N0}")
                    .ToList();

            /*
             * Bei älteren oder teilweise
             * dokumentierten Ergebnissen
             * dürfen wir keine Ursachen
             * für fehlende Detailzähler
             * erfinden.
             */
            var documentedCount =
                reasonCounts.Sum(
                    group => group.Count);

            var notDetailedCount =
                Math.Max(
                    0,
                    SkippedEntryCount - documentedCount);

            if (notDetailedCount > 0)
            {
                parts.Add(
                    "Nicht näher aufgeschlüsselt: "
                    + $"{notDetailedCount:N0}");
            }

            return string.Join(
                Environment.NewLine,
                parts);
        }
    }

    private static string GetReasonDescription(
        CleanupSkippedEntryReason reason)
    {
        return reason switch
        {
            CleanupSkippedEntryReason.AlreadyMissing =>
                "Bereits nicht mehr vorhanden",

            CleanupSkippedEntryReason.ReparsePoint =>
                "Dateisystemverweis nicht verfolgt",

            CleanupSkippedEntryReason.ReadOnly =>
                "Schreibgeschützt",

            CleanupSkippedEntryReason.SystemProtected =>
                "Systemgeschützt",

            CleanupSkippedEntryReason.LockedByProcess =>
                "Durch Prozess oder Dateisperre blockiert",

            CleanupSkippedEntryReason.DirectoryNotEmpty =>
                "Ordner noch nicht leer",

            CleanupSkippedEntryReason.OtherSafetySkip =>
                "Sonstiger sicherheitsbedingter Ausschluss",

            _ =>
                "Nicht näher klassifiziert"
        };
    }

    // =============================================
    // GRÖSSENFORMATIERUNG
    // =============================================

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

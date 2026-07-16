using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public class CleanupCategoryResult
{
    public CleanupCategoryType Category { get; set; }

    public CleanupCategoryClassification Classification
    {
        get;
        set;
    } = CleanupCategoryClassification.Information;

    public CleanupMeasurementStatus MeasurementStatus
    {
        get;
        set;
    } = CleanupMeasurementStatus.NotAnalyzed;

    public ulong? SizeBytes { get; set; }

    public long? FileCount { get; set; }

    public string Description { get; set; } =
        string.Empty;

    [JsonIgnore]
    public bool HasMeasuredSize =>
        SizeBytes.HasValue
        && MeasurementStatus
            is CleanupMeasurementStatus.Measured
            or CleanupMeasurementStatus.PartiallyMeasured
            or CleanupMeasurementStatus.TimedOut;

    [JsonIgnore]
    public bool IsFullyMeasured =>
        MeasurementStatus
            == CleanupMeasurementStatus.Measured;

    [JsonIgnore]
    public string CategoryText =>
        Category switch
        {
            CleanupCategoryType.UserTemporaryFiles =>
                "Benutzertemporärdateien",

            CleanupCategoryType.WindowsTemporaryFiles =>
                "Windows-Temp",

            CleanupCategoryType.DirectXShaderCache =>
                "DirectX-Shadercache",

            CleanupCategoryType.ThumbnailCache =>
                "Vorschaubildcache",

            CleanupCategoryType.BrowserCache =>
                "Browsercache",

            CleanupCategoryType.RecycleBin =>
                "Papierkorb",

            CleanupCategoryType.WindowsUpdateDownloadCache =>
                "Windows-Update-Downloadcache",

            CleanupCategoryType.WindowsErrorReports =>
                "Windows-Fehlerberichte",

            CleanupCategoryType.MemoryDumps =>
                "Speicherabbilder",

            CleanupCategoryType.PreviousWindowsInstallation =>
                "Vorherige Windows-Installation",

            CleanupCategoryType.StorageSense =>
                "Automatische Speicherbereinigung",

            _ =>
                "Unbekannte Kategorie"
        };

    [JsonIgnore]
    public string ClassificationText =>
        Classification switch
        {
            CleanupCategoryClassification.SafePotential =>
                "Voraussichtlich unkritisches Potenzial",

            CleanupCategoryClassification.ManualReview =>
                "Manuell zu prüfen",

            CleanupCategoryClassification.Information =>
                "Information",

            CleanupCategoryClassification.Excluded =>
                "Ausgeschlossen",

            _ =>
                "Nicht eingeordnet"
        };

    [JsonIgnore]
    public string MeasurementStatusText =>
        MeasurementStatus switch
        {
            CleanupMeasurementStatus.NotAnalyzed =>
                "Nicht analysiert",

            CleanupMeasurementStatus.Measured =>
                "Vollständig gemessen",

            CleanupMeasurementStatus.PartiallyMeasured =>
                "Teilweise gemessen",

            CleanupMeasurementStatus.NotEvaluable =>
                "Nicht auswertbar",

            CleanupMeasurementStatus.Excluded =>
                "Nicht Bestandteil der Analyse",

            CleanupMeasurementStatus.TimedOut =>
                "Zeitlimit erreicht",

            _ =>
                "Unbekannter Messstatus"
        };

    [JsonIgnore]
    public string SizeText =>
        SizeBytes.HasValue
            ? FormatBytes(SizeBytes.Value)
            : "Nicht verfügbar";

    [JsonIgnore]
    public string FileCountText
    {
        get
        {
            if (!FileCount.HasValue)
            {
                return "Dateianzahl nicht verfügbar";
            }

            return FileCount.Value == 1
                ? "1 Datei erfasst"
                : $"{FileCount.Value:N0} Dateien erfasst";
        }
    }

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
            return $"{sizeBytes / oneGigabyte:0.##} GB";
        }

        if (sizeBytes >= oneMegabyte)
        {
            return $"{sizeBytes / oneMegabyte:0.##} MB";
        }

        if (sizeBytes >= oneKilobyte)
        {
            return $"{sizeBytes / oneKilobyte:0.##} KB";
        }

        return $"{sizeBytes:N0} Byte";
    }
}
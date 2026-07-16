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
            == CleanupMeasurementStatus.Measured;

    [JsonIgnore]
    public bool IsFullyMeasured =>
        MeasurementStatus
            == CleanupMeasurementStatus.Measured;

    [JsonIgnore]
    public bool HasIncompleteMeasurement =>
        SizeBytes.HasValue
        && MeasurementStatus
            is CleanupMeasurementStatus.PartiallyMeasured
            or CleanupMeasurementStatus.TimedOut;

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

            CleanupMeasurementStatus.InformationOnly =>
                "Nur Vorhandensein geprüft",

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
    public string SizeText
    {
        get
        {
            if (MeasurementStatus
                == CleanupMeasurementStatus.InformationOnly)
            {
                return "Größe bewusst nicht ermittelt";
            }

            if (!SizeBytes.HasValue)
            {
                return "Nicht verfügbar";
            }

            var formattedSize =
                FormatBytes(
                    SizeBytes.Value);

            return HasIncompleteMeasurement
                ? $"Mindestens {formattedSize} erfasst"
                : formattedSize;
        }
    }

    [JsonIgnore]
    public string FileCountText
    {
        get
        {
            if (MeasurementStatus
                == CleanupMeasurementStatus.InformationOnly)
            {
                return "Keine Dateiliste erfasst";
            }

            if (!FileCount.HasValue)
            {
                return "Dateianzahl nicht verfügbar";
            }

            var formattedCount =
                FileCount.Value == 1
                    ? "1 Datei"
                    : $"{FileCount.Value:N0} Dateien";

            return HasIncompleteMeasurement
                ? $"Mindestens {formattedCount} erfasst"
                : $"{formattedCount} erfasst";
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
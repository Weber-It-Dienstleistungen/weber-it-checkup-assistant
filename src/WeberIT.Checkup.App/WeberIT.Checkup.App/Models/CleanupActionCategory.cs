using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public sealed class CleanupActionCategory
{
    public CleanupCategoryType? Category { get; init; }

    public CleanupCategoryClassification Classification
    {
        get;
        init;
    } = CleanupCategoryClassification.Excluded;

    public CleanupMeasurementStatus MeasurementStatus
    {
        get;
        init;
    } = CleanupMeasurementStatus.NotAnalyzed;

    public string Title { get; init; } =
        string.Empty;

    public string TargetAreaDescription { get; init; } =
        string.Empty;

    public ulong MeasuredSizeBytes { get; init; }

    public long MeasuredFileCount { get; init; }

    [JsonIgnore]
    public bool HasIncompleteMeasurement =>
        MeasurementStatus
            == CleanupMeasurementStatus.PartiallyMeasured;

    [JsonIgnore]
    public string MeasurementStatusText =>
        MeasurementStatus switch
        {
            CleanupMeasurementStatus.Measured =>
                "VOLLSTÄNDIG GEMESSEN",

            CleanupMeasurementStatus.PartiallyMeasured =>
                "TEILWEISE GEMESSEN",

            _ =>
                "NICHT VOLLSTÄNDIG GEMESSEN"
        };

    [JsonIgnore]
    public string SizeText
    {
        get
        {
            var formattedSize =
                FormatBytes(
                    MeasuredSizeBytes);

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
            if (HasIncompleteMeasurement)
            {
                return MeasuredFileCount == 1
                    ? "mindestens 1 Datei erfasst"
                    : $"mindestens {MeasuredFileCount:N0} "
                      + "Dateien erfasst";
            }

            return MeasuredFileCount == 1
                ? "1 vollständig gemessene Datei"
                : $"{MeasuredFileCount:N0} "
                  + "vollständig gemessene Dateien";
        }
    }

    [JsonIgnore]
    public string MeasurementSummaryText =>
        SizeText
        + ", "
        + FileCountText;

    public void Validate()
    {
        if (!Category.HasValue)
        {
            throw new InvalidOperationException(
                "Eine Bereinigungskategorie besitzt "
                + "keinen eindeutigen Kategoriecode.");
        }

        if (!Enum.IsDefined(
                typeof(CleanupCategoryType),
                Category.Value))
        {
            throw new InvalidOperationException(
                "Der Bereinigungsplan enthält eine "
                + "unbekannte Kategorie.");
        }

        if (!IsSupportedCategory(
                Category.Value))
        {
            throw new InvalidOperationException(
                "Die Bereinigungskategorie ist nicht "
                + "für eine automatische Aktion freigegeben.");
        }

        if (Classification
            != CleanupCategoryClassification.SafePotential)
        {
            throw new InvalidOperationException(
                "Die Bereinigungskategorie wurde nicht "
                + "als sicher auswählbares Potenzial eingestuft.");
        }

        if (!HasSupportedMeasurement(
                Category.Value,
                MeasurementStatus))
        {
            throw new InvalidOperationException(
                "Die Bereinigungskategorie besitzt kein "
                + "für diese Aktion ausreichend belastbares "
                + "Messergebnis.");
        }

        if (MeasuredFileCount < 0)
        {
            throw new InvalidOperationException(
                "Die gespeicherte Dateianzahl der "
                + "Bereinigungskategorie ist ungültig.");
        }

        if (string.IsNullOrWhiteSpace(
                Title))
        {
            throw new InvalidOperationException(
                "Die Bereinigungskategorie benötigt "
                + "eine verständliche Bezeichnung.");
        }

        if (string.IsNullOrWhiteSpace(
                TargetAreaDescription))
        {
            throw new InvalidOperationException(
                "Der vorgesehene Zielbereich der "
                + "Bereinigungskategorie ist nicht beschrieben.");
        }
    }

    public static bool IsSupportedCategory(
        CleanupCategoryType category)
    {
        return category
            is CleanupCategoryType.UserTemporaryFiles
            or CleanupCategoryType.DirectXShaderCache
            or CleanupCategoryType.ThumbnailCache;
    }

    private static bool HasSupportedMeasurement(
        CleanupCategoryType category,
        CleanupMeasurementStatus measurementStatus)
    {
        if (measurementStatus
            == CleanupMeasurementStatus.Measured)
        {
            return true;
        }

        return category
                   == CleanupCategoryType.UserTemporaryFiles
               && measurementStatus
                   == CleanupMeasurementStatus.PartiallyMeasured;
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
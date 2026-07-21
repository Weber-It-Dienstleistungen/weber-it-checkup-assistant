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
    public string SizeText =>
        FormatBytes(
            MeasuredSizeBytes);

    [JsonIgnore]
    public string FileCountText =>
        MeasuredFileCount == 1
            ? "1 vollständig gemessene Datei"
            : $"{MeasuredFileCount:N0} vollständig gemessene Dateien";

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

        if (MeasurementStatus
            != CleanupMeasurementStatus.Measured)
        {
            throw new InvalidOperationException(
                "Die Bereinigungskategorie wurde nicht "
                + "vollständig gemessen.");
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
            or CleanupCategoryType.WindowsTemporaryFiles
            or CleanupCategoryType.DirectXShaderCache
            or CleanupCategoryType.ThumbnailCache
            or CleanupCategoryType.BrowserCache;
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
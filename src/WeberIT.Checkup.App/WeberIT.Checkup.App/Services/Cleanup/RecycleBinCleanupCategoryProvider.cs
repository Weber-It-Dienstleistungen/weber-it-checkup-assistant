using System.Runtime.InteropServices;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Cleanup;

internal sealed class RecycleBinCleanupCategoryProvider
{
    public CleanupCategoryResult Analyze(
        string systemVolumeRoot,
        DateTime deadline)
    {
        var result =
            new CleanupCategoryResult
            {
                Category =
                    CleanupCategoryType.RecycleBin,

                Classification =
                    CleanupCategoryClassification.ManualReview,

                Description =
                    "Inhalte des Papierkorbs auf dem "
                    + "Windows-Systemvolume"
            };

        if (DateTime.UtcNow >= deadline)
        {
            result.MeasurementStatus =
                CleanupMeasurementStatus.TimedOut;

            result.Description +=
                ". Die Kategorie wurde wegen des "
                + "erreichten Zeitlimits nicht untersucht.";

            return result;
        }

        if (string.IsNullOrWhiteSpace(
                systemVolumeRoot)
            || IsNetworkPath(
                systemVolumeRoot))
        {
            result.MeasurementStatus =
                CleanupMeasurementStatus.Excluded;

            result.Description +=
                ". Das Zielvolume konnte nicht "
                + "als lokales Systemvolume bestätigt werden.";

            return result;
        }

        try
        {
            var recycleBinInformation =
                new RecycleBinInformation
                {
                    StructureSize =
                        Marshal.SizeOf<RecycleBinInformation>()
                };

            var resultCode =
                SHQueryRecycleBin(
                    systemVolumeRoot,
                    ref recycleBinInformation);

            if (resultCode != 0)
            {
                result.MeasurementStatus =
                    CleanupMeasurementStatus.NotEvaluable;

                result.Description +=
                    ". Windows konnte für diesen Papierkorb "
                    + "keine aggregierten Größeninformationen "
                    + "bereitstellen.";

                return result;
            }

            result.MeasurementStatus =
                CleanupMeasurementStatus.Measured;

            result.SizeBytes =
                recycleBinInformation.Size >= 0
                    ? (ulong)recycleBinInformation.Size
                    : null;

            result.FileCount =
                recycleBinInformation.ItemCount >= 0
                    ? recycleBinInformation.ItemCount
                    : null;

            result.Description +=
                ". Die Inhalte können noch benötigt werden "
                + "und gelten nicht automatisch als löschbar.";
        }
        catch
        {
            result.MeasurementStatus =
                CleanupMeasurementStatus.NotEvaluable;

            result.Description +=
                ". Die Windows-Papierkorbinformationen "
                + "konnten nicht ausgewertet werden.";
        }

        return result;
    }

    private static bool IsNetworkPath(
        string path)
    {
        return path.StartsWith(
                   @"\\",
                   StringComparison.Ordinal)
               || path.StartsWith(
                   "//",
                   StringComparison.Ordinal);
    }

    [DllImport(
        "shell32.dll",
        CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(
        string rootPath,
        ref RecycleBinInformation recycleBinInformation);

    [StructLayout(LayoutKind.Sequential)]
    private struct RecycleBinInformation
    {
        public int StructureSize;

        public long Size;

        public long ItemCount;
    }
}
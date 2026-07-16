using System.IO;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Cleanup;

internal sealed class WindowsCleanupCategoryProvider
{
    private readonly CleanupDirectoryMeasurer
        _directoryMeasurer;

    public WindowsCleanupCategoryProvider(
        CleanupDirectoryMeasurer directoryMeasurer)
    {
        _directoryMeasurer =
            directoryMeasurer;
    }

    public IReadOnlyCollection<CleanupCategoryResult> Analyze(
        string systemVolumeRoot,
        DateTime deadline)
    {
        var categories =
            new List<CleanupCategoryResult>();

        AddWindowsUpdateDownloadCache(
            categories,
            systemVolumeRoot,
            deadline);

        AddWindowsErrorReports(
            categories,
            systemVolumeRoot,
            deadline);

        AddMemoryDumps(
            categories,
            systemVolumeRoot,
            deadline);

        AddPreviousWindowsInstallation(
            categories,
            systemVolumeRoot,
            deadline);

        return categories;
    }

    private void AddWindowsUpdateDownloadCache(
        ICollection<CleanupCategoryResult> categories,
        string systemVolumeRoot,
        DateTime deadline)
    {
        var windowsDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

        var path =
            string.IsNullOrWhiteSpace(
                windowsDirectory)
                ? string.Empty
                : Path.Combine(
                    windowsDirectory,
                    "SoftwareDistribution",
                    "Download");

        categories.Add(
            _directoryMeasurer.MeasureDirectory(
                CleanupCategoryType.WindowsUpdateDownloadCache,
                CleanupCategoryClassification.ManualReview,
                "Lokal gespeicherte Dateien des "
                + "Windows-Update-Downloadcaches. "
                + "Diese Dateien können für laufende Updates, "
                + "Reparaturen oder erneute Installationsversuche "
                + "benötigt werden",
                path,
                systemVolumeRoot,
                deadline,
                SearchOption.AllDirectories));
    }

    private void AddWindowsErrorReports(
        ICollection<CleanupCategoryResult> categories,
        string systemVolumeRoot,
        DateTime deadline)
    {
        var commonApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData);

        var path =
            string.IsNullOrWhiteSpace(
                commonApplicationData)
                ? string.Empty
                : Path.Combine(
                    commonApplicationData,
                    "Microsoft",
                    "Windows",
                    "WER");

        categories.Add(
            _directoryMeasurer.MeasureDirectory(
                CleanupCategoryType.WindowsErrorReports,
                CleanupCategoryClassification.ManualReview,
                "Gespeicherte Berichte der "
                + "Windows-Fehlerberichterstattung. "
                + "Sie können technische Informationen für "
                + "die spätere Fehlerdiagnose enthalten und "
                + "gelten nicht automatisch als entbehrlich",
                path,
                systemVolumeRoot,
                deadline,
                SearchOption.AllDirectories));
    }

    private void AddMemoryDumps(
        ICollection<CleanupCategoryResult> categories,
        string systemVolumeRoot,
        DateTime deadline)
    {
        var windowsDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

        var miniDumpPath =
            string.IsNullOrWhiteSpace(
                windowsDirectory)
                ? string.Empty
                : Path.Combine(
                    windowsDirectory,
                    "Minidump");

        var result =
            _directoryMeasurer.MeasureDirectory(
                CleanupCategoryType.MemoryDumps,
                CleanupCategoryClassification.ManualReview,
                "Windows-Speicherabbilder und Minidumps. "
                + "Diese Dateien können für die Diagnose "
                + "von Systemabstürzen wichtig sein",
                miniDumpPath,
                systemVolumeRoot,
                deadline,
                SearchOption.AllDirectories);

        AddFullMemoryDump(
            result,
            windowsDirectory,
            systemVolumeRoot,
            deadline);

        categories.Add(result);
    }

    private static void AddPreviousWindowsInstallation(
        ICollection<CleanupCategoryResult> categories,
        string systemVolumeRoot,
        DateTime deadline)
    {
        var result =
            new CleanupCategoryResult
            {
                Category =
                    CleanupCategoryType.PreviousWindowsInstallation,

                Classification =
                    CleanupCategoryClassification.ManualReview,

                Description =
                    "Prüfung auf eine vorherige "
                    + "Windows-Installation"
            };

        if (DateTime.UtcNow >= deadline)
        {
            result.MeasurementStatus =
                CleanupMeasurementStatus.TimedOut;

            result.Description +=
                ". Das Zeitlimit war bereits vor "
                + "der Prüfung erreicht.";

            categories.Add(result);
            return;
        }

        if (string.IsNullOrWhiteSpace(
                systemVolumeRoot)
            || IsNetworkPath(
                systemVolumeRoot))
        {
            result.MeasurementStatus =
                CleanupMeasurementStatus.Excluded;

            result.Description +=
                ". Das lokale Systemvolume konnte "
                + "nicht sicher bestätigt werden.";

            categories.Add(result);
            return;
        }

        try
        {
            var path =
                Path.Combine(
                    systemVolumeRoot,
                    "Windows.old");

            var isPresent =
                Directory.Exists(path);

            result.MeasurementStatus =
                CleanupMeasurementStatus.InformationOnly;

            result.Description =
                isPresent
                    ? "Eine vorherige Windows-Installation "
                      + "wurde erkannt. Sie kann für die Rückkehr "
                      + "zur vorherigen Windows-Version oder für "
                      + "die Wiederherstellung älterer Dateien "
                      + "relevant sein. Die Größe wird bewusst "
                      + "nicht rekursiv ermittelt."
                    : "Es wurde keine vorherige "
                      + "Windows-Installation erkannt.";
        }
        catch
        {
            result.MeasurementStatus =
                CleanupMeasurementStatus.NotEvaluable;

            result.Description +=
                ". Das Vorhandensein konnte nicht "
                + "zuverlässig geprüft werden.";
        }

        categories.Add(result);
    }

    private static void AddFullMemoryDump(
        CleanupCategoryResult result,
        string windowsDirectory,
        string systemVolumeRoot,
        DateTime deadline)
    {
        if (result.MeasurementStatus
                is CleanupMeasurementStatus.Excluded
                or CleanupMeasurementStatus.NotEvaluable
            || string.IsNullOrWhiteSpace(
                windowsDirectory))
        {
            return;
        }

        if (DateTime.UtcNow >= deadline)
        {
            result.MeasurementStatus =
                CleanupMeasurementStatus.TimedOut;

            result.Description +=
                ". Das vollständige Speicherabbild konnte "
                + "wegen des erreichten Zeitlimits nicht "
                + "mehr geprüft werden.";

            return;
        }

        var memoryDumpPath =
            Path.Combine(
                windowsDirectory,
                "MEMORY.DMP");

        if (!IsAllowedLocalPath(
                memoryDumpPath,
                systemVolumeRoot))
        {
            result.MeasurementStatus =
                CleanupMeasurementStatus.PartiallyMeasured;

            result.Description +=
                ". Der Speicherort des vollständigen "
                + "Speicherabbilds konnte nicht sicher "
                + "dem Systemvolume zugeordnet werden.";

            return;
        }

        try
        {
            var fileInformation =
                new FileInfo(
                    memoryDumpPath);

            if (!fileInformation.Exists)
            {
                return;
            }

            if ((fileInformation.Attributes
                 & FileAttributes.ReparsePoint)
                != 0)
            {
                result.MeasurementStatus =
                    CleanupMeasurementStatus.PartiallyMeasured;

                result.Description +=
                    ". Ein Speicherabbild war als "
                    + "Verknüpfung beziehungsweise Reparse Point "
                    + "hinterlegt und wurde nicht ausgewertet.";

                return;
            }

            AddSize(
                result,
                fileInformation.Length);

            result.FileCount =
                (result.FileCount ?? 0)
                + 1;
        }
        catch
        {
            result.MeasurementStatus =
                CleanupMeasurementStatus.PartiallyMeasured;

            result.Description +=
                ". Das vollständige Speicherabbild konnte "
                + "aufgrund eines Zugriffs- oder Dateifehlers "
                + "nicht ausgewertet werden.";
        }
    }

    private static void AddSize(
        CleanupCategoryResult result,
        long fileLength)
    {
        if (fileLength <= 0)
        {
            return;
        }

        var unsignedFileLength =
            (ulong)fileLength;

        var existingSize =
            result.SizeBytes ?? 0;

        if (ulong.MaxValue
            - existingSize
            < unsignedFileLength)
        {
            result.SizeBytes =
                ulong.MaxValue;

            result.MeasurementStatus =
                CleanupMeasurementStatus.PartiallyMeasured;

            result.Description +=
                ". Der Größenwert überschritt den "
                + "darstellbaren Wertebereich.";

            return;
        }

        result.SizeBytes =
            existingSize
            + unsignedFileLength;
    }

    private static bool IsAllowedLocalPath(
        string path,
        string allowedVolumeRoot)
    {
        if (string.IsNullOrWhiteSpace(path)
            || string.IsNullOrWhiteSpace(
                allowedVolumeRoot)
            || IsNetworkPath(path))
        {
            return false;
        }

        try
        {
            var fullPath =
                Path.GetFullPath(path);

            var pathRoot =
                Path.GetPathRoot(fullPath);

            if (string.IsNullOrWhiteSpace(
                    pathRoot))
            {
                return false;
            }

            return string.Equals(
                NormalizeRootPath(pathRoot),
                NormalizeRootPath(
                    allowedVolumeRoot),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
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

    private static string NormalizeRootPath(
        string path)
    {
        try
        {
            var fullPath =
                Path.GetFullPath(path);

            var rootPath =
                Path.GetPathRoot(fullPath);

            if (string.IsNullOrWhiteSpace(
                    rootPath))
            {
                return string.Empty;
            }

            return rootPath
                .Replace(
                    Path.AltDirectorySeparatorChar,
                    Path.DirectorySeparatorChar)
                .ToUpperInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }
}
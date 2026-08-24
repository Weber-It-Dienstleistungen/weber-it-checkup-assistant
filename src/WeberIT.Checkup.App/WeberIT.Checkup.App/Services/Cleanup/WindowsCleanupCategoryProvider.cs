using System.IO;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Cleanup;

internal sealed class WindowsCleanupCategoryProvider
{
    private static readonly TimeSpan RollbackProbeTimeLimit =
        TimeSpan.FromSeconds(20);

    private readonly CleanupDirectoryMeasurer
        _directoryMeasurer;

    private readonly WindowsRollbackAvailabilityProbe
        _rollbackAvailabilityProbe;

    public WindowsCleanupCategoryProvider(
        CleanupDirectoryMeasurer directoryMeasurer,
        WindowsRollbackAvailabilityProbe
            rollbackAvailabilityProbe)
    {
        _directoryMeasurer =
            directoryMeasurer;

        _rollbackAvailabilityProbe =
            rollbackAvailabilityProbe;
    }

    public IReadOnlyCollection<CleanupCategoryResult> Analyze(
        string systemVolumeRoot,
        TimeSpan categoryTimeLimit)
    {
        var categories =
            new List<CleanupCategoryResult>();

        AddWindowsUpdateDownloadCache(
            categories,
            systemVolumeRoot,
            categoryTimeLimit);

        AddWindowsErrorReports(
            categories,
            systemVolumeRoot,
            categoryTimeLimit);

        AddMemoryDumps(
            categories,
            systemVolumeRoot,
            categoryTimeLimit);

        AddPreviousWindowsInstallation(
            categories,
            systemVolumeRoot,
            categoryTimeLimit);

        return categories;
    }

    private void AddWindowsUpdateDownloadCache(
        ICollection<CleanupCategoryResult> categories,
        string systemVolumeRoot,
        TimeSpan categoryTimeLimit)
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
                CreateDeadline(
                    categoryTimeLimit),
                SearchOption.AllDirectories));
    }

    private void AddWindowsErrorReports(
        ICollection<CleanupCategoryResult> categories,
        string systemVolumeRoot,
        TimeSpan categoryTimeLimit)
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
                CreateDeadline(
                    categoryTimeLimit),
                SearchOption.AllDirectories));
    }

    private void AddMemoryDumps(
        ICollection<CleanupCategoryResult> categories,
        string systemVolumeRoot,
        TimeSpan categoryTimeLimit)
    {
        var deadline =
            CreateDeadline(
                categoryTimeLimit);

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

    private void AddPreviousWindowsInstallation(
        ICollection<CleanupCategoryResult> categories,
        string systemVolumeRoot,
        TimeSpan categoryTimeLimit)
    {
        if (string.IsNullOrWhiteSpace(
                systemVolumeRoot)
            || IsNetworkPath(
                systemVolumeRoot))
        {
            categories.Add(
                new CleanupCategoryResult
                {
                    Category =
                        CleanupCategoryType.PreviousWindowsInstallation,

                    Classification =
                        CleanupCategoryClassification.Excluded,

                    MeasurementStatus =
                        CleanupMeasurementStatus.Excluded,

                    Description =
                        "Das lokale Systemvolume konnte für die "
                        + "Prüfung auf eine vorherige "
                        + "Windows-Installation nicht sicher "
                        + "bestätigt werden."
                });

            return;
        }

        string path;

        try
        {
            path =
                Path.Combine(
                    systemVolumeRoot,
                    "Windows.old");
        }
        catch
        {
            categories.Add(
                new CleanupCategoryResult
                {
                    Category =
                        CleanupCategoryType.PreviousWindowsInstallation,

                    Classification =
                        CleanupCategoryClassification.Information,

                    MeasurementStatus =
                        CleanupMeasurementStatus.NotEvaluable,

                    Description =
                        "Der Speicherort einer vorherigen "
                        + "Windows-Installation konnte nicht "
                        + "zuverlässig bestimmt werden."
                });

            return;
        }

        if (!Directory.Exists(
                path))
        {
            categories.Add(
                new CleanupCategoryResult
                {
                    Category =
                        CleanupCategoryType.PreviousWindowsInstallation,

                    Classification =
                        CleanupCategoryClassification.Information,

                    MeasurementStatus =
                        CleanupMeasurementStatus.InformationOnly,

                    Description =
                        "Es wurde keine vorherige "
                        + "Windows-Installation erkannt."
                });

            return;
        }

        var rollbackResult =
            _rollbackAvailabilityProbe.Analyze(
                CreateDeadline(
                    RollbackProbeTimeLimit));

        var classification =
            rollbackResult.Status
                == WindowsRollbackAvailabilityStatus.Available
                ? CleanupCategoryClassification.Information
                : CleanupCategoryClassification.ManualReview;

        var description =
            BuildPreviousWindowsInstallationDescription(
                rollbackResult);

        var measurement =
            _directoryMeasurer.MeasureDirectory(
                CleanupCategoryType.PreviousWindowsInstallation,
                classification,
                description,
                path,
                systemVolumeRoot,
                CreateDeadline(
                    categoryTimeLimit),
                SearchOption.AllDirectories);

        categories.Add(
            measurement);
    }

    private static string
        BuildPreviousWindowsInstallationDescription(
            WindowsRollbackAvailabilityResult rollbackResult)
    {
        switch (rollbackResult.Status)
        {
            case WindowsRollbackAvailabilityStatus.Available:
                {
                    var windowDescription =
                        rollbackResult.UninstallWindowDays.HasValue
                            ? $" Das konfigurierte Rückkehrfenster "
                              + $"beträgt "
                              + $"{rollbackResult.UninstallWindowDays.Value} "
                              + "Tage. Dieser Wert beschreibt die "
                              + "konfigurierte Frist und nicht die "
                              + "verbleibende Restzeit."
                            : string.Empty;

                    return
                        "Eine vorherige Windows-Installation wurde "
                        + "erkannt. DISM meldet die Windows-"
                        + "Rückkehrfunktion als verfügbar."
                        + windowDescription
                        + " Der Speicherbereich wird zur "
                        + "Dokumentation vermessen, aber nicht als "
                        + "Bereinigungspotenzial eingestuft. "
                        + "Solange die Rückkehrfunktion verfügbar "
                        + "ist, sollte Windows.old nicht entfernt werden";
                }

            case WindowsRollbackAvailabilityStatus.Unavailable:
                return
                    "Eine vorherige Windows-Installation wurde "
                    + "erkannt. DISM meldet keine verfügbare "
                    + "Windows-Rückkehrfunktion mehr. "
                    + "Windows.old wird deshalb als manuell zu "
                    + "prüfender Altbestand vermessen. Vor einer "
                    + "Bereinigung muss geprüft werden, ob daraus "
                    + "noch persönliche oder technische Dateien "
                    + "benötigt werden";

            case WindowsRollbackAvailabilityStatus.TimedOut:
                return
                    "Eine vorherige Windows-Installation wurde "
                    + "erkannt. Die automatisierte Prüfung der "
                    + "Windows-Rückkehrfunktion hat ihr eigenes "
                    + "Sicherheitszeitlimit erreicht. "
                    + "Windows.old wird trotzdem zur Dokumentation "
                    + "vermessen, darf aber ohne manuelle Prüfung "
                    + "nicht als entbehrlich eingestuft werden";

            default:
                {
                    var detail =
                        string.IsNullOrWhiteSpace(
                            rollbackResult.Message)
                            ? "Die Ursache ist nicht näher bekannt."
                            : rollbackResult.Message;

                    return
                        "Eine vorherige Windows-Installation wurde "
                        + "erkannt. Die Windows-Rückkehrfunktion konnte "
                        + "nicht zuverlässig automatisch geprüft werden. "
                        + detail
                        + " Windows.old wird trotzdem zur Dokumentation "
                        + "vermessen, darf aber ohne manuelle Prüfung "
                        + "nicht als entbehrlich eingestuft werden";
                }
        }
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

    private static DateTime CreateDeadline(
        TimeSpan timeLimit)
    {
        return DateTime.UtcNow.Add(
            timeLimit);
    }
}
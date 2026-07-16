using System.Diagnostics;
using System.IO;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Cleanup;

public class CleanupPotentialProvider :
    ICleanupPotentialProvider
{
    private static readonly TimeSpan AnalysisTimeLimit =
        TimeSpan.FromSeconds(10);

    public CleanupPotentialInformation Analyze(
        StorageInformation storageInformation)
    {
        var stopwatch =
            Stopwatch.StartNew();

        var information =
            new CleanupPotentialInformation
            {
                AnalysisDate =
                    DateTime.Now,

                ScopeDescription =
                    "Windows-Systemvolume und Cachebereiche "
                    + "des aktuell angemeldeten Benutzers"
            };

        try
        {
            var systemVolume =
                FindSystemVolume(
                    storageInformation);

            if (systemVolume is null)
            {
                information.AnalysisStatus =
                    CleanupMeasurementStatus.NotEvaluable;

                information.AnalysisMessage =
                    "Das Windows-Systemvolume konnte nicht "
                    + "eindeutig bestimmt werden.";

                return information;
            }

            information.TargetVolume =
                systemVolume.DriveLetter;

            if (!TryBuildVolumeRoot(
                    systemVolume,
                    out var systemVolumeRoot))
            {
                information.AnalysisStatus =
                    CleanupMeasurementStatus.NotEvaluable;

                information.AnalysisMessage =
                    "Der Pfad des Windows-Systemvolumes "
                    + "konnte nicht sicher bestimmt werden.";

                return information;
            }

            var deadline =
                DateTime.UtcNow
                    .Add(AnalysisTimeLimit);

            AddUserTemporaryFiles(
                information,
                systemVolumeRoot,
                deadline);

            AddWindowsTemporaryFiles(
                information,
                systemVolumeRoot,
                deadline);

            AddDirectXShaderCache(
                information,
                systemVolumeRoot,
                deadline);

            AddThumbnailCache(
                information,
                systemVolumeRoot,
                deadline);

            information.AnalysisStatus =
                DetermineOverallStatus(
                    information.Categories);

            information.AnalysisMessage =
                BuildAnalysisMessage(
                    information.AnalysisStatus,
                    information.Categories);
        }
        catch
        {
            information.AnalysisStatus =
                CleanupMeasurementStatus.NotEvaluable;

            information.AnalysisMessage =
                "Die Analyse des Bereinigungspotenzials "
                + "konnte nicht abgeschlossen werden.";
        }
        finally
        {
            stopwatch.Stop();

            information.AnalysisDurationMilliseconds =
                stopwatch.ElapsedMilliseconds;
        }

        return information;
    }

    private static VolumeInformation? FindSystemVolume(
        StorageInformation storageInformation)
    {
        return storageInformation
            .Volumes
            .FirstOrDefault(
                volume =>
                    volume.IsSystemVolume
                    && volume.IsReady);
    }

    private static bool TryBuildVolumeRoot(
        VolumeInformation systemVolume,
        out string systemVolumeRoot)
    {
        systemVolumeRoot =
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                systemVolume.DriveLetter))
        {
            return false;
        }

        try
        {
            var candidateRoot =
                Path.GetPathRoot(
                    systemVolume.DriveLetter
                    + Path.DirectorySeparatorChar);

            if (string.IsNullOrWhiteSpace(
                    candidateRoot)
                || IsNetworkPath(candidateRoot))
            {
                return false;
            }

            systemVolumeRoot =
                NormalizeRootPath(
                    candidateRoot);

            return !string.IsNullOrWhiteSpace(
                systemVolumeRoot);
        }
        catch
        {
            return false;
        }
    }

    private static void AddUserTemporaryFiles(
        CleanupPotentialInformation information,
        string systemVolumeRoot,
        DateTime deadline)
    {
        var path =
            Path.GetTempPath();

        information.Categories.Add(
            MeasureDirectory(
                CleanupCategoryType.UserTemporaryFiles,
                CleanupCategoryClassification.SafePotential,
                "Temporäre Dateien des aktuell "
                + "angemeldeten Benutzers",
                path,
                systemVolumeRoot,
                deadline,
                SearchOption.AllDirectories));
    }

    private static void AddWindowsTemporaryFiles(
        CleanupPotentialInformation information,
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
                    "Temp");

        information.Categories.Add(
            MeasureDirectory(
                CleanupCategoryType.WindowsTemporaryFiles,
                CleanupCategoryClassification.SafePotential,
                "Temporäre Dateien von Windows",
                path,
                systemVolumeRoot,
                deadline,
                SearchOption.AllDirectories));
    }

    private static void AddDirectXShaderCache(
        CleanupPotentialInformation information,
        string systemVolumeRoot,
        DateTime deadline)
    {
        var localApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        var path =
            string.IsNullOrWhiteSpace(
                localApplicationData)
                ? string.Empty
                : Path.Combine(
                    localApplicationData,
                    "D3DSCache");

        information.Categories.Add(
            MeasureDirectory(
                CleanupCategoryType.DirectXShaderCache,
                CleanupCategoryClassification.SafePotential,
                "DirectX-Shadercache",
                path,
                systemVolumeRoot,
                deadline,
                SearchOption.AllDirectories));
    }

    private static void AddThumbnailCache(
        CleanupPotentialInformation information,
        string systemVolumeRoot,
        DateTime deadline)
    {
        var localApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        var path =
            string.IsNullOrWhiteSpace(
                localApplicationData)
                ? string.Empty
                : Path.Combine(
                    localApplicationData,
                    "Microsoft",
                    "Windows",
                    "Explorer");

        information.Categories.Add(
            MeasureDirectory(
                CleanupCategoryType.ThumbnailCache,
                CleanupCategoryClassification.SafePotential,
                "Windows-Vorschaubildcache",
                path,
                systemVolumeRoot,
                deadline,
                SearchOption.TopDirectoryOnly,
                "thumbcache_*.db"));
    }

    private static CleanupCategoryResult MeasureDirectory(
        CleanupCategoryType category,
        CleanupCategoryClassification classification,
        string description,
        string path,
        string allowedVolumeRoot,
        DateTime deadline,
        SearchOption searchOption,
        string filePattern = "*")
    {
        var result =
            new CleanupCategoryResult
            {
                Category =
                    category,

                Classification =
                    classification,

                Description =
                    description
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

        if (!IsAllowedLocalPath(
                path,
                allowedVolumeRoot))
        {
            result.MeasurementStatus =
                CleanupMeasurementStatus.Excluded;

            result.Description +=
                ". Der Speicherort liegt nicht sicher "
                + "auf dem untersuchten Systemvolume.";

            return result;
        }

        if (!Directory.Exists(path))
        {
            result.MeasurementStatus =
                CleanupMeasurementStatus.Measured;

            result.SizeBytes =
                0;

            result.FileCount =
                0;

            result.Description +=
                ". Der Cache- beziehungsweise "
                + "Temporärordner ist nicht vorhanden.";

            return result;
        }

        var measurement =
            MeasureFiles(
                path,
                searchOption,
                filePattern,
                deadline);

        result.SizeBytes =
            measurement.SizeBytes;

        result.FileCount =
            measurement.FileCount;

        if (measurement.WasTimedOut)
        {
            result.MeasurementStatus =
                CleanupMeasurementStatus.TimedOut;

            result.Description +=
                ". Die bis zum Zeitlimit ermittelten "
                + "Werte sind unvollständig.";

            return result;
        }

        if (measurement.HadAccessErrors)
        {
            result.MeasurementStatus =
                CleanupMeasurementStatus.PartiallyMeasured;

            result.Description +=
                ". Nicht alle Bereiche konnten aufgrund "
                + "von Zugriffs- oder Dateifehlern "
                + "ausgewertet werden.";

            return result;
        }

        result.MeasurementStatus =
            CleanupMeasurementStatus.Measured;

        return result;
    }

    private static DirectoryMeasurement MeasureFiles(
        string rootPath,
        SearchOption searchOption,
        string filePattern,
        DateTime deadline)
    {
        var measurement =
            new DirectoryMeasurement();

        var pendingDirectories =
            new Stack<string>();

        pendingDirectories.Push(
            rootPath);

        while (pendingDirectories.Count > 0)
        {
            if (DateTime.UtcNow >= deadline)
            {
                measurement.WasTimedOut =
                    true;

                break;
            }

            var currentDirectory =
                pendingDirectories.Pop();

            if (IsReparsePoint(
                    currentDirectory,
                    measurement))
            {
                continue;
            }

            MeasureFilesInDirectory(
                currentDirectory,
                filePattern,
                deadline,
                measurement);

            if (measurement.WasTimedOut
                || searchOption
                    == SearchOption.TopDirectoryOnly)
            {
                continue;
            }

            AddChildDirectories(
                currentDirectory,
                pendingDirectories,
                deadline,
                measurement);
        }

        return measurement;
    }

    private static void MeasureFilesInDirectory(
        string directory,
        string filePattern,
        DateTime deadline,
        DirectoryMeasurement measurement)
    {
        IEnumerable<string> files;

        try
        {
            files =
                Directory.EnumerateFiles(
                    directory,
                    filePattern,
                    SearchOption.TopDirectoryOnly);
        }
        catch
        {
            measurement.HadAccessErrors =
                true;

            return;
        }

        try
        {
            foreach (var file in files)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    measurement.WasTimedOut =
                        true;

                    return;
                }

                try
                {
                    var fileInfo =
                        new FileInfo(file);

                    if ((fileInfo.Attributes
                         & FileAttributes.ReparsePoint)
                        != 0)
                    {
                        continue;
                    }

                    AddFileLength(
                        measurement,
                        fileInfo.Length);

                    measurement.FileCount++;
                }
                catch
                {
                    measurement.HadAccessErrors =
                        true;
                }
            }
        }
        catch
        {
            measurement.HadAccessErrors =
                true;
        }
    }

    private static void AddChildDirectories(
        string directory,
        Stack<string> pendingDirectories,
        DateTime deadline,
        DirectoryMeasurement measurement)
    {
        IEnumerable<string> directories;

        try
        {
            directories =
                Directory.EnumerateDirectories(
                    directory,
                    "*",
                    SearchOption.TopDirectoryOnly);
        }
        catch
        {
            measurement.HadAccessErrors =
                true;

            return;
        }

        try
        {
            foreach (var childDirectory in directories)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    measurement.WasTimedOut =
                        true;

                    return;
                }

                if (IsReparsePoint(
                        childDirectory,
                        measurement))
                {
                    continue;
                }

                pendingDirectories.Push(
                    childDirectory);
            }
        }
        catch
        {
            measurement.HadAccessErrors =
                true;
        }
    }

    private static bool IsReparsePoint(
        string path,
        DirectoryMeasurement measurement)
    {
        try
        {
            var attributes =
                File.GetAttributes(path);

            return (attributes
                    & FileAttributes.ReparsePoint)
                   != 0;
        }
        catch
        {
            measurement.HadAccessErrors =
                true;

            return true;
        }
    }

    private static void AddFileLength(
        DirectoryMeasurement measurement,
        long fileLength)
    {
        if (fileLength <= 0)
        {
            return;
        }

        var unsignedFileLength =
            (ulong)fileLength;

        if (ulong.MaxValue
            - measurement.SizeBytes
            < unsignedFileLength)
        {
            measurement.SizeBytes =
                ulong.MaxValue;

            measurement.HadAccessErrors =
                true;

            return;
        }

        measurement.SizeBytes +=
            unsignedFileLength;
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

    private static CleanupMeasurementStatus DetermineOverallStatus(
        IReadOnlyCollection<CleanupCategoryResult> categories)
    {
        if (categories.Count == 0)
        {
            return CleanupMeasurementStatus.NotEvaluable;
        }

        if (categories.All(
                category =>
                    category.MeasurementStatus
                        == CleanupMeasurementStatus.Measured))
        {
            return CleanupMeasurementStatus.Measured;
        }

        if (categories.Any(
                category =>
                    category.MeasurementStatus
                        == CleanupMeasurementStatus.TimedOut))
        {
            return CleanupMeasurementStatus.TimedOut;
        }

        if (categories.Any(
                category =>
                    category.MeasurementStatus
                        is CleanupMeasurementStatus.Measured
                        or CleanupMeasurementStatus.PartiallyMeasured))
        {
            return CleanupMeasurementStatus.PartiallyMeasured;
        }

        return CleanupMeasurementStatus.NotEvaluable;
    }

    private static string BuildAnalysisMessage(
        CleanupMeasurementStatus status,
        IReadOnlyCollection<CleanupCategoryResult> categories)
    {
        return status switch
        {
            CleanupMeasurementStatus.Measured =>
                "Die ausgewählten temporären Dateien "
                + "und Cachebereiche wurden vollständig "
                + "und rein lesend ausgewertet.",

            CleanupMeasurementStatus.PartiallyMeasured =>
                "Die Bereinigungsanalyse wurde abgeschlossen. "
                + "Mindestens eine Kategorie konnte nur "
                + "teilweise ausgewertet werden.",

            CleanupMeasurementStatus.TimedOut =>
                "Das Zeitlimit der Bereinigungsanalyse "
                + "wurde erreicht. Bereits ermittelte Werte "
                + "bleiben als unvollständige Messung erhalten.",

            _ when categories.All(
                category =>
                    category.MeasurementStatus
                        == CleanupMeasurementStatus.Excluded) =>
                "Alle vorgesehenen Kategorien lagen außerhalb "
                + "des sicher untersuchbaren Systemvolumes.",

            _ =>
                "Die vorgesehenen Kategorien konnten "
                + "nicht zuverlässig ausgewertet werden."
        };
    }

    private sealed class DirectoryMeasurement
    {
        public ulong SizeBytes { get; set; }

        public long FileCount { get; set; }

        public bool HadAccessErrors { get; set; }

        public bool WasTimedOut { get; set; }
    }
}
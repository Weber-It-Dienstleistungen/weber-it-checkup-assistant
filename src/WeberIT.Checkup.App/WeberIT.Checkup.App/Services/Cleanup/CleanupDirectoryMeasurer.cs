using System.IO;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Cleanup;

internal sealed class CleanupDirectoryMeasurer
{
    public CleanupCategoryResult MeasureDirectory(
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
                    var fileInformation =
                        new FileInfo(file);

                    if ((fileInformation.Attributes
                         & FileAttributes.ReparsePoint)
                        != 0)
                    {
                        continue;
                    }

                    AddFileLength(
                        measurement,
                        fileInformation.Length);

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

    private sealed class DirectoryMeasurement
    {
        public ulong SizeBytes { get; set; }

        public long FileCount { get; set; }

        public bool HadAccessErrors { get; set; }

        public bool WasTimedOut { get; set; }
    }
}
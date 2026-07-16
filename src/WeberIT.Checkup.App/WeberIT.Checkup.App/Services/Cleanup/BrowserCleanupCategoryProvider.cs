using System.IO;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Cleanup;

internal sealed class BrowserCleanupCategoryProvider
{
    private readonly CleanupDirectoryMeasurer
        _directoryMeasurer;

    public BrowserCleanupCategoryProvider(
        CleanupDirectoryMeasurer directoryMeasurer)
    {
        _directoryMeasurer =
            directoryMeasurer;
    }

    public CleanupCategoryResult Analyze(
        string systemVolumeRoot,
        DateTime deadline)
    {
        var result =
            new CleanupCategoryResult
            {
                Category =
                    CleanupCategoryType.BrowserCache,

                Classification =
                    CleanupCategoryClassification.SafePotential,

                Description =
                    "Bekannte Cachebereiche von Microsoft Edge, "
                    + "Google Chrome und Mozilla Firefox. "
                    + "Verlauf, Cookies, Anmeldedaten und sonstige "
                    + "Browserinhalte wurden nicht untersucht"
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

        var localApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(
                localApplicationData))
        {
            result.MeasurementStatus =
                CleanupMeasurementStatus.NotEvaluable;

            result.Description +=
                ". Der lokale Benutzerordner konnte "
                + "nicht bestimmt werden.";

            return result;
        }

        var cachePaths =
            new List<string>();

        var discoveryHadErrors =
            false;

        AddChromiumCachePaths(
            Path.Combine(
                localApplicationData,
                "Microsoft",
                "Edge",
                "User Data"),
            cachePaths,
            deadline,
            ref discoveryHadErrors);

        AddChromiumCachePaths(
            Path.Combine(
                localApplicationData,
                "Google",
                "Chrome",
                "User Data"),
            cachePaths,
            deadline,
            ref discoveryHadErrors);

        AddFirefoxCachePaths(
            Path.Combine(
                localApplicationData,
                "Mozilla",
                "Firefox",
                "Profiles"),
            cachePaths,
            deadline,
            ref discoveryHadErrors);

        if (DateTime.UtcNow >= deadline)
        {
            result.MeasurementStatus =
                CleanupMeasurementStatus.TimedOut;

            result.SizeBytes =
                0;

            result.FileCount =
                0;

            result.Description +=
                ". Das Zeitlimit wurde bereits während "
                + "der Ermittlung vorhandener Cachebereiche erreicht.";

            return result;
        }

        if (cachePaths.Count == 0)
        {
            result.MeasurementStatus =
                discoveryHadErrors
                    ? CleanupMeasurementStatus.PartiallyMeasured
                    : CleanupMeasurementStatus.Measured;

            result.SizeBytes =
                0;

            result.FileCount =
                0;

            result.Description +=
                discoveryHadErrors
                    ? ". Vorhandene Browsercachebereiche konnten "
                      + "nicht vollständig ermittelt werden."
                    : ". Es wurden keine unterstützten "
                      + "Browsercachebereiche gefunden.";

            return result;
        }

        foreach (var cachePath in cachePaths.Distinct(
                     StringComparer.OrdinalIgnoreCase))
        {
            var categoryResult =
                _directoryMeasurer.MeasureDirectory(
                    CleanupCategoryType.BrowserCache,
                    CleanupCategoryClassification.SafePotential,
                    "Browsercache",
                    cachePath,
                    systemVolumeRoot,
                    deadline,
                    SearchOption.AllDirectories);

            MergeResult(
                result,
                categoryResult);

            if (categoryResult.MeasurementStatus
                    == CleanupMeasurementStatus.TimedOut)
            {
                break;
            }
        }

        if (discoveryHadErrors
            && result.MeasurementStatus
                == CleanupMeasurementStatus.Measured)
        {
            result.MeasurementStatus =
                CleanupMeasurementStatus.PartiallyMeasured;
        }

        AddStatusDescription(
            result);

        return result;
    }

    private static void AddChromiumCachePaths(
        string userDataPath,
        ICollection<string> cachePaths,
        DateTime deadline,
        ref bool discoveryHadErrors)
    {
        if (DateTime.UtcNow >= deadline
            || !Directory.Exists(
                userDataPath))
        {
            return;
        }

        AddKnownChromiumSharedCaches(
            userDataPath,
            cachePaths);

        IEnumerable<string> profileDirectories;

        try
        {
            profileDirectories =
                Directory.EnumerateDirectories(
                    userDataPath,
                    "*",
                    SearchOption.TopDirectoryOnly);
        }
        catch
        {
            discoveryHadErrors =
                true;

            return;
        }

        try
        {
            foreach (var profileDirectory
                     in profileDirectories)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    return;
                }

                if (IsReparsePoint(
                        profileDirectory))
                {
                    continue;
                }

                var profileName =
                    Path.GetFileName(
                        profileDirectory);

                if (!IsChromiumProfileName(
                        profileName))
                {
                    continue;
                }

                cachePaths.Add(
                    Path.Combine(
                        profileDirectory,
                        "Cache"));

                cachePaths.Add(
                    Path.Combine(
                        profileDirectory,
                        "Code Cache"));

                cachePaths.Add(
                    Path.Combine(
                        profileDirectory,
                        "GPUCache"));
            }
        }
        catch
        {
            discoveryHadErrors =
                true;
        }
    }

    private static void AddKnownChromiumSharedCaches(
        string userDataPath,
        ICollection<string> cachePaths)
    {
        cachePaths.Add(
            Path.Combine(
                userDataPath,
                "GrShaderCache"));

        cachePaths.Add(
            Path.Combine(
                userDataPath,
                "ShaderCache"));
    }

    private static void AddFirefoxCachePaths(
        string profilesPath,
        ICollection<string> cachePaths,
        DateTime deadline,
        ref bool discoveryHadErrors)
    {
        if (DateTime.UtcNow >= deadline
            || !Directory.Exists(
                profilesPath))
        {
            return;
        }

        IEnumerable<string> profileDirectories;

        try
        {
            profileDirectories =
                Directory.EnumerateDirectories(
                    profilesPath,
                    "*",
                    SearchOption.TopDirectoryOnly);
        }
        catch
        {
            discoveryHadErrors =
                true;

            return;
        }

        try
        {
            foreach (var profileDirectory
                     in profileDirectories)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    return;
                }

                if (IsReparsePoint(
                        profileDirectory))
                {
                    continue;
                }

                cachePaths.Add(
                    Path.Combine(
                        profileDirectory,
                        "cache2"));

                cachePaths.Add(
                    Path.Combine(
                        profileDirectory,
                        "startupCache"));
            }
        }
        catch
        {
            discoveryHadErrors =
                true;
        }
    }

    private static bool IsChromiumProfileName(
        string profileName)
    {
        return string.Equals(
                   profileName,
                   "Default",
                   StringComparison.OrdinalIgnoreCase)
               || profileName.StartsWith(
                   "Profile ",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReparsePoint(
        string path)
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
            return true;
        }
    }

    private static void MergeResult(
        CleanupCategoryResult target,
        CleanupCategoryResult source)
    {
        AddSize(
            target,
            source.SizeBytes);

        AddFileCount(
            target,
            source.FileCount);

        target.MeasurementStatus =
            CombineStatus(
                target.MeasurementStatus,
                source.MeasurementStatus);
    }

    private static void AddSize(
        CleanupCategoryResult result,
        ulong? sizeBytes)
    {
        if (!sizeBytes.HasValue)
        {
            return;
        }

        var existingSize =
            result.SizeBytes ?? 0;

        if (ulong.MaxValue
            - existingSize
            < sizeBytes.Value)
        {
            result.SizeBytes =
                ulong.MaxValue;

            result.MeasurementStatus =
                CleanupMeasurementStatus.PartiallyMeasured;

            return;
        }

        result.SizeBytes =
            existingSize
            + sizeBytes.Value;
    }

    private static void AddFileCount(
        CleanupCategoryResult result,
        long? fileCount)
    {
        if (!fileCount.HasValue)
        {
            return;
        }

        var existingCount =
            result.FileCount ?? 0;

        if (long.MaxValue
            - existingCount
            < fileCount.Value)
        {
            result.FileCount =
                long.MaxValue;

            result.MeasurementStatus =
                CleanupMeasurementStatus.PartiallyMeasured;

            return;
        }

        result.FileCount =
            existingCount
            + fileCount.Value;
    }

    private static CleanupMeasurementStatus CombineStatus(
        CleanupMeasurementStatus currentStatus,
        CleanupMeasurementStatus newStatus)
    {
        if (currentStatus
                == CleanupMeasurementStatus.TimedOut
            || newStatus
                == CleanupMeasurementStatus.TimedOut)
        {
            return CleanupMeasurementStatus.TimedOut;
        }

        if (currentStatus
                is CleanupMeasurementStatus.PartiallyMeasured
                or CleanupMeasurementStatus.NotEvaluable
                or CleanupMeasurementStatus.Excluded
            || newStatus
                is CleanupMeasurementStatus.PartiallyMeasured
                or CleanupMeasurementStatus.NotEvaluable
                or CleanupMeasurementStatus.Excluded)
        {
            return CleanupMeasurementStatus.PartiallyMeasured;
        }

        return CleanupMeasurementStatus.Measured;
    }

    private static void AddStatusDescription(
        CleanupCategoryResult result)
    {
        result.Description +=
            result.MeasurementStatus switch
            {
                CleanupMeasurementStatus.Measured =>
                    ". Die erkannten Cachebereiche wurden "
                    + "vollständig aggregiert.",

                CleanupMeasurementStatus.PartiallyMeasured =>
                    ". Mindestens ein Cachebereich konnte "
                    + "nicht vollständig ausgewertet werden.",

                CleanupMeasurementStatus.TimedOut =>
                    ". Die bis zum Zeitlimit ermittelten "
                    + "Werte sind unvollständig.",

                _ =>
                    ". Die Cachebereiche konnten nicht "
                    + "zuverlässig ausgewertet werden."
            };
    }
}
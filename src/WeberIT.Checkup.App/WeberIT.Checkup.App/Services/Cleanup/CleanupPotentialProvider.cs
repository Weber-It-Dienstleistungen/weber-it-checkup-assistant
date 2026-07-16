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

    private readonly SafeCleanupCategoryProvider
        _safeCategoryProvider;

    private readonly RecycleBinCleanupCategoryProvider
        _recycleBinCategoryProvider;

    private readonly WindowsCleanupCategoryProvider
        _windowsCategoryProvider;

    private readonly BrowserCleanupCategoryProvider
        _browserCategoryProvider;

    public CleanupPotentialProvider()
    {
        var directoryMeasurer =
            new CleanupDirectoryMeasurer();

        _safeCategoryProvider =
            new SafeCleanupCategoryProvider(
                directoryMeasurer);

        _recycleBinCategoryProvider =
            new RecycleBinCleanupCategoryProvider();

        _windowsCategoryProvider =
            new WindowsCleanupCategoryProvider(
                directoryMeasurer);

        _browserCategoryProvider =
            new BrowserCleanupCategoryProvider(
                directoryMeasurer);
    }

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

            information.Categories.AddRange(
                _safeCategoryProvider.Analyze(
                    systemVolumeRoot,
                    deadline));

            information.Categories.Add(
                _recycleBinCategoryProvider.Analyze(
                    systemVolumeRoot,
                    deadline));

            information.Categories.Add(
                _browserCategoryProvider.Analyze(
                    systemVolumeRoot,
                    deadline));

            information.Categories.AddRange(
                _windowsCategoryProvider.Analyze(
                    systemVolumeRoot,
                    deadline));

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
                        is CleanupMeasurementStatus.Measured
                        or CleanupMeasurementStatus.InformationOnly))
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
                        or CleanupMeasurementStatus.PartiallyMeasured
                        or CleanupMeasurementStatus.InformationOnly))
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
                "Die vorgesehenen Größenmessungen und "
                + "Informationsprüfungen wurden vollständig "
                + "und rein lesend ausgeführt.",

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
}
using System.IO;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Cleanup;

internal sealed class SafeCleanupCategoryProvider
{
    private readonly CleanupDirectoryMeasurer
        _directoryMeasurer;

    public SafeCleanupCategoryProvider(
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

        AddUserTemporaryFiles(
            categories,
            systemVolumeRoot,
            deadline);

        AddWindowsTemporaryFiles(
            categories,
            systemVolumeRoot,
            deadline);

        AddDirectXShaderCache(
            categories,
            systemVolumeRoot,
            deadline);

        AddThumbnailCache(
            categories,
            systemVolumeRoot,
            deadline);

        return categories;
    }

    private void AddUserTemporaryFiles(
        ICollection<CleanupCategoryResult> categories,
        string systemVolumeRoot,
        DateTime deadline)
    {
        var path =
            Path.GetTempPath();

        categories.Add(
            _directoryMeasurer.MeasureDirectory(
                CleanupCategoryType.UserTemporaryFiles,
                CleanupCategoryClassification.SafePotential,
                "Temporäre Dateien des aktuell "
                + "angemeldeten Benutzers",
                path,
                systemVolumeRoot,
                deadline,
                SearchOption.AllDirectories));
    }

    private void AddWindowsTemporaryFiles(
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
                    "Temp");

        categories.Add(
            _directoryMeasurer.MeasureDirectory(
                CleanupCategoryType.WindowsTemporaryFiles,
                CleanupCategoryClassification.SafePotential,
                "Temporäre Dateien von Windows",
                path,
                systemVolumeRoot,
                deadline,
                SearchOption.AllDirectories));
    }

    private void AddDirectXShaderCache(
        ICollection<CleanupCategoryResult> categories,
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

        categories.Add(
            _directoryMeasurer.MeasureDirectory(
                CleanupCategoryType.DirectXShaderCache,
                CleanupCategoryClassification.SafePotential,
                "DirectX-Shadercache",
                path,
                systemVolumeRoot,
                deadline,
                SearchOption.AllDirectories));
    }

    private void AddThumbnailCache(
        ICollection<CleanupCategoryResult> categories,
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

        categories.Add(
            _directoryMeasurer.MeasureDirectory(
                CleanupCategoryType.ThumbnailCache,
                CleanupCategoryClassification.SafePotential,
                "Windows-Vorschaubildcache",
                path,
                systemVolumeRoot,
                deadline,
                SearchOption.TopDirectoryOnly,
                "thumbcache_*.db"));
    }
}
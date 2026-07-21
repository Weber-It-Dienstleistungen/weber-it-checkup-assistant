using System.IO;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Tasks;

public sealed class CleanupActionExecutor :
    ICleanupActionExecutor
{
    private const string RestoreCapacityTaskCode =
        "task.storage.restore-system-volume-capacity";

    private const string ControlledCleanupTaskCode =
        "task.storage.controlled-cleanup";

    private const string SupportedActionCode =
        "action.cleanup.selected-safe-categories";

    private const CleanupCategoryType SupportedCategory =
        CleanupCategoryType.UserTemporaryFiles;

    private readonly ICheckupTaskActionExecutionCoordinator
        _executionCoordinator;

    public CleanupActionExecutor(
        ICheckupTaskActionExecutionCoordinator
            executionCoordinator)
    {
        ArgumentNullException.ThrowIfNull(
            executionCoordinator);

        _executionCoordinator =
            executionCoordinator;
    }

    public async Task<CleanupActionExecutionResult>
        ExecuteAsync(
            CheckupTaskActionPlan plan,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            plan);

        ValidatePlan(
            plan);

        var startedAt =
            DateTimeOffset.Now;

        if (cancellationToken.IsCancellationRequested)
        {
            return new CleanupActionExecutionResult
            {
                PlanId =
                    plan.Id,

                WasCancelled =
                    true,

                StartedAt =
                    startedAt,

                FinishedAt =
                    DateTimeOffset.Now
            };
        }

        using var executionLease =
            _executionCoordinator.TryBeginExecution(
                plan.ActionCode,
                plan.ActionTitle);

        if (executionLease is null)
        {
            return new CleanupActionExecutionResult
            {
                PlanId =
                    plan.Id,

                WasBlocked =
                    true,

                ErrorMessage =
                    BuildExecutionBlockedMessage(),

                StartedAt =
                    startedAt,

                FinishedAt =
                    DateTimeOffset.Now
            };
        }

        var selectedCategory =
            plan.CleanupCategories.Single();

        try
        {
            var categoryResult =
                await Task.Run(
                    () =>
                        ExecuteUserTemporaryFiles(
                            selectedCategory,
                            cancellationToken));

            return new CleanupActionExecutionResult
            {
                PlanId =
                    plan.Id,

                WasCancelled =
                    categoryResult.WasCancelled,

                ErrorMessage =
                    categoryResult.WasCancelled
                    || categoryResult.IsSuccessful
                        ? string.Empty
                        : categoryResult.ErrorMessage,

                StartedAt =
                    startedAt,

                FinishedAt =
                    DateTimeOffset.Now,

                CategoryResults =
                    new List<
                        CleanupActionCategoryExecutionResult>
                    {
                        categoryResult
                    }
            };
        }
        catch (Exception exception)
        {
            return new CleanupActionExecutionResult
            {
                PlanId =
                    plan.Id,

                ErrorMessage =
                    string.IsNullOrWhiteSpace(
                        exception.Message)
                        ? "Die kontrollierte Bereinigung "
                          + "wurde unerwartet abgebrochen."
                        : exception.Message,

                StartedAt =
                    startedAt,

                FinishedAt =
                    DateTimeOffset.Now
            };
        }
    }

    private static void ValidatePlan(
        CheckupTaskActionPlan plan)
    {
        plan.Validate();

        if (!string.Equals(
                plan.TaskCode,
                RestoreCapacityTaskCode,
                StringComparison.Ordinal)
            && !string.Equals(
                plan.TaskCode,
                ControlledCleanupTaskCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Der Aktionsplan gehört nicht zu einer "
                + "freigegebenen Bereinigungsaufgabe.");
        }

        if (!string.Equals(
                plan.ActionCode,
                SupportedActionCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Der Aktionscode ist für die kontrollierte "
                + "Bereinigung nicht freigegeben.");
        }

        if (plan.HasCommands)
        {
            throw new InvalidOperationException(
                "Ein Bereinigungsplan darf keine externen "
                + "Befehle enthalten.");
        }

        if (plan.CleanupCategories.Count != 1)
        {
            throw new InvalidOperationException(
                "In dieser Ausbaustufe darf genau eine "
                + "Bereinigungskategorie ausgeführt werden.");
        }

        if (plan.MayRequireRestart)
        {
            throw new InvalidOperationException(
                "Der freigegebene Bereinigungsplan darf "
                + "keinen Neustart vorsehen.");
        }

        var category =
            plan.CleanupCategories.Single();

        category.Validate();

        if (category.Category
            != SupportedCategory)
        {
            throw new InvalidOperationException(
                "In dieser Ausbaustufe können ausschließlich "
                + "Benutzertemporärdateien bereinigt werden.");
        }
    }

    private static CleanupActionCategoryExecutionResult
        ExecuteUserTemporaryFiles(
            CleanupActionCategory category,
            CancellationToken cancellationToken)
    {
        var metrics =
            new DeletionMetrics();

        var targetPath =
            string.Empty;

        try
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            targetPath =
                ResolveUserTemporaryDirectory();

            cancellationToken
                .ThrowIfCancellationRequested();

            metrics.WasStarted =
                true;

            if (!Directory.Exists(
                    targetPath))
            {
                return CreateCategoryResult(
                    category,
                    targetPath,
                    metrics,
                    wasCancelled: false,
                    errorMessage: string.Empty);
            }

            ValidateTargetDirectory(
                targetPath);

            DeleteDirectoryContents(
                targetPath,
                cancellationToken,
                metrics);

            return CreateCategoryResult(
                category,
                targetPath,
                metrics,
                wasCancelled: false,
                errorMessage: string.Empty);
        }
        catch (OperationCanceledException)
        {
            return CreateCategoryResult(
                category,
                targetPath,
                metrics,
                wasCancelled: true,
                errorMessage: string.Empty);
        }
        catch (Exception exception)
        {
            return CreateCategoryResult(
                category,
                targetPath,
                metrics,
                wasCancelled: false,
                errorMessage:
                    string.IsNullOrWhiteSpace(
                        exception.Message)
                        ? "Die Benutzertemporärdateien "
                          + "konnten nicht sicher bereinigt werden."
                        : exception.Message);
        }
    }

    private static void DeleteDirectoryContents(
        string rootPath,
        CancellationToken cancellationToken,
        DeletionMetrics metrics)
    {
        if (!TryEnumerateEntries(
                rootPath,
                metrics,
                out var rootEntries))
        {
            return;
        }

        var pendingEntries =
            new Stack<DeletionWorkItem>();

        foreach (var entry
                 in rootEntries)
        {
            pendingEntries.Push(
                new DeletionWorkItem(
                    entry,
                    DeleteDirectoryAfterChildren: false));
        }

        while (pendingEntries.Count > 0)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var workItem =
                pendingEntries.Pop();

            EnsureSafeDescendant(
                workItem.Path,
                rootPath);

            if (workItem.DeleteDirectoryAfterChildren)
            {
                TryDeleteDirectory(
                    workItem.Path,
                    metrics);

                continue;
            }

            if (!TryReadAttributes(
                    workItem.Path,
                    metrics,
                    out var attributes))
            {
                continue;
            }

            if ((attributes
                 & FileAttributes.ReparsePoint)
                != 0)
            {
                metrics.SkippedEntryCount++;

                continue;
            }

            if ((attributes
                 & FileAttributes.Directory)
                == 0)
            {
                TryDeleteFile(
                    workItem.Path,
                    metrics);

                continue;
            }

            if (!TryEnumerateEntries(
                    workItem.Path,
                    metrics,
                    out var childEntries))
            {
                continue;
            }

            pendingEntries.Push(
                new DeletionWorkItem(
                    workItem.Path,
                    DeleteDirectoryAfterChildren: true));

            foreach (var childEntry
                     in childEntries)
            {
                pendingEntries.Push(
                    new DeletionWorkItem(
                        childEntry,
                        DeleteDirectoryAfterChildren: false));
            }
        }
    }

    private static bool TryEnumerateEntries(
        string directoryPath,
        DeletionMetrics metrics,
        out IReadOnlyList<string> entries)
    {
        try
        {
            entries =
                Directory
                    .EnumerateFileSystemEntries(
                        directoryPath,
                        "*",
                        SearchOption.TopDirectoryOnly)
                    .ToList();

            return true;
        }
        catch (DirectoryNotFoundException)
        {
            metrics.SkippedEntryCount++;

            entries =
                Array.Empty<string>();

            return false;
        }
        catch
        {
            metrics.FailedEntryCount++;

            entries =
                Array.Empty<string>();

            return false;
        }
    }

    private static bool TryReadAttributes(
        string path,
        DeletionMetrics metrics,
        out FileAttributes attributes)
    {
        try
        {
            attributes =
                File.GetAttributes(
                    path);

            return true;
        }
        catch (FileNotFoundException)
        {
            metrics.SkippedEntryCount++;

            attributes =
                default;

            return false;
        }
        catch (DirectoryNotFoundException)
        {
            metrics.SkippedEntryCount++;

            attributes =
                default;

            return false;
        }
        catch
        {
            metrics.FailedEntryCount++;

            attributes =
                default;

            return false;
        }
    }

    private static void TryDeleteFile(
        string filePath,
        DeletionMetrics metrics)
    {
        long fileLength =
            0;

        try
        {
            try
            {
                fileLength =
                    new FileInfo(
                        filePath)
                        .Length;
            }
            catch
            {
                fileLength =
                    0;
            }

            File.Delete(
                filePath);

            metrics.DeletedFileCount++;

            AddDeletedSize(
                metrics,
                fileLength);
        }
        catch (FileNotFoundException)
        {
            metrics.SkippedEntryCount++;
        }
        catch (DirectoryNotFoundException)
        {
            metrics.SkippedEntryCount++;
        }
        catch
        {
            metrics.FailedEntryCount++;
        }
    }

    private static void TryDeleteDirectory(
        string directoryPath,
        DeletionMetrics metrics)
    {
        try
        {
            var attributes =
                File.GetAttributes(
                    directoryPath);

            if ((attributes
                 & FileAttributes.ReparsePoint)
                != 0)
            {
                metrics.SkippedEntryCount++;

                return;
            }

            Directory.Delete(
                directoryPath,
                recursive: false);

            metrics.DeletedDirectoryCount++;
        }
        catch (DirectoryNotFoundException)
        {
            metrics.SkippedEntryCount++;
        }
        catch
        {
            metrics.FailedEntryCount++;
        }
    }

    private static void AddDeletedSize(
        DeletionMetrics metrics,
        long fileLength)
    {
        if (fileLength <= 0)
        {
            return;
        }

        var unsignedFileLength =
            (ulong)fileLength;

        if (ulong.MaxValue
            - metrics.DeletedSizeBytes
            < unsignedFileLength)
        {
            metrics.DeletedSizeBytes =
                ulong.MaxValue;

            return;
        }

        metrics.DeletedSizeBytes +=
            unsignedFileLength;
    }

    private static string ResolveUserTemporaryDirectory()
    {
        var localApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(
                localApplicationData))
        {
            throw new InvalidOperationException(
                "Der lokale Anwendungsdatenordner des "
                + "angemeldeten Benutzers ist nicht verfügbar.");
        }

        var expectedTemporaryDirectory =
            NormalizeDirectoryPath(
                Path.Combine(
                    localApplicationData,
                    "Temp"));

        var currentTemporaryDirectory =
            NormalizeDirectoryPath(
                Path.GetTempPath());

        if (IsNetworkPath(
                expectedTemporaryDirectory)
            || IsNetworkPath(
                currentTemporaryDirectory))
        {
            throw new InvalidOperationException(
                "Ein Bereinigungsziel auf einem Netzwerkpfad "
                + "ist nicht zulässig.");
        }

        if (!string.Equals(
                currentTemporaryDirectory,
                expectedTemporaryDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Der aktuelle Temp-Pfad entspricht nicht "
                + "dem ausdrücklich freigegebenen "
                + "Benutzer-Temp-Ordner. Die Bereinigung "
                + "wurde vorsorglich nicht gestartet.");
        }

        var volumeRoot =
            Path.GetPathRoot(
                currentTemporaryDirectory);

        if (string.IsNullOrWhiteSpace(
                volumeRoot)
            || string.Equals(
                NormalizeDirectoryPath(
                    volumeRoot),
                currentTemporaryDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Der Benutzer-Temp-Pfad konnte nicht sicher "
                + "von einem Volumestamm unterschieden werden.");
        }

        return currentTemporaryDirectory;
    }

    private static void ValidateTargetDirectory(
        string targetPath)
    {
        var attributes =
            File.GetAttributes(
                targetPath);

        if ((attributes
             & FileAttributes.Directory)
            == 0)
        {
            throw new InvalidOperationException(
                "Das freigegebene Bereinigungsziel ist "
                + "kein Verzeichnis.");
        }

        if ((attributes
             & FileAttributes.ReparsePoint)
            != 0)
        {
            throw new InvalidOperationException(
                "Der Benutzer-Temp-Ordner ist ein "
                + "Verweis beziehungsweise Reparse Point. "
                + "Die Bereinigung wurde vorsorglich nicht gestartet.");
        }
    }

    private static void EnsureSafeDescendant(
        string entryPath,
        string rootPath)
    {
        var normalizedEntryPath =
            NormalizeDirectoryPath(
                entryPath);

        var normalizedRootPath =
            NormalizeDirectoryPath(
                rootPath);

        var requiredPrefix =
            normalizedRootPath
            + Path.DirectorySeparatorChar;

        if (!normalizedEntryPath.StartsWith(
                requiredPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Ein ermittelter Bereinigungseintrag liegt "
                + "außerhalb des freigegebenen Temp-Ordners.");
        }
    }

    private static string NormalizeDirectoryPath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(
                path))
        {
            throw new InvalidOperationException(
                "Ein Bereinigungspfad ist leer.");
        }

        var fullPath =
            Path.GetFullPath(
                path);

        return Path.TrimEndingDirectorySeparator(
            fullPath);
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

    private string BuildExecutionBlockedMessage()
    {
        if (!_executionCoordinator.IsExecutionRunning)
        {
            return
                "Die Systemaktion konnte nicht gesperrt werden.";
        }

        if (string.IsNullOrWhiteSpace(
                _executionCoordinator.ActiveActionTitle))
        {
            return
                "Eine andere Systemaktion wird bereits ausgeführt.";
        }

        return
            "Die Bereinigung kann noch nicht gestartet werden, "
            + "weil bereits folgende Aktion läuft: "
            + _executionCoordinator.ActiveActionTitle;
    }

    private static CleanupActionCategoryExecutionResult
        CreateCategoryResult(
            CleanupActionCategory category,
            string targetPath,
            DeletionMetrics metrics,
            bool wasCancelled,
            string errorMessage)
    {
        var normalizedErrorMessage =
            wasCancelled
                ? string.Empty
                : NormalizeErrorMessage(
                    errorMessage,
                    metrics.FailedEntryCount);

        return new CleanupActionCategoryExecutionResult
        {
            Category =
                SupportedCategory,

            CategoryTitle =
                category.Title,

            TargetPath =
                targetPath,

            WasStarted =
                metrics.WasStarted,

            WasCancelled =
                wasCancelled,

            DeletedFileCount =
                metrics.DeletedFileCount,

            DeletedDirectoryCount =
                metrics.DeletedDirectoryCount,

            DeletedSizeBytes =
                metrics.DeletedSizeBytes,

            FailedEntryCount =
                metrics.FailedEntryCount,

            SkippedEntryCount =
                metrics.SkippedEntryCount,

            ErrorMessage =
                normalizedErrorMessage
        };
    }

    private static string NormalizeErrorMessage(
        string errorMessage,
        long failedEntryCount)
    {
        if (!string.IsNullOrWhiteSpace(
                errorMessage))
        {
            return errorMessage.Trim();
        }

        return failedEntryCount switch
        {
            0 =>
                string.Empty,

            1 =>
                "Ein Eintrag konnte nicht gelöscht werden. "
                + "Die übrigen Ergebnisse wurden protokolliert.",

            _ =>
                $"{failedEntryCount:N0} Einträge konnten "
                + "nicht gelöscht werden. Die übrigen "
                + "Ergebnisse wurden protokolliert."
        };
    }

    private sealed class DeletionMetrics
    {
        public bool WasStarted { get; set; }

        public long DeletedFileCount { get; set; }

        public long DeletedDirectoryCount { get; set; }

        public ulong DeletedSizeBytes { get; set; }

        public long FailedEntryCount { get; set; }

        public long SkippedEntryCount { get; set; }
    }

    private sealed record DeletionWorkItem(
        string Path,
        bool DeleteDirectoryAfterChildren);
}
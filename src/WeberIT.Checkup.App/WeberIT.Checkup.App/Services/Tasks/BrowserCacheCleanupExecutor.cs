using System.IO;
using System.Text;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Cleanup;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Tasks;

public sealed class BrowserCacheCleanupExecutor
{
    private const string RestoreCapacityTaskCode =
        "task.storage.restore-system-volume-capacity";

    private const string ControlledCleanupTaskCode =
        "task.storage.controlled-cleanup";

    private const string SupportedActionCode =
        "action.cleanup.selected-safe-categories";

    private readonly ICheckupTaskActionExecutionCoordinator
        _executionCoordinator;

    public BrowserCacheCleanupExecutor(
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
            return CreateBlockedResult(
                plan.Id,
                startedAt,
                "Eine andere technische Systemaktion wird "
                + "bereits ausgeführt. Die Browsercache-"
                + "Bereinigung wurde nicht gestartet.");
        }

        var initialProcessState =
            BrowserCacheRuntimeGuard.Evaluate();

        if (!initialProcessState.CanProceed)
        {
            return CreateBlockedResult(
                plan.Id,
                startedAt,
                initialProcessState.BlockingMessage);
        }

        var category =
            plan.CleanupCategories.Single();

        try
        {
            var outcome =
                await Task.Run(
                    () =>
                        ExecuteBrowserCache(
                            category,
                            cancellationToken));

            if (outcome.WasBlocked)
            {
                return CreateBlockedResult(
                    plan.Id,
                    startedAt,
                    outcome.ErrorMessage);
            }

            if (outcome.CategoryResult is null)
            {
                return new CleanupActionExecutionResult
                {
                    PlanId =
                        plan.Id,

                    ErrorMessage =
                        string.IsNullOrWhiteSpace(
                            outcome.ErrorMessage)
                            ? "Die Browsercache-Bereinigung "
                              + "lieferte kein eindeutiges "
                              + "Kategorieergebnis."
                            : outcome.ErrorMessage,

                    StartedAt =
                        startedAt,

                    FinishedAt =
                        DateTimeOffset.Now
                };
            }

            var categoryResult =
                outcome.CategoryResult;

            return new CleanupActionExecutionResult
            {
                PlanId =
                    plan.Id,

                WasCancelled =
                    categoryResult.WasCancelled,

                ErrorMessage =
                    categoryResult.WasCancelled
                    || categoryResult.IsSuccessful
                    || categoryResult.IsPartiallySuccessful
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
                        ? "Die Browsercache-Bereinigung wurde "
                          + "unerwartet abgebrochen."
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
                + "Browsercache-Bereinigung nicht freigegeben.");
        }

        if (plan.HasCommands)
        {
            throw new InvalidOperationException(
                "Ein Browsercache-Bereinigungsplan darf "
                + "keine externen Befehle enthalten.");
        }

        if (plan.CleanupCategories.Count != 1)
        {
            throw new InvalidOperationException(
                "Für die Browsercache-Bereinigung muss genau "
                + "eine Kategorie ausgewählt werden.");
        }

        if (plan.RequiresAdministrator)
        {
            throw new InvalidOperationException(
                "Die Browsercache-Bereinigung darf keine "
                + "Administratorrechte anfordern.");
        }

        if (plan.MayRequireRestart)
        {
            throw new InvalidOperationException(
                "Die Browsercache-Bereinigung darf keinen "
                + "Neustart vorsehen.");
        }

        var category =
            plan.CleanupCategories.Single();

        category.Validate();

        if (category.Category
            != CleanupCategoryType.BrowserCache)
        {
            throw new InvalidOperationException(
                "Der übergebene Bereinigungsplan enthält "
                + "keine Browsercache-Kategorie.");
        }
    }

    private static BrowserCacheExecutionOutcome
        ExecuteBrowserCache(
            CleanupActionCategory category,
            CancellationToken cancellationToken)
    {
        var resolution =
            BrowserCacheTargetResolver.Resolve();

        if (!resolution.IsAvailable)
        {
            return BrowserCacheExecutionOutcome.Failed(
                CreateCategoryResult(
                    category,
                    string.Empty,
                    new BrowserDeletionMetrics(),
                    wasCancelled: false,
                    resolution.ErrorMessage));
        }

        if (resolution.HadDiscoveryErrors)
        {
            return BrowserCacheExecutionOutcome.Blocked(
                "Die aktuell vorhandenen Browserprofile "
                + "konnten nicht vollständig und eindeutig "
                + "ermittelt werden. Aus Sicherheitsgründen "
                + "wurde keine Datei gelöscht.");
        }

        var existingTargets =
            resolution.Targets
                .Where(
                    target =>
                        Directory.Exists(
                            target.Path))
                .ToList();

        foreach (var target
                 in existingTargets)
        {
            ValidateTargetDirectory(
                target.Path,
                resolution.LocalApplicationDataPath);
        }

        var finalProcessState =
            BrowserCacheRuntimeGuard.Evaluate();

        if (!finalProcessState.CanProceed)
        {
            return BrowserCacheExecutionOutcome.Blocked(
                finalProcessState.BlockingMessage);
        }

        var metrics =
            new BrowserDeletionMetrics
            {
                WasStarted =
                    true
            };

        var targetDescription =
            BuildTargetDescription(
                resolution.LocalApplicationDataPath,
                existingTargets);

        try
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            foreach (var target
                     in existingTargets)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var currentProcessState =
                    BrowserCacheRuntimeGuard.Evaluate();

                if (!currentProcessState.CanProceed)
                {
                    if (!metrics.HasChanges
                        && metrics.FailedEntryCount == 0)
                    {
                        return BrowserCacheExecutionOutcome.Blocked(
                            currentProcessState.BlockingMessage);
                    }

                    return BrowserCacheExecutionOutcome.Failed(
                        CreateCategoryResult(
                            category,
                            targetDescription,
                            metrics,
                            wasCancelled: false,
                            "Während der Bereinigung wurde ein "
                            + "unterstützter Browser gestartet. "
                            + "Die weitere Verarbeitung wurde "
                            + "sofort beendet. Bereits gelöschte "
                            + "Cachedateien bleiben gelöscht."));
                }

                DeleteDirectoryContents(
                    target.Path,
                    resolution.LocalApplicationDataPath,
                    cancellationToken,
                    metrics);
            }

            return BrowserCacheExecutionOutcome.Completed(
                CreateCategoryResult(
                    category,
                    targetDescription,
                    metrics,
                    wasCancelled: false,
                    string.Empty));
        }
        catch (OperationCanceledException)
        {
            return BrowserCacheExecutionOutcome.Completed(
                CreateCategoryResult(
                    category,
                    targetDescription,
                    metrics,
                    wasCancelled: true,
                    string.Empty));
        }
        catch (Exception exception)
        {
            return BrowserCacheExecutionOutcome.Failed(
                CreateCategoryResult(
                    category,
                    targetDescription,
                    metrics,
                    wasCancelled: false,
                    string.IsNullOrWhiteSpace(
                        exception.Message)
                        ? "Der Browsercache konnte nicht "
                          + "sicher bereinigt werden."
                        : exception.Message));
        }
    }

    private static void DeleteDirectoryContents(
        string targetPath,
        string localApplicationDataPath,
        CancellationToken cancellationToken,
        BrowserDeletionMetrics metrics)
    {
        ValidateTargetDirectory(
            targetPath,
            localApplicationDataPath);

        if (!TryEnumerateEntries(
                targetPath,
                metrics,
                out var rootEntries))
        {
            return;
        }

        var pendingEntries =
            new Stack<BrowserDeletionWorkItem>();

        foreach (var entry
                 in rootEntries)
        {
            pendingEntries.Push(
                new BrowserDeletionWorkItem(
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
                targetPath);

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
                new BrowserDeletionWorkItem(
                    workItem.Path,
                    DeleteDirectoryAfterChildren: true));

            foreach (var childEntry
                     in childEntries)
            {
                pendingEntries.Push(
                    new BrowserDeletionWorkItem(
                        childEntry,
                        DeleteDirectoryAfterChildren: false));
            }
        }
    }

    private static bool TryEnumerateEntries(
        string path,
        BrowserDeletionMetrics metrics,
        out IReadOnlyList<string> entries)
    {
        try
        {
            entries =
                Directory.EnumerateFileSystemEntries(
                        path,
                        "*",
                        SearchOption.TopDirectoryOnly)
                    .ToList();

            return true;
        }
        catch (DirectoryNotFoundException)
        {
            entries =
                Array.Empty<string>();

            metrics.SkippedEntryCount++;

            return false;
        }
        catch
        {
            entries =
                Array.Empty<string>();

            metrics.FailedEntryCount++;

            return false;
        }
    }

    private static bool TryReadAttributes(
        string path,
        BrowserDeletionMetrics metrics,
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
            attributes =
                default;

            metrics.SkippedEntryCount++;

            return false;
        }
        catch (DirectoryNotFoundException)
        {
            attributes =
                default;

            metrics.SkippedEntryCount++;

            return false;
        }
        catch
        {
            attributes =
                default;

            metrics.FailedEntryCount++;

            return false;
        }
    }

    private static void TryDeleteFile(
        string path,
        BrowserDeletionMetrics metrics)
    {
        ulong fileSize =
            0;

        try
        {
            var fileInformation =
                new FileInfo(
                    path);

            if (fileInformation.Exists)
            {
                fileSize =
                    fileInformation.Length < 0
                        ? 0
                        : (ulong)fileInformation.Length;
            }

            File.Delete(
                path);

            metrics.DeletedFileCount++;
            metrics.AddDeletedSize(
                fileSize);
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
        string path,
        BrowserDeletionMetrics metrics)
    {
        try
        {
            Directory.Delete(
                path,
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

    private static void ValidateTargetDirectory(
        string targetPath,
        string localApplicationDataPath)
    {
        var normalizedTargetPath =
            NormalizeDirectoryPath(
                targetPath);

        var normalizedLocalApplicationDataPath =
            NormalizeDirectoryPath(
                localApplicationDataPath);

        if (IsNetworkPath(
                normalizedTargetPath)
            || IsNetworkPath(
                normalizedLocalApplicationDataPath))
        {
            throw new InvalidOperationException(
                "Netzwerkpfade sind für die Browsercache-"
                + "Bereinigung nicht freigegeben.");
        }

        if (!IsStrictDescendant(
                normalizedTargetPath,
                normalizedLocalApplicationDataPath))
        {
            throw new InvalidOperationException(
                "Ein Browsercache-Ziel liegt außerhalb des "
                + "lokalen Anwendungsdatenordners.");
        }

        EnsureNoReparsePointInExistingPath(
            normalizedTargetPath,
            normalizedLocalApplicationDataPath);
    }

    private static void EnsureNoReparsePointInExistingPath(
        string targetPath,
        string allowedRootPath)
    {
        var currentPath =
            targetPath;

        while (!string.Equals(
                   currentPath,
                   allowedRootPath,
                   StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(
                    currentPath))
            {
                var attributes =
                    File.GetAttributes(
                        currentPath);

                if ((attributes
                     & FileAttributes.ReparsePoint)
                    != 0)
                {
                    throw new InvalidOperationException(
                        "Ein Browsercache-Ziel oder einer seiner "
                        + "übergeordneten Profilordner ist ein "
                        + "Umleitungs- beziehungsweise "
                        + "Verknüpfungspunkt.");
                }
            }

            var parentDirectory =
                Directory.GetParent(
                    currentPath);

            if (parentDirectory is null)
            {
                throw new InvalidOperationException(
                    "Der Browsercache-Pfad konnte nicht sicher "
                    + "bis zum erlaubten Benutzerordner "
                    + "zurückverfolgt werden.");
            }

            currentPath =
                NormalizeDirectoryPath(
                    parentDirectory.FullName);
        }
    }

    private static void EnsureSafeDescendant(
        string candidatePath,
        string targetRootPath)
    {
        var normalizedCandidatePath =
            Path.GetFullPath(
                candidatePath);

        var normalizedTargetRootPath =
            NormalizeDirectoryPath(
                targetRootPath);

        if (!IsStrictDescendant(
                normalizedCandidatePath,
                normalizedTargetRootPath))
        {
            throw new InvalidOperationException(
                "Ein zu verarbeitender Eintrag liegt außerhalb "
                + "des ausdrücklich bestätigten "
                + "Browsercache-Zielordners.");
        }
    }

    private static bool IsStrictDescendant(
        string candidatePath,
        string rootPath)
    {
        var normalizedCandidatePath =
            Path.GetFullPath(
                candidatePath);

        var normalizedRootPath =
            NormalizeDirectoryPath(
                rootPath);

        if (string.Equals(
                normalizedCandidatePath,
                normalizedRootPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rootPrefix =
            normalizedRootPath
            + Path.DirectorySeparatorChar;

        return normalizedCandidatePath.StartsWith(
            rootPrefix,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDirectoryPath(
        string path)
    {
        var fullPath =
            Path.GetFullPath(
                path);

        var rootPath =
            Path.GetPathRoot(
                fullPath);

        if (string.IsNullOrWhiteSpace(
                rootPath))
        {
            throw new InvalidOperationException(
                "Ein Browsercache-Pfad besitzt kein "
                + "eindeutiges lokales Stammverzeichnis.");
        }

        if (string.Equals(
                fullPath,
                rootPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return fullPath;
        }

        return fullPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
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

    private static string BuildTargetDescription(
        string localApplicationDataPath,
        IReadOnlyCollection<BrowserCacheTarget> targets)
    {
        if (targets.Count == 0)
        {
            return
                "Keine derzeit vorhandenen freigegebenen "
                + "Browsercache-Zielordner unter "
                + localApplicationDataPath;
        }

        var builder =
            new StringBuilder();

        builder.AppendLine(
            "Freigegebene Browsercache-Zielordner:");

        foreach (var target
                 in targets)
        {
            builder.Append("• ");
            builder.Append(target.BrowserName);
            builder.Append(": ");
            builder.AppendLine(target.Path);
        }

        return builder
            .ToString()
            .TrimEnd();
    }

    private static CleanupActionCategoryExecutionResult
        CreateCategoryResult(
            CleanupActionCategory category,
            string targetPath,
            BrowserDeletionMetrics metrics,
            bool wasCancelled,
            string errorMessage)
    {
        return new CleanupActionCategoryExecutionResult
        {
            Category =
                CleanupCategoryType.BrowserCache,

            CategoryTitle =
                string.IsNullOrWhiteSpace(
                    category.Title)
                    ? "Browsercache"
                    : category.Title,

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
                errorMessage
        };
    }

    private static CleanupActionExecutionResult
        CreateBlockedResult(
            Guid planId,
            DateTimeOffset startedAt,
            string errorMessage)
    {
        return new CleanupActionExecutionResult
        {
            PlanId =
                planId,

            WasBlocked =
                true,

            ErrorMessage =
                string.IsNullOrWhiteSpace(
                    errorMessage)
                    ? "Die Browsercache-Bereinigung wurde "
                      + "vor dem ersten Dateizugriff blockiert."
                    : errorMessage,

            StartedAt =
                startedAt,

            FinishedAt =
                DateTimeOffset.Now
        };
    }

    private sealed record BrowserDeletionWorkItem(
        string Path,
        bool DeleteDirectoryAfterChildren);

    private sealed class BrowserDeletionMetrics
    {
        public bool WasStarted { get; init; }

        public long DeletedFileCount { get; set; }

        public long DeletedDirectoryCount { get; set; }

        public ulong DeletedSizeBytes { get; private set; }

        public long FailedEntryCount { get; set; }

        public long SkippedEntryCount { get; set; }

        public bool HasChanges =>
            DeletedFileCount > 0
            || DeletedDirectoryCount > 0;

        public void AddDeletedSize(
            ulong sizeBytes)
        {
            if (ulong.MaxValue
                - DeletedSizeBytes
                < sizeBytes)
            {
                DeletedSizeBytes =
                    ulong.MaxValue;

                return;
            }

            DeletedSizeBytes +=
                sizeBytes;
        }
    }

    private sealed class BrowserCacheExecutionOutcome
    {
        public bool WasBlocked { get; init; }

        public string ErrorMessage { get; init; } =
            string.Empty;

        public CleanupActionCategoryExecutionResult?
            CategoryResult
        {
            get;
            init;
        }

        public static BrowserCacheExecutionOutcome Blocked(
            string errorMessage)
        {
            return new BrowserCacheExecutionOutcome
            {
                WasBlocked =
                    true,

                ErrorMessage =
                    errorMessage
            };
        }

        public static BrowserCacheExecutionOutcome Completed(
            CleanupActionCategoryExecutionResult categoryResult)
        {
            return new BrowserCacheExecutionOutcome
            {
                CategoryResult =
                    categoryResult
            };
        }

        public static BrowserCacheExecutionOutcome Failed(
            CleanupActionCategoryExecutionResult categoryResult)
        {
            return new BrowserCacheExecutionOutcome
            {
                ErrorMessage =
                    categoryResult.ErrorMessage,

                CategoryResult =
                    categoryResult
            };
        }
    }
}
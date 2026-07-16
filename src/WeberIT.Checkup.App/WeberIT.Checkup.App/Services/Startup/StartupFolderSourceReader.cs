using System.IO;
using System.Security.Cryptography;
using System.Text;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Startup;

public class StartupFolderSourceReader
{
    private readonly StartupCommandAnalyzer
        _commandAnalyzer;

    private readonly ShellLinkTargetReader
        _shellLinkTargetReader;

    public StartupFolderSourceReader(
        StartupCommandAnalyzer commandAnalyzer,
        ShellLinkTargetReader shellLinkTargetReader)
    {
        _commandAnalyzer =
            commandAnalyzer;

        _shellLinkTargetReader =
            shellLinkTargetReader;
    }

    public StartupFolderReadResult Read(
        DateTime deadline)
    {
        var result =
            new StartupFolderReadResult();

        ReadFolder(
            Environment.GetFolderPath(
                Environment.SpecialFolder.Startup),
            StartupEntryContext.CurrentUser,
            result,
            deadline);

        ReadFolder(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonStartup),
            StartupEntryContext.AllUsers,
            result,
            deadline);

        return result;
    }

    private void ReadFolder(
        string folderPath,
        StartupEntryContext context,
        StartupFolderReadResult result,
        DateTime deadline)
    {
        if (DateTime.UtcNow
            >= deadline)
        {
            result.WasTimedOut =
                true;

            return;
        }

        if (string.IsNullOrWhiteSpace(
                folderPath)
            || IsNetworkPath(
                folderPath))
        {
            result.FailedSourceCount++;
            return;
        }

        try
        {
            var folderInformation =
                new DirectoryInfo(
                    folderPath);

            if (!folderInformation.Exists)
            {
                result.SuccessfulSourceCount++;
                return;
            }

            if ((folderInformation.Attributes
                 & FileAttributes.ReparsePoint)
                != 0)
            {
                result.FailedSourceCount++;
                return;
            }

            foreach (var fileInformation
                     in folderInformation
                         .EnumerateFiles(
                             "*",
                             SearchOption.TopDirectoryOnly))
            {
                if (DateTime.UtcNow
                    >= deadline)
                {
                    result.WasTimedOut =
                        true;

                    return;
                }

                ReadFolderEntry(
                    fileInformation,
                    context,
                    result);
            }

            result.SuccessfulSourceCount++;
        }
        catch
        {
            result.FailedSourceCount++;
        }
    }

    private void ReadFolderEntry(
        FileInfo fileInformation,
        StartupEntryContext context,
        StartupFolderReadResult result)
    {
        try
        {
            if ((fileInformation.Attributes
                 & FileAttributes.ReparsePoint)
                != 0)
            {
                result.ExcludedEntryCount++;
                return;
            }

            var extension =
                fileInformation.Extension;

            if (extension.Equals(
                    ".lnk",
                    StringComparison.OrdinalIgnoreCase))
            {
                ReadShortcutEntry(
                    fileInformation,
                    context,
                    result);

                return;
            }

            if (!IsSupportedDirectStartupFile(
                    extension))
            {
                result.Entries.Add(
                    CreateUnsupportedEntry(
                        fileInformation,
                        context));

                return;
            }

            var entry =
                _commandAnalyzer.Analyze(
                    Path.GetFileNameWithoutExtension(
                        fileInformation.Name),
                    QuotePath(
                        fileInformation.FullName),
                    StartupSourceType.StartupFolder,
                    context,
                    StartupRegistryView.NotApplicable,
                    StartupEntryState.Enabled);

            entry.SourceIdentifier =
                CreateFolderSourceIdentifier(
                    context,
                    fileInformation.Name);

            result.Entries.Add(
                entry);
        }
        catch
        {
            result.Entries.Add(
                CreateFailedEntry(
                    fileInformation.Name,
                    context));
        }
    }

    private void ReadShortcutEntry(
        FileInfo shortcutInformation,
        StartupEntryContext context,
        StartupFolderReadResult result)
    {
        var displayName =
            Path.GetFileNameWithoutExtension(
                shortcutInformation.Name);

        if (!_shellLinkTargetReader
            .TryReadStoredTarget(
                shortcutInformation.FullName,
                out var targetPath))
        {
            result.Entries.Add(
                new StartupEntryInformation
                {
                    DisplayName =
                        string.IsNullOrWhiteSpace(
                            displayName)
                            ? "Unbekannte Verknüpfung"
                            : displayName,

                    NormalizedProgramName =
                        displayName,

                    SourceType =
                        StartupSourceType.StartupFolder,

                    Context =
                        context,

                    RegistryView =
                        StartupRegistryView.NotApplicable,

                    State =
                        StartupEntryState.Enabled,

                    TargetType =
                        StartupTargetType.Shortcut,

                    TargetExists =
                        null,

                    Classification =
                        StartupClassification.NotEvaluable,

                    SourceIdentifier =
                        CreateFolderSourceIdentifier(
                            context,
                            shortcutInformation.Name),

                    Description =
                        "Das in der Verknüpfung gespeicherte "
                        + "Startziel konnte ohne weitergehende "
                        + "Auflösung nicht zuverlässig gelesen werden."
                });

            return;
        }

        if (IsNetworkPath(
                targetPath))
        {
            result.Entries.Add(
                new StartupEntryInformation
                {
                    DisplayName =
                        string.IsNullOrWhiteSpace(
                            displayName)
                            ? "Unbekannte Verknüpfung"
                            : displayName,

                    NormalizedProgramName =
                        displayName,

                    SourceType =
                        StartupSourceType.StartupFolder,

                    Context =
                        context,

                    RegistryView =
                        StartupRegistryView.NotApplicable,

                    State =
                        StartupEntryState.Enabled,

                    TargetType =
                        StartupTargetType.Shortcut,

                    TargetExists =
                        null,

                    Classification =
                        StartupClassification.Unknown,

                    SourceIdentifier =
                        CreateFolderSourceIdentifier(
                            context,
                            shortcutInformation.Name),

                    Description =
                        "Die Verknüpfung verweist auf ein "
                        + "Netzwerkziel. Dieses Ziel wurde aus "
                        + "Sicherheits- und Datenschutzgründen "
                        + "nicht weiter geprüft."
                });

            return;
        }

        var entry =
            _commandAnalyzer.Analyze(
                displayName,
                QuotePath(
                    targetPath),
                StartupSourceType.StartupFolder,
                context,
                StartupRegistryView.NotApplicable,
                StartupEntryState.Enabled);

        entry.TargetType =
            StartupTargetType.Shortcut;

        entry.SourceIdentifier =
            CreateFolderSourceIdentifier(
                context,
                shortcutInformation.Name);

        entry.Description =
            BuildShortcutDescription(
                entry);

        result.Entries.Add(
            entry);
    }

    private static StartupEntryInformation
        CreateUnsupportedEntry(
            FileInfo fileInformation,
            StartupEntryContext context)
    {
        var displayName =
            Path.GetFileNameWithoutExtension(
                fileInformation.Name);

        return new StartupEntryInformation
        {
            DisplayName =
                string.IsNullOrWhiteSpace(
                    displayName)
                    ? "Unbekannter Autostarteintrag"
                    : displayName,

            NormalizedProgramName =
                displayName,

            SourceType =
                StartupSourceType.StartupFolder,

            Context =
                context,

            RegistryView =
                StartupRegistryView.NotApplicable,

            State =
                StartupEntryState.Enabled,

            TargetType =
                StartupTargetType.Unknown,

            TargetExists =
                true,

            Classification =
                StartupClassification.NotEvaluable,

            SourceIdentifier =
                CreateFolderSourceIdentifier(
                    context,
                    fileInformation.Name),

            Description =
                "Im Autostartordner wurde ein vorhandener "
                + "Dateityp erkannt, der nicht zuverlässig als "
                + "Programm, Skript oder Verknüpfung eingeordnet "
                + "werden konnte. Die Datei wurde nicht geöffnet."
        };
    }

    private static StartupEntryInformation CreateFailedEntry(
        string fileName,
        StartupEntryContext context)
    {
        var displayName =
            Path.GetFileNameWithoutExtension(
                fileName);

        return new StartupEntryInformation
        {
            DisplayName =
                string.IsNullOrWhiteSpace(
                    displayName)
                    ? "Unbekannter Autostarteintrag"
                    : displayName,

            NormalizedProgramName =
                displayName,

            SourceType =
                StartupSourceType.StartupFolder,

            Context =
                context,

            RegistryView =
                StartupRegistryView.NotApplicable,

            State =
                StartupEntryState.Unknown,

            TargetType =
                StartupTargetType.Unknown,

            TargetExists =
                null,

            Classification =
                StartupClassification.NotEvaluable,

            SourceIdentifier =
                CreateFolderSourceIdentifier(
                    context,
                    fileName),

            Description =
                "Der Eintrag im Autostartordner konnte nicht "
                + "vollständig ausgewertet werden."
        };
    }

    private static bool IsSupportedDirectStartupFile(
        string extension)
    {
        return extension.Equals(
                   ".exe",
                   StringComparison.OrdinalIgnoreCase)
               || extension.Equals(
                   ".com",
                   StringComparison.OrdinalIgnoreCase)
               || extension.Equals(
                   ".cmd",
                   StringComparison.OrdinalIgnoreCase)
               || extension.Equals(
                   ".bat",
                   StringComparison.OrdinalIgnoreCase)
               || extension.Equals(
                   ".ps1",
                   StringComparison.OrdinalIgnoreCase)
               || extension.Equals(
                   ".vbs",
                   StringComparison.OrdinalIgnoreCase)
               || extension.Equals(
                   ".js",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildShortcutDescription(
        StartupEntryInformation entry)
    {
        if (entry.TargetExists == false)
        {
            return "Die Autostartverknüpfung ist aktiv, "
                   + "das darin gespeicherte lokale Ziel wurde "
                   + "jedoch nicht gefunden. Der Eintrag sollte "
                   + "manuell geprüft werden.";
        }

        if (entry.TargetExists is null)
        {
            return "Das gespeicherte Ziel der Autostartverknüpfung "
                   + "konnte ohne weitergehende Auflösung nicht "
                   + "sicher geprüft werden.";
        }

        if (entry.Classification
            == StartupClassification.SystemOrDriverRelated)
        {
            return "Das gespeicherte Ziel der Verknüpfung ist "
                   + "vorhanden und liegt im Windows-Verzeichnis. "
                   + "Der Eintrag wird vorsichtig als system- oder "
                   + "treibernah eingeordnet.";
        }

        if (entry.Classification
            == StartupClassification.ProbablyUseful)
        {
            return "Das gespeicherte lokale Ziel der Verknüpfung "
                   + "ist vorhanden und enthält auswertbare Produkt- "
                   + "oder Herstellerinformationen. Dies ist kein "
                   + "Nachweis einer digitalen Signatur.";
        }

        return "Das gespeicherte lokale Ziel der "
               + "Autostartverknüpfung ist vorhanden. Eine "
               + "weitergehende Einordnung war nicht eindeutig möglich.";
    }

    private static string QuotePath(
        string path)
    {
        return $"\"{path}\"";
    }

    private static string CreateFolderSourceIdentifier(
        StartupEntryContext context,
        string fileName)
    {
        var identifierSource =
            $"{StartupSourceType.StartupFolder}|"
            + $"{context}|"
            + fileName.Trim().ToUpperInvariant();

        var identifierBytes =
            Encoding.UTF8.GetBytes(
                identifierSource);

        var hashBytes =
            SHA256.HashData(
                identifierBytes);

        return Convert.ToHexString(
            hashBytes);
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
}
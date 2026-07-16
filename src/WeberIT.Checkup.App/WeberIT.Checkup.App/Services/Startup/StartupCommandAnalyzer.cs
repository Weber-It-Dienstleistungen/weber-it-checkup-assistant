using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Startup;

public class StartupCommandAnalyzer
{
    private static readonly string[] KnownTargetExtensions =
    {
        ".exe",
        ".com",
        ".cmd",
        ".bat",
        ".ps1",
        ".vbs",
        ".js",
        ".dll"
    };

    public StartupEntryInformation Analyze(
        string entryName,
        string command,
        StartupSourceType sourceType,
        StartupEntryContext context,
        StartupRegistryView registryView,
        StartupEntryState state)
    {
        var information =
            new StartupEntryInformation
            {
                DisplayName =
                    NormalizeDisplayName(
                        entryName),

                NormalizedProgramName =
                    NormalizeProgramName(
                        entryName),

                SourceType =
                    sourceType,

                Context =
                    context,

                RegistryView =
                    registryView,

                State =
                    state,

                SourceIdentifier =
                    CreateSourceIdentifier(
                        sourceType,
                        context,
                        registryView,
                        entryName)
            };

        if (state == StartupEntryState.Disabled)
        {
            information.Classification =
                StartupClassification.Disabled;
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            information.TargetType =
                StartupTargetType.Unknown;

            information.TargetExists =
                null;

            information.Classification =
                state == StartupEntryState.Disabled
                    ? StartupClassification.Disabled
                    : StartupClassification.NotEvaluable;

            information.Description =
                "Für den Autostarteintrag war kein auswertbarer "
                + "Startbefehl hinterlegt.";

            return information;
        }

        var expandedCommand =
            ExpandEnvironmentVariables(
                command);

        var targetPath =
            ExtractTargetPath(
                expandedCommand);

        information.TargetType =
            DetermineTargetType(
                expandedCommand,
                targetPath);

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            information.TargetExists =
                null;

            if (state != StartupEntryState.Disabled)
            {
                information.Classification =
                    StartupClassification.NotEvaluable;
            }

            information.Description =
                BuildIndirectTargetDescription(
                    information.TargetType,
                    state);

            return information;
        }

        var normalizedTargetPath =
            NormalizeLocalTargetPath(
                targetPath);

        if (string.IsNullOrWhiteSpace(
                normalizedTargetPath)
            || IsNetworkPath(
                normalizedTargetPath))
        {
            information.TargetExists =
                null;

            if (state != StartupEntryState.Disabled)
            {
                information.Classification =
                    StartupClassification.Unknown;
            }

            information.Description =
                "Das Startziel ist indirekt, nicht lokal oder "
                + "konnte ohne Ausführung nicht sicher geprüft werden.";

            return information;
        }

        var targetExists =
            TryDetermineTargetExistence(
                normalizedTargetPath);

        information.TargetExists =
            targetExists;

        if (targetExists == true
            && information.TargetType
                == StartupTargetType.Executable)
        {
            ReadFileVersionInformation(
                normalizedTargetPath,
                information);
        }

        ApplyInitialClassification(
            information,
            normalizedTargetPath);

        information.Description =
            BuildDescription(
                information);

        return information;
    }

    private static string NormalizeDisplayName(
        string entryName)
    {
        if (string.IsNullOrWhiteSpace(
                entryName))
        {
            return "Unbekannter Autostarteintrag";
        }

        return entryName.Trim();
    }

    private static string NormalizeProgramName(
        string entryName)
    {
        if (string.IsNullOrWhiteSpace(
                entryName))
        {
            return string.Empty;
        }

        var normalizedName =
            entryName.Trim();

        var extension =
            Path.GetExtension(
                normalizedName);

        if (!string.IsNullOrWhiteSpace(
                extension))
        {
            normalizedName =
                Path.GetFileNameWithoutExtension(
                    normalizedName);
        }

        return normalizedName.Trim();
    }

    private static string ExpandEnvironmentVariables(
        string command)
    {
        try
        {
            return Environment
                .ExpandEnvironmentVariables(
                    command.Trim());
        }
        catch
        {
            return command.Trim();
        }
    }

    private static string ExtractTargetPath(
        string command)
    {
        if (string.IsNullOrWhiteSpace(
                command))
        {
            return string.Empty;
        }

        var trimmedCommand =
            command.Trim();

        if (trimmedCommand.StartsWith(
                '"'))
        {
            var closingQuoteIndex =
                trimmedCommand.IndexOf(
                    '"',
                    1);

            if (closingQuoteIndex > 1)
            {
                return trimmedCommand
                    .Substring(
                        1,
                        closingQuoteIndex - 1)
                    .Trim();
            }

            return string.Empty;
        }

        var extensionEndIndex =
            FindKnownExtensionEndIndex(
                trimmedCommand);

        if (extensionEndIndex > 0)
        {
            return trimmedCommand
                .Substring(
                    0,
                    extensionEndIndex)
                .Trim()
                .Trim('"');
        }

        var firstSeparatorIndex =
            trimmedCommand.IndexOfAny(
                new[]
                {
                    ' ',
                    '\t'
                });

        if (firstSeparatorIndex > 0)
        {
            return trimmedCommand
                .Substring(
                    0,
                    firstSeparatorIndex)
                .Trim()
                .Trim('"');
        }

        return trimmedCommand.Trim('"');
    }

    private static int FindKnownExtensionEndIndex(
        string command)
    {
        var bestEndIndex =
            -1;

        foreach (var extension
                 in KnownTargetExtensions)
        {
            var searchStartIndex =
                0;

            while (searchStartIndex
                   < command.Length)
            {
                var extensionIndex =
                    command.IndexOf(
                        extension,
                        searchStartIndex,
                        StringComparison.OrdinalIgnoreCase);

                if (extensionIndex < 0)
                {
                    break;
                }

                var extensionEndIndex =
                    extensionIndex
                    + extension.Length;

                if (extensionEndIndex
                        == command.Length
                    || IsTargetSeparator(
                        command[
                            extensionEndIndex]))
                {
                    if (bestEndIndex < 0
                        || extensionEndIndex
                            < bestEndIndex)
                    {
                        bestEndIndex =
                            extensionEndIndex;
                    }

                    break;
                }

                searchStartIndex =
                    extensionEndIndex;
            }
        }

        return bestEndIndex;
    }

    private static bool IsTargetSeparator(
        char character)
    {
        return char.IsWhiteSpace(
                   character)
               || character == ','
               || character == '"';
    }

    private static StartupTargetType DetermineTargetType(
        string command,
        string targetPath)
    {
        var executableName =
            Path.GetFileName(
                targetPath);

        if (executableName.Equals(
                "powershell.exe",
                StringComparison.OrdinalIgnoreCase)
            || executableName.Equals(
                "powershell",
                StringComparison.OrdinalIgnoreCase)
            || executableName.Equals(
                "pwsh.exe",
                StringComparison.OrdinalIgnoreCase)
            || executableName.Equals(
                "pwsh",
                StringComparison.OrdinalIgnoreCase))
        {
            return StartupTargetType.PowerShell;
        }

        if (executableName.Equals(
                "cmd.exe",
                StringComparison.OrdinalIgnoreCase)
            || executableName.Equals(
                "cmd",
                StringComparison.OrdinalIgnoreCase))
        {
            return StartupTargetType.CommandInterpreter;
        }

        if (executableName.Equals(
                "rundll32.exe",
                StringComparison.OrdinalIgnoreCase)
            || executableName.Equals(
                "rundll32",
                StringComparison.OrdinalIgnoreCase))
        {
            return StartupTargetType.DynamicLibrary;
        }

        if (command.StartsWith(
                "shell:",
                StringComparison.OrdinalIgnoreCase)
            || command.Contains(
                "shell:AppsFolder",
                StringComparison.OrdinalIgnoreCase))
        {
            return StartupTargetType.StoreApplication;
        }

        var extension =
            Path.GetExtension(
                targetPath);

        if (extension.Equals(
                ".exe",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".com",
                StringComparison.OrdinalIgnoreCase))
        {
            return StartupTargetType.Executable;
        }

        if (extension.Equals(
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
                StringComparison.OrdinalIgnoreCase))
        {
            return StartupTargetType.Script;
        }

        if (extension.Equals(
                ".dll",
                StringComparison.OrdinalIgnoreCase))
        {
            return StartupTargetType.DynamicLibrary;
        }

        return string.IsNullOrWhiteSpace(
            targetPath)
                ? StartupTargetType.Unknown
                : StartupTargetType.IndirectTarget;
    }

    private static string NormalizeLocalTargetPath(
        string targetPath)
    {
        if (string.IsNullOrWhiteSpace(
                targetPath))
        {
            return string.Empty;
        }

        var trimmedPath =
            targetPath
                .Trim()
                .Trim('"');

        if (IsNetworkPath(
                trimmedPath))
        {
            return string.Empty;
        }

        try
        {
            if (Path.IsPathFullyQualified(
                    trimmedPath))
            {
                return Path.GetFullPath(
                    trimmedPath);
            }

            var windowsDirectory =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.Windows);

            if (!string.IsNullOrWhiteSpace(
                    windowsDirectory))
            {
                var system32Candidate =
                    Path.Combine(
                        windowsDirectory,
                        "System32",
                        trimmedPath);

                if (File.Exists(
                        system32Candidate))
                {
                    return Path.GetFullPath(
                        system32Candidate);
                }

                var windowsCandidate =
                    Path.Combine(
                        windowsDirectory,
                        trimmedPath);

                if (File.Exists(
                        windowsCandidate))
                {
                    return Path.GetFullPath(
                        windowsCandidate);
                }
            }

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool? TryDetermineTargetExistence(
        string targetPath)
    {
        if (string.IsNullOrWhiteSpace(
                targetPath)
            || IsNetworkPath(
                targetPath))
        {
            return null;
        }

        try
        {
            var fileInformation =
                new FileInfo(
                    targetPath);

            if (!fileInformation.Exists)
            {
                return false;
            }

            if ((fileInformation.Attributes
                 & FileAttributes.ReparsePoint)
                != 0)
            {
                return null;
            }

            return true;
        }
        catch
        {
            return null;
        }
    }

    private static void ReadFileVersionInformation(
        string targetPath,
        StartupEntryInformation information)
    {
        try
        {
            var versionInformation =
                FileVersionInfo.GetVersionInfo(
                    targetPath);

            information.ProductName =
                versionInformation.ProductName
                    ?.Trim()
                ?? string.Empty;

            information.Publisher =
                versionInformation.CompanyName
                    ?.Trim()
                ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(
                    information.ProductName))
            {
                information.NormalizedProgramName =
                    information.ProductName;
            }
        }
        catch
        {
            // Fehlende Dateiversionsinformationen verhindern
            // nicht die übrige, rein lesende Analyse.
        }
    }

    private static void ApplyInitialClassification(
        StartupEntryInformation information,
        string targetPath)
    {
        if (information.State
            == StartupEntryState.Disabled)
        {
            information.Classification =
                StartupClassification.Disabled;

            return;
        }

        if (information.TargetExists == false)
        {
            information.Classification =
                StartupClassification.Conspicuous;

            return;
        }

        if (information.TargetExists is null)
        {
            information.Classification =
                StartupClassification.Unknown;

            return;
        }

        if (IsWindowsOwnedPath(
                targetPath))
        {
            information.Classification =
                StartupClassification.SystemOrDriverRelated;

            return;
        }

        if (!string.IsNullOrWhiteSpace(
                information.ProductName)
            || !string.IsNullOrWhiteSpace(
                information.Publisher))
        {
            information.Classification =
                StartupClassification.ProbablyUseful;

            return;
        }

        information.Classification =
            StartupClassification.Unknown;
    }

    private static bool IsWindowsOwnedPath(
        string targetPath)
    {
        var windowsDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

        if (string.IsNullOrWhiteSpace(
                windowsDirectory))
        {
            return false;
        }

        try
        {
            var normalizedWindowsDirectory =
                Path.GetFullPath(
                        windowsDirectory)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            var normalizedTargetPath =
                Path.GetFullPath(
                    targetPath);

            return normalizedTargetPath.StartsWith(
                normalizedWindowsDirectory,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string BuildDescription(
        StartupEntryInformation information)
    {
        if (information.State
            == StartupEntryState.Disabled)
        {
            return information.TargetExists == false
                ? "Der Autostarteintrag ist deaktiviert. "
                  + "Das hinterlegte lokale Ziel wurde nicht gefunden."
                : "Der Autostarteintrag ist deaktiviert und "
                  + "wird nur zu Informationszwecken angezeigt.";
        }

        if (information.TargetExists == false)
        {
            return "Der Autostarteintrag ist aktiv, das hinterlegte "
                   + "lokale Programmziel wurde jedoch nicht gefunden. "
                   + "Der Eintrag sollte manuell geprüft werden.";
        }

        if (information.TargetExists is null)
        {
            return "Das Startziel konnte ohne Ausführung oder "
                   + "weitergehende Auflösung nicht sicher geprüft werden.";
        }

        if (information.Classification
            == StartupClassification.SystemOrDriverRelated)
        {
            return "Das lokale Startziel liegt im Windows-Verzeichnis. "
                   + "Der Eintrag wird deshalb vorsichtig als system- "
                   + "oder treibernah eingeordnet.";
        }

        if (information.Classification
            == StartupClassification.ProbablyUseful)
        {
            return "Das lokale Startziel ist vorhanden und enthält "
                   + "auswertbare Produkt- oder Herstellerinformationen. "
                   + "Dies ist kein Nachweis einer digitalen Signatur.";
        }

        return "Das lokale Startziel ist vorhanden. Eine weitergehende "
               + "fachliche Einordnung war nicht eindeutig möglich.";
    }

    private static string BuildIndirectTargetDescription(
        StartupTargetType targetType,
        StartupEntryState state)
    {
        var statusText =
            state == StartupEntryState.Disabled
                ? "Der Eintrag ist deaktiviert. "
                : string.Empty;

        return targetType switch
        {
            StartupTargetType.PowerShell =>
                statusText
                + "Der Eintrag verwendet PowerShell. "
                + "Der hinterlegte Befehl wurde nicht ausgeführt "
                + "und nicht dauerhaft gespeichert.",

            StartupTargetType.CommandInterpreter =>
                statusText
                + "Der Eintrag verwendet den Windows-Befehlsinterpreter. "
                + "Der hinterlegte Befehl wurde nicht ausgeführt "
                + "und nicht dauerhaft gespeichert.",

            StartupTargetType.DynamicLibrary =>
                statusText
                + "Der Eintrag verwendet einen indirekten DLL-Aufruf. "
                + "Die Zielbibliothek wurde nicht ausgeführt.",

            StartupTargetType.StoreApplication =>
                statusText
                + "Der Eintrag verweist auf eine Windows- oder "
                + "Store-Anwendung. Das Ziel wurde nicht gestartet.",

            _ =>
                statusText
                + "Das Startziel konnte ohne Ausführung nicht "
                + "eindeutig bestimmt werden."
        };
    }

    private static string CreateSourceIdentifier(
        StartupSourceType sourceType,
        StartupEntryContext context,
        StartupRegistryView registryView,
        string entryName)
    {
        var identifierSource =
            $"{sourceType}|{context}|{registryView}|"
            + entryName.Trim().ToUpperInvariant();

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
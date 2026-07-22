using System.IO;

namespace WeberIT.Checkup.App.Services.Cleanup;

internal static class BrowserCacheTargetResolver
{
    public static BrowserCacheTargetResolution Resolve()
    {
        if (!OperatingSystem.IsWindows())
        {
            return BrowserCacheTargetResolution.Failed(
                "Browsercachebereiche können nur unter "
                + "Windows sicher bestimmt werden.");
        }

        var localApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(
                localApplicationData))
        {
            return BrowserCacheTargetResolution.Failed(
                "Der lokale Anwendungsdatenordner des "
                + "angemeldeten Benutzers konnte nicht "
                + "bestimmt werden.");
        }

        string normalizedLocalApplicationData;

        try
        {
            normalizedLocalApplicationData =
                NormalizeDirectoryPath(
                    localApplicationData);
        }
        catch (Exception exception)
        {
            return BrowserCacheTargetResolution.Failed(
                "Der lokale Anwendungsdatenordner konnte "
                + "nicht sicher normalisiert werden. "
                + exception.Message);
        }

        if (IsNetworkPath(
                normalizedLocalApplicationData))
        {
            return BrowserCacheTargetResolution.Failed(
                "Browsercachebereiche auf einem Netzwerkpfad "
                + "sind nicht für die automatische "
                + "Bereinigung freigegeben.");
        }

        var targets =
            new List<BrowserCacheTarget>();

        var discoveryHadErrors =
            false;

        AddChromiumTargets(
            "Microsoft Edge",
            Path.Combine(
                normalizedLocalApplicationData,
                "Microsoft",
                "Edge",
                "User Data"),
            targets,
            ref discoveryHadErrors);

        AddChromiumTargets(
            "Google Chrome",
            Path.Combine(
                normalizedLocalApplicationData,
                "Google",
                "Chrome",
                "User Data"),
            targets,
            ref discoveryHadErrors);

        AddFirefoxTargets(
            Path.Combine(
                normalizedLocalApplicationData,
                "Mozilla",
                "Firefox",
                "Profiles"),
            targets,
            ref discoveryHadErrors);

        var distinctTargets =
            targets
                .GroupBy(
                    target =>
                        target.Path,
                    StringComparer.OrdinalIgnoreCase)
                .Select(
                    group =>
                        group.First())
                .OrderBy(
                    target =>
                        target.BrowserName,
                    StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(
                    target =>
                        target.Path,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        return new BrowserCacheTargetResolution
        {
            LocalApplicationDataPath =
                normalizedLocalApplicationData,

            Targets =
                distinctTargets,

            HadDiscoveryErrors =
                discoveryHadErrors
        };
    }

    private static void AddChromiumTargets(
        string browserName,
        string userDataPath,
        ICollection<BrowserCacheTarget> targets,
        ref bool discoveryHadErrors)
    {
        if (!Directory.Exists(
                userDataPath))
        {
            return;
        }

        targets.Add(
            new BrowserCacheTarget(
                browserName,
                Path.Combine(
                    userDataPath,
                    "GrShaderCache")));

        targets.Add(
            new BrowserCacheTarget(
                browserName,
                Path.Combine(
                    userDataPath,
                    "ShaderCache")));

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
                if (!TryReadAttributes(
                        profileDirectory,
                        out var attributes))
                {
                    discoveryHadErrors =
                        true;

                    continue;
                }

                if ((attributes
                     & FileAttributes.ReparsePoint)
                    != 0)
                {
                    discoveryHadErrors =
                        true;

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

                targets.Add(
                    new BrowserCacheTarget(
                        browserName,
                        Path.Combine(
                            profileDirectory,
                            "Cache")));

                targets.Add(
                    new BrowserCacheTarget(
                        browserName,
                        Path.Combine(
                            profileDirectory,
                            "Code Cache")));

                targets.Add(
                    new BrowserCacheTarget(
                        browserName,
                        Path.Combine(
                            profileDirectory,
                            "GPUCache")));
            }
        }
        catch
        {
            discoveryHadErrors =
                true;
        }
    }

    private static void AddFirefoxTargets(
        string profilesPath,
        ICollection<BrowserCacheTarget> targets,
        ref bool discoveryHadErrors)
    {
        if (!Directory.Exists(
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
                if (!TryReadAttributes(
                        profileDirectory,
                        out var attributes))
                {
                    discoveryHadErrors =
                        true;

                    continue;
                }

                if ((attributes
                     & FileAttributes.ReparsePoint)
                    != 0)
                {
                    discoveryHadErrors =
                        true;

                    continue;
                }

                targets.Add(
                    new BrowserCacheTarget(
                        "Mozilla Firefox",
                        Path.Combine(
                            profileDirectory,
                            "cache2")));

                targets.Add(
                    new BrowserCacheTarget(
                        "Mozilla Firefox",
                        Path.Combine(
                            profileDirectory,
                            "startupCache")));
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

    private static bool TryReadAttributes(
        string path,
        out FileAttributes attributes)
    {
        try
        {
            attributes =
                File.GetAttributes(
                    path);

            return true;
        }
        catch
        {
            attributes =
                default;

            return false;
        }
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
                "Der Pfad besitzt kein eindeutiges "
                + "lokales Stammverzeichnis.");
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
}

internal sealed record BrowserCacheTarget(
    string BrowserName,
    string Path);

internal sealed class BrowserCacheTargetResolution
{
    public string LocalApplicationDataPath { get; init; } =
        string.Empty;

    public IReadOnlyList<BrowserCacheTarget> Targets
    {
        get;
        init;
    } = Array.Empty<BrowserCacheTarget>();

    public bool HadDiscoveryErrors { get; init; }

    public string ErrorMessage { get; init; } =
        string.Empty;

    public bool IsAvailable =>
        string.IsNullOrWhiteSpace(
            ErrorMessage)
        && !string.IsNullOrWhiteSpace(
            LocalApplicationDataPath);

    public static BrowserCacheTargetResolution Failed(
        string errorMessage)
    {
        return new BrowserCacheTargetResolution
        {
            ErrorMessage =
                errorMessage
        };
    }
}
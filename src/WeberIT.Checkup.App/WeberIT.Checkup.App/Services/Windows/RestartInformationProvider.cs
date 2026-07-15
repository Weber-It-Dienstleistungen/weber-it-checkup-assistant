using Microsoft.Win32;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Windows;

public class RestartInformationProvider :
    IRestartInformationProvider
{
    private const string ComponentBasedServicingPath =
        @"SOFTWARE\Microsoft\Windows"
        + @"\CurrentVersion\Component Based Servicing"
        + @"\RebootPending";

    private const string WindowsUpdatePath =
        @"SOFTWARE\Microsoft\Windows"
        + @"\CurrentVersion\WindowsUpdate"
        + @"\Auto Update\RebootRequired";

    private const string SessionManagerPath =
        @"SYSTEM\CurrentControlSet\Control\Session Manager";

    private const string ActiveComputerNamePath =
        @"SYSTEM\CurrentControlSet\Control"
        + @"\ComputerName\ActiveComputerName";

    private const string ConfiguredComputerNamePath =
        @"SYSTEM\CurrentControlSet\Control"
        + @"\ComputerName\ComputerName";

    public RestartInformation GetRestartInformation()
    {
        var information =
            new RestartInformation
            {
                IsAnalysisPerformed = true,
                AnalysisDate = DateTime.Now
            };

        information.Sources.Add(
            ReadRegistryKeyIndicator(
                RestartSourceType.WindowsUpdate,
                "Windows Update",
                WindowsUpdatePath,
                "Windows Update meldet einen "
                + "ausstehenden Neustart.",
                "Windows Update meldet derzeit "
                + "keinen ausstehenden Neustart."));

        information.Sources.Add(
            ReadRegistryKeyIndicator(
                RestartSourceType.ComponentBasedServicing,
                "Windows-Komponentenwartung",
                ComponentBasedServicingPath,
                "Die Windows-Komponentenwartung meldet "
                + "einen ausstehenden Neustart.",
                "Die Windows-Komponentenwartung meldet "
                + "derzeit keinen ausstehenden Neustart."));

        information.Sources.Add(
            ReadPendingFileRenameOperations());

        information.Sources.Add(
            ReadPendingComputerRename());

        ApplyOverallResult(information);

        return information;
    }

    private static RestartSourceResult ReadRegistryKeyIndicator(
        RestartSourceType sourceType,
        string displayName,
        string subKeyName,
        string restartRequiredDetails,
        string noRestartRequiredDetails)
    {
        try
        {
            using var baseKey =
                RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    RegistryView.Registry64);

            using var key =
                baseKey.OpenSubKey(subKeyName);

            var restartRequired =
                key is not null;

            return new RestartSourceResult
            {
                SourceType = sourceType,
                DisplayName = displayName,
                IsCheckSuccessful = true,
                IsRestartRequired = restartRequired,
                Details =
                    restartRequired
                        ? restartRequiredDetails
                        : noRestartRequiredDetails
            };
        }
        catch (Exception exception)
        {
            return CreateFailedResult(
                sourceType,
                displayName,
                exception);
        }
    }

    private static RestartSourceResult
        ReadPendingFileRenameOperations()
    {
        const string displayName =
            "Ausstehende Dateioperationen";

        try
        {
            using var baseKey =
                RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    RegistryView.Registry64);

            using var key =
                baseKey.OpenSubKey(
                    SessionManagerPath);

            if (key is null)
            {
                return new RestartSourceResult
                {
                    SourceType =
                        RestartSourceType
                            .PendingFileRenameOperations,

                    DisplayName =
                        displayName,

                    IsCheckSuccessful =
                        false,

                    IsRestartRequired =
                        null,

                    Details =
                        "Der Windows-Sitzungsmanager "
                        + "konnte nicht ausgewertet werden."
                };
            }

            var pendingOperations =
                key.GetValue(
                    "PendingFileRenameOperations");

            var restartRequired =
                HasPendingFileOperations(
                    pendingOperations);

            return new RestartSourceResult
            {
                SourceType =
                    RestartSourceType
                        .PendingFileRenameOperations,

                DisplayName =
                    displayName,

                IsCheckSuccessful =
                    true,

                IsRestartRequired =
                    restartRequired,

                Details =
                    restartRequired
                        ? "Windows hat Dateioperationen "
                          + "für einen kommenden Systemstart "
                          + "vorgemerkt. Dieser Hinweis allein "
                          + "beweist keinen zwingend "
                          + "erforderlichen Neustart."
                        : "Es wurden keine für einen kommenden "
                          + "Systemstart vorgemerkten "
                          + "Dateioperationen erkannt."
            };
        }
        catch (Exception exception)
        {
            return CreateFailedResult(
                RestartSourceType
                    .PendingFileRenameOperations,
                displayName,
                exception);
        }
    }

    private static bool HasPendingFileOperations(
        object? registryValue)
    {
        if (registryValue is string[] stringValues)
        {
            return stringValues.Any(
                value =>
                    !string.IsNullOrWhiteSpace(value));
        }

        if (registryValue is string stringValue)
        {
            return !string.IsNullOrWhiteSpace(
                stringValue);
        }

        return registryValue is not null;
    }

    private static RestartSourceResult
        ReadPendingComputerRename()
    {
        const string displayName =
            "Ausstehende Computername-Änderung";

        try
        {
            using var baseKey =
                RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    RegistryView.Registry64);

            using var activeComputerNameKey =
                baseKey.OpenSubKey(
                    ActiveComputerNamePath);

            using var configuredComputerNameKey =
                baseKey.OpenSubKey(
                    ConfiguredComputerNamePath);

            if (activeComputerNameKey is null
                || configuredComputerNameKey is null)
            {
                return new RestartSourceResult
                {
                    SourceType =
                        RestartSourceType
                            .PendingComputerRename,

                    DisplayName =
                        displayName,

                    IsCheckSuccessful =
                        false,

                    IsRestartRequired =
                        null,

                    Details =
                        "Der aktive oder konfigurierte "
                        + "Computername konnte nicht "
                        + "ausgewertet werden."
                };
            }

            var activeComputerName =
                activeComputerNameKey
                    .GetValue("ComputerName")
                    ?.ToString()
                    ?.Trim()
                ?? string.Empty;

            var configuredComputerName =
                configuredComputerNameKey
                    .GetValue("ComputerName")
                    ?.ToString()
                    ?.Trim()
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(
                    activeComputerName)
                || string.IsNullOrWhiteSpace(
                    configuredComputerName))
            {
                return new RestartSourceResult
                {
                    SourceType =
                        RestartSourceType
                            .PendingComputerRename,

                    DisplayName =
                        displayName,

                    IsCheckSuccessful =
                        false,

                    IsRestartRequired =
                        null,

                    Details =
                        "Der aktive oder konfigurierte "
                        + "Computername war nicht eindeutig "
                        + "verfügbar."
                };
            }

            var restartRequired =
                !activeComputerName.Equals(
                    configuredComputerName,
                    StringComparison.OrdinalIgnoreCase);

            return new RestartSourceResult
            {
                SourceType =
                    RestartSourceType
                        .PendingComputerRename,

                DisplayName =
                    displayName,

                IsCheckSuccessful =
                    true,

                IsRestartRequired =
                    restartRequired,

                Details =
                    restartRequired
                        ? "Eine Änderung des Computernamens "
                          + "wird erst nach einem Neustart "
                          + "vollständig wirksam."
                        : "Es wurde keine ausstehende "
                          + "Computername-Änderung erkannt."
            };
        }
        catch (Exception exception)
        {
            return CreateFailedResult(
                RestartSourceType
                    .PendingComputerRename,
                displayName,
                exception);
        }
    }

    private static RestartSourceResult CreateFailedResult(
        RestartSourceType sourceType,
        string displayName,
        Exception exception)
    {
        var errorDetails =
            string.IsNullOrWhiteSpace(
                exception.Message)
                ? "Keine weiteren Fehlerdetails verfügbar."
                : exception.Message;

        return new RestartSourceResult
        {
            SourceType =
                sourceType,

            DisplayName =
                displayName,

            IsCheckSuccessful =
                false,

            IsRestartRequired =
                null,

            Details =
                "Die Neustartquelle konnte nicht "
                + "zuverlässig ausgewertet werden. "
                + $"Technische Details: {errorDetails}"
        };
    }

    private static void ApplyOverallResult(
        RestartInformation information)
    {
        var authoritativeSources =
            information.Sources
                .Where(IsAuthoritativeSource)
                .ToList();

        var confirmedRestartRequired =
            authoritativeSources.Any(
                source =>
                    source.IsCheckSuccessful
                    && source.IsRestartRequired == true);

        if (confirmedRestartRequired)
        {
            information.IsAnalysisConclusive =
                true;

            information.IsRestartRequired =
                true;

            return;
        }

        var advisoryRestartHint =
            information.Sources.Any(
                source =>
                    !IsAuthoritativeSource(source)
                    && source.IsCheckSuccessful
                    && source.IsRestartRequired == true);

        if (advisoryRestartHint)
        {
            information.IsAnalysisConclusive =
                false;

            information.IsRestartRequired =
                null;

            return;
        }

        var allChecksSuccessful =
            information.Sources.Count > 0
            && information.Sources.All(
                source =>
                    source.IsCheckSuccessful
                    && source.IsRestartRequired.HasValue);

        if (allChecksSuccessful)
        {
            information.IsAnalysisConclusive =
                true;

            information.IsRestartRequired =
                false;

            return;
        }

        information.IsAnalysisConclusive =
            false;

        information.IsRestartRequired =
            null;
    }

    private static bool IsAuthoritativeSource(
        RestartSourceResult source)
    {
        return source.SourceType
            is RestartSourceType.WindowsUpdate
            or RestartSourceType.ComponentBasedServicing
            or RestartSourceType.PendingComputerRename;
    }
}
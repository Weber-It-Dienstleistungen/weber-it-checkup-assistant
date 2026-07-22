using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Tasks;

public sealed class GuidedTaskActionLauncher :
    IGuidedTaskActionLauncher
{
    private static readonly IReadOnlyDictionary<
        string,
        GuidedLaunchTarget>
        LaunchTargets =
            CreateLaunchTargets();

    public bool CanLaunch(
        string actionCode)
    {
        return !string.IsNullOrWhiteSpace(
                   actionCode)
               && LaunchTargets.ContainsKey(
                   actionCode);
    }

    public string GetTargetDescription(
        string actionCode)
    {
        return GetLaunchTarget(
                actionCode)
            .Description;
    }

    public void Launch(
        string actionCode)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Geführte Windows-Prüfansichten können nur "
                + "unter Windows geöffnet werden.");
        }

        var launchTarget =
            GetLaunchTarget(
                actionCode);

        ValidateLaunchTarget(
            launchTarget);

        var startInfo =
            new ProcessStartInfo
            {
                FileName =
                    launchTarget.FileName,

                Arguments =
                    launchTarget.Arguments,

                UseShellExecute =
                    true,

                ErrorDialog =
                    false
            };

        try
        {
            Process.Start(
                startInfo);
        }
        catch (Exception exception)
            when (exception
                  is Win32Exception
                  or FileNotFoundException
                  or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Die geführte Prüfansicht „"
                + launchTarget.Description
                + "“ konnte nicht geöffnet werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Technische Ursache: "
                + exception.Message,
                exception);
        }
    }

    private static GuidedLaunchTarget
        GetLaunchTarget(
            string actionCode)
    {
        if (string.IsNullOrWhiteSpace(
                actionCode))
        {
            throw new ArgumentException(
                "Für die geführte Prüfung ist ein stabiler "
                + "Aktionscode erforderlich.",
                nameof(actionCode));
        }

        if (LaunchTargets.TryGetValue(
                actionCode,
                out var launchTarget))
        {
            return launchTarget;
        }

        throw new InvalidOperationException(
            "Für den Aktionscode „"
            + actionCode
            + "“ ist keine geführte Windows-Prüfansicht "
            + "freigegeben.");
    }

    private static void ValidateLaunchTarget(
        GuidedLaunchTarget launchTarget)
    {
        if (string.IsNullOrWhiteSpace(
                launchTarget.FileName))
        {
            throw new InvalidOperationException(
                "Die geführte Prüfung besitzt kein "
                + "gültiges Startziel.");
        }

        if (launchTarget.IsUri)
        {
            if (!Uri.TryCreate(
                    launchTarget.FileName,
                    UriKind.Absolute,
                    out var targetUri)
                || !string.Equals(
                    targetUri.Scheme,
                    "ms-settings",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Die geführte Prüfung enthält kein "
                    + "freigegebenes Windows-Einstellungsziel.");
            }

            return;
        }

        if (!Path.IsPathFullyQualified(
                launchTarget.FileName))
        {
            throw new InvalidOperationException(
                "Das lokale Startziel der geführten Prüfung "
                + "ist kein vollständig qualifizierter Pfad.");
        }

        if (!File.Exists(
                launchTarget.FileName))
        {
            throw new FileNotFoundException(
                "Das benötigte Windows-Verwaltungswerkzeug "
                + "ist auf diesem System nicht vorhanden.",
                launchTarget.FileName);
        }
    }

    private static IReadOnlyDictionary<
        string,
        GuidedLaunchTarget>
        CreateLaunchTargets()
    {
        var systemDirectory =
            Environment.SystemDirectory;

        if (string.IsNullOrWhiteSpace(
                systemDirectory))
        {
            systemDirectory =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.Windows),
                    "System32");
        }

        var controlPanelPath =
            Path.Combine(
                systemDirectory,
                "control.exe");

        var userAccountControlPath =
            Path.Combine(
                systemDirectory,
                "UserAccountControlSettings.exe");

        var servicesConsolePath =
            Path.Combine(
                systemDirectory,
                "services.msc");

        var deviceManagerPath =
            Path.Combine(
                systemDirectory,
                "devmgmt.msc");

        var systemInformationPath =
            Path.Combine(
                systemDirectory,
                "msinfo32.exe");

        var tpmManagementPath =
            Path.Combine(
                systemDirectory,
                "tpm.msc");

        var diskManagementPath =
            Path.Combine(
                systemDirectory,
                "diskmgmt.msc");

        return new Dictionary<
            string,
            GuidedLaunchTarget>(
            StringComparer.Ordinal)
        {
            ["action.security.antivirus-review"] =
                new GuidedLaunchTarget(
                    "ms-settings:windowsdefender",
                    string.Empty,
                    "Windows-Sicherheit",
                    IsUri: true),

            ["action.security.antivirus-registration-review"] =
                new GuidedLaunchTarget(
                    "ms-settings:windowsdefender",
                    string.Empty,
                    "Windows-Sicherheit und registrierte Schutzbereiche",
                    IsUri: true),

            ["action.security.firewall-review"] =
                new GuidedLaunchTarget(
                    controlPanelPath,
                    "/name Microsoft.WindowsFirewall",
                    "Windows-Firewall",
                    IsUri: false),

            ["action.security.uac-review"] =
                new GuidedLaunchTarget(
                    userAccountControlPath,
                    string.Empty,
                    "Einstellungen der Benutzerkontensteuerung",
                    IsUri: false),

            ["action.security.security-center-review"] =
                new GuidedLaunchTarget(
                    servicesConsolePath,
                    string.Empty,
                    "Windows-Diensteverwaltung",
                    IsUri: false),

            ["action.security.drive-encryption-review"] =
                new GuidedLaunchTarget(
                    controlPanelPath,
                    "/name Microsoft.BitLockerDriveEncryption",
                    "BitLocker-Laufwerkverschlüsselung",
                    IsUri: false),

            ["action.security.secure-boot-review"] =
                new GuidedLaunchTarget(
                    systemInformationPath,
                    string.Empty,
                    "Windows-Systeminformationen",
                    IsUri: false),

            ["action.security.tpm-review"] =
                new GuidedLaunchTarget(
                    tpmManagementPath,
                    string.Empty,
                    "Lokale TPM-Verwaltung",
                    IsUri: false),

            ["action.operating-system.version-review"] =
                new GuidedLaunchTarget(
                    "ms-settings:about",
                    string.Empty,
                    "Windows-Systeminformationen",
                    IsUri: true),

            ["action.windows-update.guided-review"] =
                new GuidedLaunchTarget(
                    "ms-settings:windowsupdate-history",
                    string.Empty,
                    "Windows-Updateverlauf",
                    IsUri: true),

            ["action.windows-update.service-review"] =
                new GuidedLaunchTarget(
                    servicesConsolePath,
                    string.Empty,
                    "Windows-Diensteverwaltung",
                    IsUri: false),

            ["action.startup.guided-review"] =
                new GuidedLaunchTarget(
                    "ms-settings:startupapps",
                    string.Empty,
                    "Windows-Autostarteinstellungen",
                    IsUri: true),

            ["action.devices.missing-driver-review"] =
                new GuidedLaunchTarget(
                    deviceManagerPath,
                    string.Empty,
                    "Windows-Geräte-Manager",
                    IsUri: false),

            ["action.devices.windows-problem-review"] =
                new GuidedLaunchTarget(
                    deviceManagerPath,
                    string.Empty,
                    "Windows-Geräte-Manager",
                    IsUri: false),

            ["action.devices.unsigned-driver-review"] =
                new GuidedLaunchTarget(
                    deviceManagerPath,
                    string.Empty,
                    "Windows-Geräte-Manager",
                    IsUri: false),

            ["action.storage.disk-management-review"] =
                new GuidedLaunchTarget(
                    diskManagementPath,
                    string.Empty,
                    "Windows-Datenträgerverwaltung",
                    IsUri: false),

            ["action.hardware.storage-review"] =
                new GuidedLaunchTarget(
                    diskManagementPath,
                    string.Empty,
                    "Windows-Datenträgerverwaltung",
                    IsUri: false),

            ["action.hardware.memory-review"] =
                new GuidedLaunchTarget(
                    systemInformationPath,
                    string.Empty,
                    "Windows-Systeminformationen",
                    IsUri: false),

            ["action.hardware.tpm-review"] =
                new GuidedLaunchTarget(
                    tpmManagementPath,
                    string.Empty,
                    "Lokale TPM-Verwaltung",
                    IsUri: false)
        };
    }

    private sealed record GuidedLaunchTarget(
        string FileName,
        string Arguments,
        string Description,
        bool IsUri);
}
using System.IO;
using Microsoft.Win32;
using System.Management;
using System.Runtime.InteropServices;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Security;

public class SecurityInformationProvider : ISecurityInformationProvider
{
    private const int DomainFirewallProfile = 1;
    private const int PrivateFirewallProfile = 2;
    private const int PublicFirewallProfile = 4;

    private const int AntivirusSecurityProvider = 4;

    public SecurityInformation GetSecurityInformation()
    {
        return new SecurityInformation
        {
            AntivirusProducts =
                GetAntivirusProducts(),

            AntivirusStatus =
                GetAntivirusStatus(
                    out var antivirusStatusDetails),

            AntivirusStatusDetails =
                antivirusStatusDetails,

            FirewallProfiles =
                GetFirewallProfiles(),

            SystemDriveEncryption =
                GetSystemDriveEncryption(),

            UserAccountControlStatus =
                GetUserAccountControlStatus(),

            SecureBootStatus =
                GetSecureBootStatus(),

            WindowsSecurityCenterStatus =
                GetWindowsSecurityCenterStatus()
        };
    }

    private static List<AntivirusProductInformation>
        GetAntivirusProducts()
    {
        var products =
            new List<AntivirusProductInformation>();

        try
        {
            var scope = new ManagementScope(
                @"\\.\root\SecurityCenter2");

            scope.Connect();

            using var searcher =
                new ManagementObjectSearcher(
                    scope,
                    new ObjectQuery(
                        "SELECT displayName, productState, "
                        + "pathToSignedProductExe "
                        + "FROM AntiVirusProduct"));

            foreach (var result in searcher.Get())
            {
                var displayName =
                    result["displayName"]
                        ?.ToString()
                        ?.Trim();

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    continue;
                }

                uint? productState = null;

                if (uint.TryParse(
                        result["productState"]?.ToString(),
                        out var parsedProductState))
                {
                    productState =
                        parsedProductState;
                }

                var productPath =
                    result["pathToSignedProductExe"]
                        ?.ToString()
                        ?.Trim()
                    ?? string.Empty;

                products.Add(
                    new AntivirusProductInformation
                    {
                        DisplayName =
                            displayName,

                        ProductState =
                            productState,

                        ProductPath =
                            productPath
                    });
            }
        }
        catch
        {
            // Ein Fehler beim Windows-Sicherheitscenter
            // darf den gesamten Checkup nicht abbrechen.
        }

        return products
            .GroupBy(
                product => product.DisplayName,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(product => product.DisplayName)
            .ToList();
    }

    private static SecurityState GetAntivirusStatus(
        out string statusDetails)
    {
        try
        {
            var result =
                WscGetSecurityProviderHealth(
                    AntivirusSecurityProvider,
                    out var health);

            if (result != 0)
            {
                statusDetails =
                    $"Windows Security Center lieferte "
                    + $"den Fehlercode 0x{result:X8}.";

                return SecurityState.Unknown;
            }

            switch (health)
            {
                case SecurityProviderHealth.Good:
                    statusDetails =
                        "Windows meldet einen ordnungsgemäßen "
                        + "Virenschutzstatus.";

                    return SecurityState.Enabled;

                case SecurityProviderHealth.Poor:
                    statusDetails =
                        "Windows meldet beim Virenschutz "
                        + "einen Zustand mit Handlungsbedarf.";

                    return SecurityState.Disabled;

                case SecurityProviderHealth.Snooze:
                    statusDetails =
                        "Der Virenschutz wurde laut Windows "
                        + "vorübergehend ausgesetzt.";

                    return SecurityState.Unknown;

                case SecurityProviderHealth.NotMonitored:
                    statusDetails =
                        "Der Virenschutzstatus wird von Windows "
                        + "derzeit nicht überwacht.";

                    return SecurityState.Unknown;

                default:
                    statusDetails =
                        "Windows lieferte einen unbekannten "
                        + "Virenschutzstatus.";

                    return SecurityState.Unknown;
            }
        }
        catch (Exception exception)
        {
            statusDetails =
                "Virenschutzstatus nicht ermittelbar: "
                + exception.Message;

            return SecurityState.Unknown;
        }
    }

    private static List<FirewallProfileInformation>
        GetFirewallProfiles()
    {
        var profiles =
            new List<FirewallProfileInformation>();

        object? firewallPolicy = null;

        try
        {
            var firewallPolicyType =
                Type.GetTypeFromProgID(
                    "HNetCfg.FwPolicy2");

            if (firewallPolicyType is null)
            {
                return CreateUnknownFirewallProfiles();
            }

            firewallPolicy =
                Activator.CreateInstance(
                    firewallPolicyType);

            if (firewallPolicy is null)
            {
                return CreateUnknownFirewallProfiles();
            }

            dynamic policy =
                firewallPolicy;

            var activeProfileTypes =
                (int)policy.CurrentProfileTypes;

            profiles.Add(
                CreateFirewallProfile(
                    policy,
                    "Domänennetzwerk",
                    DomainFirewallProfile,
                    activeProfileTypes));

            profiles.Add(
                CreateFirewallProfile(
                    policy,
                    "Privates Netzwerk",
                    PrivateFirewallProfile,
                    activeProfileTypes));

            profiles.Add(
                CreateFirewallProfile(
                    policy,
                    "Öffentliches Netzwerk",
                    PublicFirewallProfile,
                    activeProfileTypes));
        }
        catch
        {
            return CreateUnknownFirewallProfiles();
        }
        finally
        {
            if (firewallPolicy is not null
                && Marshal.IsComObject(firewallPolicy))
            {
                Marshal.FinalReleaseComObject(
                    firewallPolicy);
            }
        }

        return profiles;
    }

    private static FirewallProfileInformation
        CreateFirewallProfile(
            dynamic policy,
            string profileName,
            int profileType,
            int activeProfileTypes)
    {
        try
        {
            var isEnabled =
                (bool)policy.FirewallEnabled[profileType];

            return new FirewallProfileInformation
            {
                ProfileName =
                    profileName,

                IsActive =
                    (activeProfileTypes & profileType)
                    == profileType,

                State =
                    isEnabled
                        ? SecurityState.Enabled
                        : SecurityState.Disabled
            };
        }
        catch
        {
            return new FirewallProfileInformation
            {
                ProfileName =
                    profileName,

                IsActive =
                    (activeProfileTypes & profileType)
                    == profileType,

                State =
                    SecurityState.Unknown
            };
        }
    }

    private static List<FirewallProfileInformation>
        CreateUnknownFirewallProfiles()
    {
        return new List<FirewallProfileInformation>
        {
            new()
            {
                ProfileName =
                    "Domänennetzwerk",

                State =
                    SecurityState.Unknown
            },
            new()
            {
                ProfileName =
                    "Privates Netzwerk",

                State =
                    SecurityState.Unknown
            },
            new()
            {
                ProfileName =
                    "Öffentliches Netzwerk",

                State =
                    SecurityState.Unknown
            }
        };
    }

    private static DriveEncryptionInformation
        GetSystemDriveEncryption()
    {
        var systemDrive =
            Path.GetPathRoot(
                Environment.SystemDirectory)
            ?.TrimEnd('\\')
            ?? string.Empty;

        var encryptionInformation =
            new DriveEncryptionInformation
            {
                DriveLetter =
                    systemDrive,

                ProtectionState =
                    SecurityState.Unknown,

                ConversionStatus =
                    "Nicht ermittelbar"
            };

        try
        {
            var scope =
                new ManagementScope(
                    @"\\.\root\CIMV2\Security"
                    + @"\MicrosoftVolumeEncryption");

            scope.Connect();

            using var searcher =
                new ManagementObjectSearcher(
                    scope,
                    new ObjectQuery(
                        "SELECT DriveLetter, "
                        + "ProtectionStatus, "
                        + "ConversionStatus, "
                        + "EncryptionPercentage "
                        + "FROM Win32_EncryptableVolume"));

            foreach (var result in searcher.Get())
            {
                var driveLetter =
                    result["DriveLetter"]
                        ?.ToString()
                        ?.Trim()
                    ?? string.Empty;

                if (!driveLetter.Equals(
                        systemDrive,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                encryptionInformation.DriveLetter =
                    driveLetter;

                encryptionInformation.ProtectionState =
                    MapProtectionState(
                        result["ProtectionStatus"]);

                encryptionInformation.ConversionStatus =
                    MapConversionStatus(
                        result["ConversionStatus"]);

                if (int.TryParse(
                        result["EncryptionPercentage"]
                            ?.ToString(),
                        out var encryptionPercentage))
                {
                    encryptionInformation
                            .EncryptionPercentage =
                        encryptionPercentage;
                }

                return encryptionInformation;
            }
        }
        catch (ManagementException exception)
            when (exception.ErrorCode
                  == ManagementStatus.InvalidNamespace
                  || exception.ErrorCode
                  == ManagementStatus.InvalidClass)
        {
            encryptionInformation.ProtectionState =
                SecurityState.NotSupported;

            encryptionInformation.ConversionStatus =
                "Nicht verfügbar";
        }
        catch
        {
            // Ein Fehler bei der Verschlüsselungsabfrage
            // darf den gesamten Checkup nicht abbrechen.
        }

        return encryptionInformation;
    }

    private static SecurityState MapProtectionState(
        object? protectionStatusValue)
    {
        if (!uint.TryParse(
                protectionStatusValue?.ToString(),
                out var protectionStatus))
        {
            return SecurityState.Unknown;
        }

        return protectionStatus switch
        {
            0 => SecurityState.Disabled,
            1 => SecurityState.Enabled,
            _ => SecurityState.Unknown
        };
    }

    private static string MapConversionStatus(
        object? conversionStatusValue)
    {
        if (!uint.TryParse(
                conversionStatusValue?.ToString(),
                out var conversionStatus))
        {
            return "Nicht ermittelbar";
        }

        return conversionStatus switch
        {
            0 => "Vollständig entschlüsselt",
            1 => "Vollständig verschlüsselt",
            2 => "Verschlüsselung läuft",
            3 => "Entschlüsselung läuft",
            4 => "Verschlüsselung angehalten",
            5 => "Entschlüsselung angehalten",
            _ => "Unbekannter Zustand"
        };
    }

    private static SecurityState
        GetUserAccountControlStatus()
    {
        try
        {
            using var key =
                Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows"
                    + @"\CurrentVersion\Policies\System");

            var enableLuaValue =
                key?.GetValue("EnableLUA");

            if (enableLuaValue is null)
            {
                return SecurityState.Unknown;
            }

            var numericValue =
                Convert.ToInt32(enableLuaValue);

            return numericValue == 0
                ? SecurityState.Disabled
                : SecurityState.Enabled;
        }
        catch
        {
            return SecurityState.Unknown;
        }
    }

    private static SecurityState GetSecureBootStatus()
    {
        try
        {
            if (!GetFirmwareType(
                    out var firmwareType))
            {
                return SecurityState.Unknown;
            }

            if (firmwareType == FirmwareType.Bios)
            {
                return SecurityState.NotSupported;
            }

            if (firmwareType != FirmwareType.Uefi)
            {
                return SecurityState.Unknown;
            }

            using var key =
                Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet"
                    + @"\Control\SecureBoot\State");

            var secureBootValue =
                key?.GetValue(
                    "UEFISecureBootEnabled");

            if (secureBootValue is null)
            {
                return SecurityState.Unknown;
            }

            var numericValue =
                Convert.ToInt32(secureBootValue);

            return numericValue switch
            {
                1 => SecurityState.Enabled,
                0 => SecurityState.Disabled,
                _ => SecurityState.Unknown
            };
        }
        catch
        {
            return SecurityState.Unknown;
        }
    }

    private static SecurityState
        GetWindowsSecurityCenterStatus()
    {
        try
        {
            using var searcher =
                new ManagementObjectSearcher(
                    "SELECT State FROM Win32_Service "
                    + "WHERE Name = 'wscsvc'");

            foreach (var result in searcher.Get())
            {
                var state =
                    result["State"]
                        ?.ToString()
                        ?.Trim();

                if (state?.Equals(
                        "Running",
                        StringComparison.OrdinalIgnoreCase)
                    == true)
                {
                    return SecurityState.Enabled;
                }

                return SecurityState.Disabled;
            }
        }
        catch
        {
            // Der Dienststatus darf den Scan
            // nicht abbrechen.
        }

        return SecurityState.Unknown;
    }

    [DllImport(
        "wscapi.dll",
        CallingConvention =
            CallingConvention.Winapi)]
    private static extern int
        WscGetSecurityProviderHealth(
            int providers,
            out SecurityProviderHealth health);

    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFirmwareType(
        out FirmwareType firmwareType);

    private enum SecurityProviderHealth
    {
        Good = 0,
        NotMonitored = 1,
        Poor = 2,
        Snooze = 3
    }

    private enum FirmwareType
    {
        Unknown = 0,
        Bios = 1,
        Uefi = 2
    }
}
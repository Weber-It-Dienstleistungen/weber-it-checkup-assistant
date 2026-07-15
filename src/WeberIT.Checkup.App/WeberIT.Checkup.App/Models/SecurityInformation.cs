namespace WeberIT.Checkup.App.Models;

public class SecurityInformation
{
    public List<AntivirusProductInformation> AntivirusProducts { get; set; } =
        new();

    public SecurityState AntivirusStatus { get; set; } =
        SecurityState.Unknown;

    public string AntivirusStatusDetails { get; set; } =
        string.Empty;

    public List<FirewallProfileInformation> FirewallProfiles { get; set; } =
        new();

    public DriveEncryptionInformation SystemDriveEncryption { get; set; } =
        new();

    public SecurityState UserAccountControlStatus { get; set; } =
        SecurityState.Unknown;

    public SecurityState SecureBootStatus { get; set; } =
        SecurityState.Unknown;

    public SecurityState WindowsSecurityCenterStatus { get; set; } =
        SecurityState.Unknown;
}
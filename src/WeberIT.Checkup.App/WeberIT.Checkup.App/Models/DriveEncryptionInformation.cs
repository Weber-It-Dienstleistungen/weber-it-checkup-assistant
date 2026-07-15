namespace WeberIT.Checkup.App.Models;

public class DriveEncryptionInformation
{
    public string DriveLetter { get; set; } = string.Empty;

    public string ConversionStatus { get; set; } = string.Empty;

    public int? EncryptionPercentage { get; set; }

    public SecurityState ProtectionState { get; set; } =
        SecurityState.Unknown;
}
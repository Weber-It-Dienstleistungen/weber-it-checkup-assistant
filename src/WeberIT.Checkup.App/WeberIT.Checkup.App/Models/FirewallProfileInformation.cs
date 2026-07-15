namespace WeberIT.Checkup.App.Models;

public class FirewallProfileInformation
{
    public string ProfileName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public SecurityState State { get; set; } =
        SecurityState.Unknown;
}
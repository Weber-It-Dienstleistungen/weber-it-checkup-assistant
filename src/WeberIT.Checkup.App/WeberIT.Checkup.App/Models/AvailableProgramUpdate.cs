namespace WeberIT.Checkup.App.Models;

public class AvailableProgramUpdate
{
    public string PackageId { get; set; } =
        string.Empty;

    public string Name { get; set; } =
        string.Empty;

    public string InstalledVersion { get; set; } =
        string.Empty;

    public string AvailableVersion { get; set; } =
        string.Empty;

    public string Source { get; set; } =
        string.Empty;
}
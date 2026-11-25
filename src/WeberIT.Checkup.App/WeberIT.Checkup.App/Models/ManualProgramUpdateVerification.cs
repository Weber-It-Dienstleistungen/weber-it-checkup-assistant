namespace WeberIT.Checkup.App.Models;

public sealed class ManualProgramUpdateVerification
{
    public Guid VisitId { get; set; }
    public string PackageId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string PreviousVersion { get; set; } = string.Empty;
    public string TargetVersion { get; set; } = string.Empty;
    public string ObservedVersion { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
}

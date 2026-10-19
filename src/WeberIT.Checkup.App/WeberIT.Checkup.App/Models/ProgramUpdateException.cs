
namespace WeberIT.Checkup.App.Models;

/// <summary>
/// Dokumentiert eine fachlich begründete Ausnahme
/// für genau eine erkannte Programmaktualisierung
/// innerhalb eines bestimmten Kundencheckups.
/// </summary>
public sealed class ProgramUpdateException
{
    public Guid Id { get; set; } =
        Guid.NewGuid();

    public Guid CheckupVisitId { get; set; }

    public string PackageId { get; set; } =
        string.Empty;

    public string PackageName { get; set; } =
        string.Empty;

    public string InstalledVersion { get; set; } =
        string.Empty;

    public string AvailableVersion { get; set; } =
        string.Empty;

    public string Source { get; set; } =
        string.Empty;

    public string Reason { get; set; } =
        string.Empty;

    public string TechnicianNote { get; set; } =
        string.Empty;

    public DateTime ApprovedAt { get; set; } =
        DateTime.Now;

    /// <summary>
    /// Prüft, ob die dokumentierte Ausnahme noch
    /// auf genau dieselbe Aktualisierung innerhalb
    /// desselben Kundencheckups zutrifft.
    ///
    /// Andere Versionen und spätere Checkups
    /// erfordern eine erneute Entscheidung.
    /// </summary>
    public bool AppliesTo(
        Guid checkupVisitId,
        AvailableProgramUpdate update)
    {
        if (update is null
            || checkupVisitId == Guid.Empty
            || CheckupVisitId != checkupVisitId
            || string.IsNullOrWhiteSpace(Reason)
            || string.IsNullOrWhiteSpace(PackageId)
            || string.IsNullOrWhiteSpace(InstalledVersion)
            || string.IsNullOrWhiteSpace(AvailableVersion))
        {
            return false;
        }

        return string.Equals(
                   PackageId,
                   update.PackageId,
                   StringComparison.OrdinalIgnoreCase)
               && string.Equals(
                   InstalledVersion,
                   update.InstalledVersion,
                   StringComparison.OrdinalIgnoreCase)
               && string.Equals(
                   AvailableVersion,
                   update.AvailableVersion,
                   StringComparison.OrdinalIgnoreCase)
               && string.Equals(
                   Source,
                   update.Source,
                   StringComparison.OrdinalIgnoreCase);
    }
}


namespace WeberIT.Checkup.App.Models;

public class PendingWindowsUpdate
{
    public string UpdateId { get; set; } =
        string.Empty;

    public int? RevisionNumber { get; set; }

    public string Title { get; set; } =
        string.Empty;

    public WindowsUpdateCategory Category { get; set; } =
        WindowsUpdateCategory.Unknown;

    public string MsrcSeverity { get; set; } =
        string.Empty;

    public DateTime? LastDeploymentChangeTime { get; set; }

    public List<string> KbArticleIds { get; set; } =
        new();

    public bool IsAssigned { get; set; }

    public bool IsAutoSelected { get; set; }

    public bool IsOptional { get; set; }

    public bool IsHidden { get; set; }

    public bool RequiresRestart { get; set; }
}

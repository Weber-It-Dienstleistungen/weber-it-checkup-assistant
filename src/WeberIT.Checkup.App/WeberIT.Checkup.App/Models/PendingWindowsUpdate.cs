namespace WeberIT.Checkup.App.Models;

public class PendingWindowsUpdate
{
    public string Title { get; set; } =
        string.Empty;

    public WindowsUpdateCategory Category { get; set; } =
        WindowsUpdateCategory.Unknown;

    public bool IsOptional { get; set; }

    public bool IsHidden { get; set; }

    public bool RequiresRestart { get; set; }
}
namespace WeberIT.Checkup.App.Models;

public class WindowsUpdateFailure
{
    public string Title { get; set; } =
        string.Empty;

    public DateTime? Date { get; set; }

    public int? ErrorCode { get; set; }

    public string ErrorCodeHex { get; set; } =
        string.Empty;
}
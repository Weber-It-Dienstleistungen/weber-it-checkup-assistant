namespace WeberIT.Checkup.App.Models;

public class AntivirusProductInformation
{
    public string DisplayName { get; set; } = string.Empty;

    public uint? ProductState { get; set; }

    public string ProductPath { get; set; } = string.Empty;
}
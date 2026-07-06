using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Windows;

public class WindowsInformationProvider : IWindowsInformationProvider
{
    public string GetComputerName()
    {
        return Environment.MachineName;
    }
}
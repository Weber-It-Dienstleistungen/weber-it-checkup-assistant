namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IWindowsInformationProvider
{
    string GetComputerName();
    string GetOperatingSystemName();
    string GetOperatingSystemVersion();
    string GetOperatingSystemArchitecture();
}
namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IWindowsInformationProvider
{
    string GetComputerName();
    string GetManufacturer();
    string GetModel();
    string GetSerialNumber();
    string GetDeviceType();

    string GetOperatingSystemName();
    string GetOperatingSystemVersion();
    string GetOperatingSystemArchitecture();
    string GetBiosVersion();
}
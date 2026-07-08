namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IWindowsInformationProvider
{
    string GetComputerName();
    string GetManufacturer();
    string GetModel();
    string GetSerialNumber();
    string GetDeviceType();
}
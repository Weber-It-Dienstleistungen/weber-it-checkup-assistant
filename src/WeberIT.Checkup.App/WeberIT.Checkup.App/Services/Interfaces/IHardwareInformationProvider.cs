namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IHardwareInformationProvider
{
    string GetManufacturer();
    string GetModel();
    string GetSerialNumber();
    string GetDeviceType();
    string GetBiosVersion();
}
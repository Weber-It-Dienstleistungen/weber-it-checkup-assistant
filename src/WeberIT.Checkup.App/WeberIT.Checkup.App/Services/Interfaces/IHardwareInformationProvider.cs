namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IHardwareInformationProvider
{
    string GetManufacturer();
    string GetModel();
    string GetSerialNumber();
    string GetDeviceType();
    string GetBiosManufacturer();
    string GetBiosVersion();
    string GetProcessorName();
    string GetInstalledMemory();
    string GetMainboardManufacturer();
    string GetMainboardProduct();
    List<string> GetGraphicsCards();
    string GetTpmStatus();
    string GetTpmVersion();
}
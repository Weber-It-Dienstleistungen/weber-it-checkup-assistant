using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IDeviceIdentityService
{
    CustomerDevice? FindMatchingDevice(
        IEnumerable<CustomerDevice> existingDevices,
        DeviceInformation scannedDevice);
}
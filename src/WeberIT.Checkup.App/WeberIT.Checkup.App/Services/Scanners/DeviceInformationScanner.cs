using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Scanners;

public class DeviceInformationScanner : IDeviceInformationScanner
{
    private static readonly string[] InvalidHardwareValues =
    {
        "To Be Filled By O.E.M.",
        "To Be Filled By O.E.M",
        "To Be Filled By OEM",
        "Default string",
        "System Product Name",
        "System Manufacturer",
        "Not Applicable",
        "N/A",
        "Unknown",
        "Unbekannt"
    };

    private readonly IWindowsInformationProvider _windowsInformationProvider;
    private readonly IHardwareInformationProvider _hardwareInformationProvider;

    public DeviceInformationScanner(
        IWindowsInformationProvider windowsInformationProvider,
        IHardwareInformationProvider hardwareInformationProvider)
    {
        _windowsInformationProvider = windowsInformationProvider;
        _hardwareInformationProvider = hardwareInformationProvider;
    }

    public ScanResult<DeviceInformation> Scan()
    {
        var deviceInformation = new DeviceInformation
        {
            Name = _windowsInformationProvider.GetComputerName(),

            Manufacturer = NormalizeHardwareValue(
                _hardwareInformationProvider.GetManufacturer()),

            Model = NormalizeHardwareValue(
                _hardwareInformationProvider.GetModel()),

            SerialNumber = _hardwareInformationProvider.GetSerialNumber(),
            DeviceType = _hardwareInformationProvider.GetDeviceType(),
            BiosVersion = _hardwareInformationProvider.GetBiosVersion()
        };

        return new ScanResult<DeviceInformation>
        {
            IsSuccessful = true,
            Data = deviceInformation
        };
    }

    private static string NormalizeHardwareValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalizedValue = value.Trim();

        var isInvalidValue = InvalidHardwareValues.Any(
            invalidValue => normalizedValue.Equals(
                invalidValue,
                StringComparison.OrdinalIgnoreCase));

        return isInvalidValue
            ? string.Empty
            : normalizedValue;
    }
}
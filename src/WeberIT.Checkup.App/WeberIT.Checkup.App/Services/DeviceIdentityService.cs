using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services;

public class DeviceIdentityService : IDeviceIdentityService
{
    private static readonly string[] InvalidIdentifierValues =
    {
        "To Be Filled By O.E.M.",
        "To Be Filled By O.E.M",
        "To Be Filled By OEM",
        "Default string",
        "System Serial Number",
        "Not Applicable",
        "N/A",
        "Unknown",
        "Unbekannt"
    };

    public CustomerDevice? FindMatchingDevice(
        IEnumerable<CustomerDevice> existingDevices,
        DeviceInformation scannedDevice)
    {
        var devices = existingDevices.ToList();

        var scannedSerialNumber =
            NormalizeIdentifier(scannedDevice.SerialNumber);

        if (!string.IsNullOrWhiteSpace(scannedSerialNumber))
        {
            var serialNumberMatch = devices.FirstOrDefault(device =>
            {
                var existingSerialNumber = NormalizeIdentifier(
                    device.CheckupSession.DeviceInformation.SerialNumber);

                return !string.IsNullOrWhiteSpace(existingSerialNumber)
                       && existingSerialNumber.Equals(
                           scannedSerialNumber,
                           StringComparison.OrdinalIgnoreCase);
            });

            if (serialNumberMatch is not null)
            {
                return serialNumberMatch;
            }
        }

        var scannedComputerName =
            NormalizeIdentifier(scannedDevice.Name);

        if (string.IsNullOrWhiteSpace(scannedComputerName))
        {
            return null;
        }

        return devices.FirstOrDefault(device =>
        {
            var existingComputerName = NormalizeIdentifier(
                device.CheckupSession.DeviceInformation.Name);

            return !string.IsNullOrWhiteSpace(existingComputerName)
                   && existingComputerName.Equals(
                       scannedComputerName,
                       StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string NormalizeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalizedValue = value.Trim();

        var isInvalidValue = InvalidIdentifierValues.Any(
            invalidValue => normalizedValue.Equals(
                invalidValue,
                StringComparison.OrdinalIgnoreCase));

        return isInvalidValue
            ? string.Empty
            : normalizedValue;
    }
}
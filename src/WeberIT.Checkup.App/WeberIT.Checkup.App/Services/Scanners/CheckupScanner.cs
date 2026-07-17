using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Scanners;

public class CheckupScanner : ICheckupScanner
{
    private readonly IDeviceInformationScanner
        _deviceInformationScanner;

    private readonly IHardwareInformationScanner
        _hardwareInformationScanner;

    private readonly IOperatingSystemInformationScanner
        _operatingSystemInformationScanner;

    private readonly IStorageInformationScanner
        _storageInformationScanner;

    private readonly ICleanupPotentialScanner
        _cleanupPotentialScanner;

    private readonly IStartupInformationScanner
        _startupInformationScanner;

    private readonly IDeviceDriverInformationScanner
        _deviceDriverInformationScanner;

    private readonly ISecurityInformationScanner
        _securityInformationScanner;

    private readonly IWindowsUpdateInformationScanner
        _windowsUpdateInformationScanner;

    private readonly IProgramUpdateInformationScanner
        _programUpdateInformationScanner;

    private readonly IRestartInformationScanner
        _restartInformationScanner;

    public CheckupScanner(
        IDeviceInformationScanner deviceInformationScanner,
        IHardwareInformationScanner hardwareInformationScanner,
        IOperatingSystemInformationScanner operatingSystemInformationScanner,
        IStorageInformationScanner storageInformationScanner,
        ICleanupPotentialScanner cleanupPotentialScanner,
        IStartupInformationScanner startupInformationScanner,
        IDeviceDriverInformationScanner deviceDriverInformationScanner,
        ISecurityInformationScanner securityInformationScanner,
        IWindowsUpdateInformationScanner windowsUpdateInformationScanner,
        IProgramUpdateInformationScanner programUpdateInformationScanner,
        IRestartInformationScanner restartInformationScanner)
    {
        _deviceInformationScanner =
            deviceInformationScanner;

        _hardwareInformationScanner =
            hardwareInformationScanner;

        _operatingSystemInformationScanner =
            operatingSystemInformationScanner;

        _storageInformationScanner =
            storageInformationScanner;

        _cleanupPotentialScanner =
            cleanupPotentialScanner;

        _startupInformationScanner =
            startupInformationScanner;

        _deviceDriverInformationScanner =
            deviceDriverInformationScanner;

        _securityInformationScanner =
            securityInformationScanner;

        _windowsUpdateInformationScanner =
            windowsUpdateInformationScanner;

        _programUpdateInformationScanner =
            programUpdateInformationScanner;

        _restartInformationScanner =
            restartInformationScanner;
    }

    public CheckupSession Scan()
    {
        var deviceInformationResult =
            _deviceInformationScanner.Scan();

        var hardwareInformationResult =
            _hardwareInformationScanner.Scan();

        var operatingSystemInformationResult =
            _operatingSystemInformationScanner.Scan();

        var storageInformationResult =
            _storageInformationScanner.Scan();

        var storageInformation =
            storageInformationResult.Data
            ?? new StorageInformation();

        var cleanupPotentialResult =
            _cleanupPotentialScanner.Scan(
                storageInformation);

        var startupInformationResult =
            _startupInformationScanner.Scan();

        var deviceDriverInformationResult =
            _deviceDriverInformationScanner.Scan();

        var securityInformationResult =
            _securityInformationScanner.Scan();

        var windowsUpdateInformationResult =
            _windowsUpdateInformationScanner.Scan();

        var programUpdateInformationResult =
            _programUpdateInformationScanner.Scan();

        var restartInformationResult =
            _restartInformationScanner.Scan();

        return new CheckupSession
        {
            ScanDate =
                DateTime.Now,

            DeviceInformation =
                deviceInformationResult.Data
                ?? new DeviceInformation(),

            HardwareInformation =
                hardwareInformationResult.Data
                ?? new HardwareInformation(),

            OperatingSystemInformation =
                operatingSystemInformationResult.Data
                ?? new OperatingSystemInformation(),

            StorageInformation =
                storageInformation,

            CleanupPotentialInformation =
                cleanupPotentialResult.Data
                ?? new CleanupPotentialInformation(),

            StartupInformation =
                startupInformationResult.Data
                ?? new StartupInformation(),

            DeviceDriverInformation =
                deviceDriverInformationResult.Data
                ?? new DeviceDriverInformation(),

            SecurityInformation =
                securityInformationResult.Data
                ?? new SecurityInformation(),

            WindowsUpdateInformation =
                windowsUpdateInformationResult.Data
                ?? new WindowsUpdateInformation(),

            ProgramUpdateInformation =
                programUpdateInformationResult.Data
                ?? new ProgramUpdateInformation(),

            RestartInformation =
                restartInformationResult.Data
                ?? new RestartInformation()
        };
    }
}
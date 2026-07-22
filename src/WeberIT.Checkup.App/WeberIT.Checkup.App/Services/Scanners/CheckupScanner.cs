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
        IOperatingSystemInformationScanner
            operatingSystemInformationScanner,
        IStorageInformationScanner storageInformationScanner,
        ICleanupPotentialScanner cleanupPotentialScanner,
        IStartupInformationScanner startupInformationScanner,
        IDeviceDriverInformationScanner
            deviceDriverInformationScanner,
        ISecurityInformationScanner securityInformationScanner,
        IWindowsUpdateInformationScanner
            windowsUpdateInformationScanner,
        IProgramUpdateInformationScanner
            programUpdateInformationScanner,
        IRestartInformationScanner restartInformationScanner)
    {
        ArgumentNullException.ThrowIfNull(
            deviceInformationScanner);

        ArgumentNullException.ThrowIfNull(
            hardwareInformationScanner);

        ArgumentNullException.ThrowIfNull(
            operatingSystemInformationScanner);

        ArgumentNullException.ThrowIfNull(
            storageInformationScanner);

        ArgumentNullException.ThrowIfNull(
            cleanupPotentialScanner);

        ArgumentNullException.ThrowIfNull(
            startupInformationScanner);

        ArgumentNullException.ThrowIfNull(
            deviceDriverInformationScanner);

        ArgumentNullException.ThrowIfNull(
            securityInformationScanner);

        ArgumentNullException.ThrowIfNull(
            windowsUpdateInformationScanner);

        ArgumentNullException.ThrowIfNull(
            programUpdateInformationScanner);

        ArgumentNullException.ThrowIfNull(
            restartInformationScanner);

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

    public CheckupSession Scan(
        IProgress<CheckupScanProgress>? progress = null)
    {
        var deviceInformationResult =
            ExecuteStep(
                CheckupScanStepCatalog.DeviceInformation,
                () =>
                    _deviceInformationScanner.Scan(),
                progress);

        var hardwareInformationResult =
            ExecuteStep(
                CheckupScanStepCatalog.HardwareInformation,
                () =>
                    _hardwareInformationScanner.Scan(),
                progress);

        var operatingSystemInformationResult =
            ExecuteStep(
                CheckupScanStepCatalog
                    .OperatingSystemInformation,
                () =>
                    _operatingSystemInformationScanner.Scan(),
                progress);

        var storageInformationResult =
            ExecuteStep(
                CheckupScanStepCatalog.StorageInformation,
                () =>
                    _storageInformationScanner.Scan(),
                progress);

        var storageInformation =
            storageInformationResult.Data
            ?? new StorageInformation();

        var cleanupPotentialResult =
            ExecuteStep(
                CheckupScanStepCatalog.CleanupPotential,
                () =>
                    _cleanupPotentialScanner.Scan(
                        storageInformation),
                progress,
                result =>
                    result.Data?.AnalysisStatus
                    == CleanupMeasurementStatus.PartiallyMeasured);

        var startupInformationResult =
            ExecuteStep(
                CheckupScanStepCatalog.StartupInformation,
                () =>
                    _startupInformationScanner.Scan(),
                progress);

        var deviceDriverInformationResult =
            ExecuteStep(
                CheckupScanStepCatalog.DeviceDriverInformation,
                () =>
                    _deviceDriverInformationScanner.Scan(),
                progress);

        var securityInformationResult =
            ExecuteStep(
                CheckupScanStepCatalog.SecurityInformation,
                () =>
                    _securityInformationScanner.Scan(),
                progress);

        var windowsUpdateInformationResult =
            ExecuteStep(
                CheckupScanStepCatalog.WindowsUpdateInformation,
                () =>
                    _windowsUpdateInformationScanner.Scan(),
                progress);

        var programUpdateInformationResult =
            ExecuteStep(
                CheckupScanStepCatalog.ProgramUpdateInformation,
                () =>
                    _programUpdateInformationScanner.Scan(),
                progress);

        var restartInformationResult =
            ExecuteStep(
                CheckupScanStepCatalog.RestartInformation,
                () =>
                    _restartInformationScanner.Scan(),
                progress);

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

    private static ScanResult<T> ExecuteStep<T>(
        CheckupScanStepDefinition definition,
        Func<ScanResult<T>> scanAction,
        IProgress<CheckupScanProgress>? progress,
        Func<ScanResult<T>, bool>? warningEvaluator = null)
    {
        ArgumentNullException.ThrowIfNull(
            definition);

        ArgumentNullException.ThrowIfNull(
            scanAction);

        progress?.Report(
            CheckupScanProgress.CreateRunning(
                definition));

        try
        {
            var result =
                scanAction();

            if (result is null)
            {
                throw new InvalidOperationException(
                    "Der Scanbereich lieferte kein "
                    + "technisches Ergebnis.");
            }

            var hasWarning =
                !result.IsSuccessful
                || (warningEvaluator?.Invoke(
                        result)
                    ?? false);

            if (hasWarning)
            {
                progress?.Report(
                    CheckupScanProgress.CreateWarning(
                        definition,
                        BuildWarningMessage(
                            result.Message)));
            }
            else
            {
                progress?.Report(
                    CheckupScanProgress.CreateSuccessful(
                        definition));
            }

            return result;
        }
        catch (Exception exception)
        {
            var errorMessage =
                BuildFailureMessage(
                    exception);

            progress?.Report(
                CheckupScanProgress.CreateFailed(
                    definition,
                    errorMessage));

            throw new InvalidOperationException(
                "Der Scanbereich „"
                + definition.Title
                + "“ konnte nicht abgeschlossen werden. "
                + errorMessage,
                exception);
        }
    }

    private static string BuildWarningMessage(
        string? message)
    {
        if (!string.IsNullOrWhiteSpace(
                message))
        {
            return message.Trim();
        }

        return
            "Der Scanbereich konnte nicht vollständig "
            + "ausgewertet werden. Der Systemscan wird "
            + "mit den verfügbaren Daten fortgesetzt.";
    }

    private static string BuildFailureMessage(
        Exception exception)
    {
        if (!string.IsNullOrWhiteSpace(
                exception.Message))
        {
            return exception.Message.Trim();
        }

        return
            "Keine weiteren technischen Fehlerdetails "
            + "sind verfügbar.";
    }
}
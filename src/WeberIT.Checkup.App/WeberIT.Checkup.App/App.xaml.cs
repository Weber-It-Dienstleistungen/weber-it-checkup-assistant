using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;
using WeberIT.Checkup.App.Infrastructure.Persistence;
using WeberIT.Checkup.App.Repositories;
using WeberIT.Checkup.App.Repositories.Interfaces;
using WeberIT.Checkup.App.Services;
using WeberIT.Checkup.App.Services.Assessment;
using WeberIT.Checkup.App.Services.Cleanup;
using WeberIT.Checkup.App.Services.Devices;
using WeberIT.Checkup.App.Services.Hardware;
using WeberIT.Checkup.App.Services.Interfaces;
using WeberIT.Checkup.App.Services.Maintenance;
using WeberIT.Checkup.App.Services.Scanners;
using WeberIT.Checkup.App.Services.Security;
using WeberIT.Checkup.App.Services.Startup;
using WeberIT.Checkup.App.Services.Storage;
using WeberIT.Checkup.App.Services.Tasks;
using WeberIT.Checkup.App.Services.Updates;
using WeberIT.Checkup.App.Services.Windows;
using WeberIT.Checkup.App.ViewModels;
using WeberIT.Checkup.App.Views.Pages;

namespace WeberIT.Checkup.App;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<DatabasePaths>();
                services.AddSingleton<DatabaseConnectionFactory>();
                services.AddSingleton<DatabaseInitializer>();

                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();

                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<DashboardView>();

                services.AddSingleton<CheckupViewModel>();
                services.AddSingleton<CheckupView>();

                services.AddSingleton<MaintenanceViewModel>();
                services.AddSingleton<MaintenanceView>();

                services.AddSingleton<
                    INavigationService,
                    NavigationService>();

                services.AddSingleton<
                    IDialogService,
                    DialogService>();

                services.AddSingleton<
                    ICheckupTaskActionExecutionCoordinator,
                    CheckupTaskActionExecutionCoordinator>();

                services.AddSingleton<
                    IGuidedTaskActionLauncher,
                    GuidedTaskActionLauncher>();

                services.AddSingleton<
                    IDeviceIdentityService,
                    DeviceIdentityService>();

                services.AddSingleton<CustomerDevicesViewModel>();
                services.AddSingleton<CustomersViewModel>();
                services.AddSingleton<CustomersView>();

                services.AddTransient<CustomerEditViewModel>();

                services.AddSingleton<
                    IMaintenanceProcessRunner,
                    MaintenanceProcessRunner>();

                services.AddSingleton<
                    IProgramUpdateActionExecutor,
                    ProgramUpdateActionExecutor>();

                services.AddSingleton<CleanupActionExecutor>();

                services.AddSingleton<
                    BrowserCacheCleanupExecutor>();

                services.AddSingleton<
                    ICleanupActionExecutor,
                    ControlledCleanupActionExecutor>();

                services.AddSingleton<
                    ISystemFileChecker,
                    SystemFileChecker>();

                services.AddSingleton<
                    IWindowsImageRepairService,
                    WindowsImageRepairService>();

                services.AddSingleton<
                    ICustomerRepository,
                    SQLiteCustomerRepository>();

                services.AddSingleton<
                    ICustomerService,
                    CustomerService>();

                services.AddSingleton<
                    IWindowsInformationProvider,
                    WindowsInformationProvider>();

                services.AddSingleton<
                    IHardwareInformationProvider,
                    HardwareInformationProvider>();

                services.AddSingleton<
                    IStorageInformationProvider,
                    StorageInformationProvider>();

                services.AddSingleton<
                    ICleanupPotentialProvider,
                    CleanupPotentialProvider>();

                services.AddSingleton<StartupCommandAnalyzer>();
                services.AddSingleton<ShellLinkTargetReader>();
                services.AddSingleton<StartupRegistrySourceReader>();
                services.AddSingleton<StartupFolderSourceReader>();

                services.AddSingleton<
                    IStartupInformationProvider,
                    StartupInformationProvider>();

                services.AddSingleton<
                    IDeviceDriverInformationProvider,
                    DeviceDriverInformationProvider>();

                services.AddSingleton<
                    ISecurityInformationProvider,
                    SecurityInformationProvider>();

                services.AddSingleton<
                    IWindowsUpdateInformationProvider,
                    WindowsUpdateInformationProvider>();

                services.AddSingleton<
                    IProgramUpdateInformationProvider,
                    ProgramUpdateInformationProvider>();

                services.AddSingleton<
                    IRestartInformationProvider,
                    RestartInformationProvider>();

                services.AddSingleton<
                    IDeviceInformationScanner,
                    DeviceInformationScanner>();

                services.AddSingleton<
                    IHardwareInformationScanner,
                    HardwareInformationScanner>();

                services.AddSingleton<
                    IOperatingSystemInformationScanner,
                    OperatingSystemInformationScanner>();

                services.AddSingleton<
                    IStorageInformationScanner,
                    StorageInformationScanner>();

                services.AddSingleton<
                    ICleanupPotentialScanner,
                    CleanupPotentialScanner>();

                services.AddSingleton<
                    IStartupInformationScanner,
                    StartupInformationScanner>();

                services.AddSingleton<
                    IDeviceDriverInformationScanner,
                    DeviceDriverInformationScanner>();

                services.AddSingleton<
                    ISecurityInformationScanner,
                    SecurityInformationScanner>();

                services.AddSingleton<
                    IWindowsUpdateInformationScanner,
                    WindowsUpdateInformationScanner>();

                services.AddSingleton<
                    IProgramUpdateInformationScanner,
                    ProgramUpdateInformationScanner>();

                services.AddSingleton<
                    IRestartInformationScanner,
                    RestartInformationScanner>();

                services.AddSingleton<
                    ICheckupScanner,
                    CheckupScanner>();

                services.AddSingleton<
                    ISystemConditionAssessmentService,
                    SystemConditionAssessmentService>();

                services.AddSingleton<
                    IHardwareConditionAssessmentService,
                    HardwareConditionAssessmentService>();

                services.AddSingleton<
                    ICheckupAssessmentService,
                    CheckupAssessmentService>();

                services.AddSingleton<
                    ICheckupAssessmentRule,
                    StorageAssessmentRule>();

                services.AddSingleton<
                    ICheckupAssessmentRule,
                    CleanupPotentialAssessmentRule>();

                services.AddSingleton<
                    ICheckupAssessmentRule,
                    StartupAssessmentRule>();

                services.AddSingleton<
                    ICheckupAssessmentRule,
                    DeviceDriverAssessmentRule>();

                services.AddSingleton<
                    ICheckupAssessmentRule,
                    MemoryAssessmentRule>();

                services.AddSingleton<
                    ICheckupAssessmentRule,
                    TpmAssessmentRule>();

                services.AddSingleton<
                    ICheckupAssessmentRule,
                    OperatingSystemAssessmentRule>();

                services.AddSingleton<
                    ICheckupAssessmentRule,
                    AntivirusAssessmentRule>();

                services.AddSingleton<
                    ICheckupAssessmentRule,
                    FirewallAssessmentRule>();

                services.AddSingleton<
                    ICheckupAssessmentRule,
                    UserAccountControlAssessmentRule>();

                services.AddSingleton<
                    ICheckupAssessmentRule,
                    SecurityCenterAssessmentRule>();

                services.AddSingleton<
                    ICheckupAssessmentRule,
                    DriveEncryptionAssessmentRule>();

                services.AddSingleton<
                    ICheckupAssessmentRule,
                    SecureBootAssessmentRule>();

                services.AddSingleton<
                    ICheckupAssessmentRule,
                    WindowsUpdateAssessmentRule>();

                services.AddSingleton<
                    ICheckupAssessmentRule,
                    RestartAssessmentRule>();

                services.AddSingleton<
                    ICheckupAssessmentRule,
                    ProgramUpdateAssessmentRule>();
            })
            .Build();
    }

    public IServiceProvider Services =>
        _host.Services;

    protected override async void OnStartup(
        StartupEventArgs e)
    {
        var databasePaths =
            _host.Services.GetRequiredService<DatabasePaths>();

        var databaseInitializer =
            _host.Services.GetRequiredService<DatabaseInitializer>();

        try
        {
            databaseInitializer.Initialize();
        }
        catch (Exception exception)
        {
            var dialogService =
                _host.Services.GetRequiredService<IDialogService>();

            dialogService.ShowError(
                "Datenbank nicht verfügbar",
                $"""
                 Die portable Datenbank konnte nicht geöffnet oder angelegt werden.

                 Datenbankpfad:
                 {databasePaths.DatabaseFilePath}

                 Prüfe bitte, ob der Programmordner und der verwendete Datenträger beschreibbar sind. Stelle außerdem sicher, dass der USB-Stick nicht schreibgeschützt oder entfernt wurde.

                 Technische Ursache:
                 {exception.Message}

                 Die Anwendung wird beendet, damit keine Daten an einem falschen Speicherort angelegt werden.
                 """);

            Shutdown(-1);

            return;
        }

        await _host.StartAsync();

        var mainWindow =
            _host.Services.GetRequiredService<MainWindow>();

        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(
        ExitEventArgs e)
    {
        await _host.StopAsync();

        _host.Dispose();

        base.OnExit(e);
    }
}
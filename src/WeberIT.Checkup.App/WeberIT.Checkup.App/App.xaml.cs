using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;
using WeberIT.Checkup.App.Infrastructure.Persistence;
using WeberIT.Checkup.App.Repositories;
using WeberIT.Checkup.App.Repositories.Interfaces;
using WeberIT.Checkup.App.Services;
using WeberIT.Checkup.App.Services.Assessment;
using WeberIT.Checkup.App.Services.Hardware;
using WeberIT.Checkup.App.Services.Interfaces;
using WeberIT.Checkup.App.Services.Scanners;
using WeberIT.Checkup.App.Services.Storage;
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

                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<IDialogService, DialogService>();

                services.AddSingleton<CustomerDevicesViewModel>();
                services.AddSingleton<CustomersViewModel>();
                services.AddSingleton<CustomersView>();

                services.AddTransient<CustomerEditViewModel>();

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
                    ICheckupScanner,
                    CheckupScanner>();

                services.AddSingleton<
                    ICheckupAssessmentService,
                    CheckupAssessmentService>();

                services.AddSingleton<
                    ICheckupAssessmentRule,
                    StorageAssessmentRule>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        var databaseInitializer =
            _host.Services.GetRequiredService<DatabaseInitializer>();

        databaseInitializer.Initialize();

        var mainWindow =
            _host.Services.GetRequiredService<MainWindow>();

        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();

        base.OnExit(e);
    }
}
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IWindowsImageRepairService
{
    Task<MaintenanceToolResult> RunAsync();
}
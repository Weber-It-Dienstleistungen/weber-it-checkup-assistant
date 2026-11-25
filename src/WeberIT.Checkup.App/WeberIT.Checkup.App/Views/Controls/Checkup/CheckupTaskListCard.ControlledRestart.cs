using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;
using WeberIT.Checkup.App.Services.Tasks;
using WeberIT.Checkup.App.ViewModels;

namespace WeberIT.Checkup.App.Views.Controls.Checkup;

public partial class CheckupTaskListCard
{
    private bool _controlledRestartRunning;

    private async void VerifyPendingControlledRestartOnOpen()
    {
        if (_controlledRestartRunning || CheckupSession is not { } session
            || session.CurrentCustomerCheckupVisit is not { IsCompletionPrepared: false } visit)
            return;

        var task = session.TaskList.Tasks.FirstOrDefault(item =>
            ControlledRestartEvidencePolicy.GetPendingRequest(item, visit.Id) is not null);
        if (task is not null)
            await RunControlledRestartAsync(task, interactive: false);
    }

    private bool TryResolveControlledRestartContext(CheckupTask task,
        out CustomerDevicesViewModel viewModel, out Customer customer,
        out CustomerDevice device, out CustomerCheckupVisit visit)
    {
        viewModel = null!;
        customer = null!;
        device = null!;
        visit = null!;

        if (Window.GetWindow(this)?.DataContext is not CustomerDevicesViewModel vm
            || vm.IsScanRunning || vm.SelectedCustomer is not { } selectedCustomer
            || vm.SelectedDevice is not { } selectedDevice
            || !selectedCustomer.Devices.Any(item => ReferenceEquals(item, selectedDevice))
            || CheckupSession is null || !ReferenceEquals(selectedDevice.CheckupSession, CheckupSession)
            || DataContext is not CheckupTaskList list || !ReferenceEquals(list, CheckupSession.TaskList)
            || !list.Tasks.Any(item => ReferenceEquals(item, task))
            || task.TaskCode != ControlledRestartEvidencePolicy.TaskCode
            || task.Status != CheckupTaskStatus.Open
            || CheckupSession.CurrentCustomerCheckupVisit is not { IsInProgress: true,
                IsCompletionPrepared: false } currentVisit)
            return false;

        viewModel = vm;
        customer = selectedCustomer;
        device = selectedDevice;
        visit = currentVisit;
        return true;
    }

    private async Task RunControlledRestartAsync(CheckupTask task, bool interactive)
    {
        if (_controlledRestartRunning)
            return;

        _controlledRestartRunning = true;
        try
        {
            if (!TryResolveControlledRestartContext(task, out _, out var customer,
                    out var device, out var visit))
            {
                if (interactive)
                    ShowPreparationError("Der laufende Kundencheckup konnte nicht eindeutig zugeordnet werden.");
                return;
            }

            if (Application.Current is not App application)
                throw new InvalidOperationException("Anwendungsdienste fehlen.");

            var coordinator = application.Services.GetRequiredService<ICheckupTaskActionExecutionCoordinator>();
            using var lease = coordinator.TryBeginExecution(
                ControlledRestartEvidencePolicy.RequestCode, "Kontrollierter Windows-Neustart");
            if (lease is null)
            {
                if (interactive)
                    ShowPreparationError("Eine andere technische Systemaktion läuft bereits.");
                return;
            }

            var scanner = application.Services.GetRequiredService<IDeviceInformationScanner>();
            var identity = application.Services.GetRequiredService<IDeviceIdentityService>();

            bool ContextStillMatches() =>
                TryResolveControlledRestartContext(task, out _, out var currentCustomer,
                    out var currentDevice, out var currentVisit)
                && ReferenceEquals(currentCustomer, customer)
                && ReferenceEquals(currentDevice, device)
                && ReferenceEquals(currentVisit, visit);

            var deviceScan = await Task.Run(() => scanner.Scan());
            if (!ContextStillMatches())
                return;
            if (!deviceScan.IsSuccessful || deviceScan.Data is null
                || identity.FindMatchingDevice(customer.Devices, deviceScan.Data)?.Id != device.Id)
            {
                if (interactive)
                    ShowPreparationError("Der aktuell gestartete Computer stimmt nicht eindeutig mit dem Kundengerät überein.");
                return;
            }

            var currentBoot = await Task.Run(ReadBootTimeUtc);
            if (!ContextStillMatches())
                return;

            var taskList = device.CheckupSession.TaskList;
            var request = ControlledRestartEvidencePolicy.GetPendingRequest(task, visit.Id);

            if (request is not null && ControlledRestartEvidencePolicy.IsRebootVerified(
                    request.ControlledRestartBootTimeUtc!.Value, request.StartedAt!.Value,
                    currentBoot, DateTimeOffset.UtcNow))
            {
                var restartProvider = application.Services.GetRequiredService<IRestartInformationProvider>();
                var restart = await Task.Run(() => restartProvider.GetRestartInformation());
                if (!ContextStillMatches())
                    return;

                if (!restart.IsAnalysisPerformed || !restart.IsAnalysisConclusive
                    || !restart.IsRestartRequired.HasValue)
                {
                    if (interactive)
                        ShowPreparationError("Der Neustart ist nachgewiesen, der verbleibende Neustartbedarf aber noch nicht zuverlässig auswertbar. Die Aufgabe bleibt offen.");
                    return;
                }

                var stillRequired = restart.IsRestartRequired.Value;
                var summary = stillRequired
                    ? "Neuer Windows-Systemstart bestätigt; Windows meldet weiteren Neustartbedarf."
                    : "Neuer Windows-Systemstart bestätigt; kein weiterer Neustartbedarf erkannt.";

                var verification = new CheckupTaskActionResult
                {
                    ActionCode = ControlledRestartEvidencePolicy.VerificationCode,
                    ActionTitle = "Kontrollierten Neustart überprüft",
                    TargetDescription = "Windows-Systemstart des zugeordneten Kundengeräts",
                    Status = CheckupTaskActionStatus.Successful,
                    Summary = summary,
                    Details = $"Vorheriger Systemstart (UTC): {request.ControlledRestartBootTimeUtc:O}\nNeuer Systemstart (UTC): {currentBoot:O}",
                    ControlledRestartVisitId = visit.Id,
                    ControlledRestartRequestId = request.Id,
                    ControlledRestartBootTimeUtc = currentBoot,
                    RestartRequired = stillRequired,
                    RestartStatusWasConclusive = true,
                    StartedAt = DateTimeOffset.Now,
                    FinishedAt = DateTimeOffset.Now
                };

                taskList.ApplyTaskActionOutcome(task, verification,
                    stillRequired ? CheckupTaskStatus.Open : CheckupTaskStatus.Completed,
                    summary);
                if (interactive)
                    MessageBox.Show(Window.GetWindow(this), summary, "Neustartprüfung",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Automatische Wiederaufnahme prueft nur; sie startet niemals neu.
            if (!interactive)
                return;

            var message = request is null
                ? "Windows wird jetzt neu gestartet."
                : "Seit dem zuletzt gespeicherten Neustartauftrag wurde noch kein neuer Systemstart nachgewiesen. Soll der Neustart erneut angefordert werden?";
            message += "\n\nAlle offenen Arbeiten speichern und Programme schließen. Der Checkup wird vor dem Neustart gesichert. Öffnen Sie danach erneut denselben Kundencheckup. Es wird kein erzwungenes Schließen angefordert.";

            if (MessageBox.Show(Window.GetWindow(this), message, "Kontrollierter Neustart",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
                != MessageBoxResult.Yes || !ContextStillMatches())
                return;

            var requestedAt = DateTimeOffset.UtcNow;
            if (currentBoot >= requestedAt)
                throw new InvalidOperationException("Der bisherige Systemstart konnte zeitlich nicht sicher eingeordnet werden.");

            var newRequest = new CheckupTaskActionResult
            {
                ActionCode = ControlledRestartEvidencePolicy.RequestCode,
                ActionTitle = "Kontrollierten Neustart vorbereitet",
                    TargetDescription = "Windows-Systemstart des zugeordneten Kundengeräts",
                Status = CheckupTaskActionStatus.Unknown,
                Summary = "Neustart ausdrücklich bestätigt und gespeichert. Tatsächlicher Systemstart noch nicht nachgewiesen.",
                Details = $"Bisheriger Systemstart (UTC): {currentBoot:O}",
                ControlledRestartVisitId = visit.Id,
                ControlledRestartBootTimeUtc = currentBoot,
                RestartStatusWasConclusive = false,
                StartedAt = requestedAt,
                FinishedAt = requestedAt
            };

            // Bei Persistenzfehler wird der Neustart nicht angefordert.
            taskList.AddTaskActionResult(task, newRequest);
            if (!ContextStillMatches())
                return;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.SystemDirectory, "shutdown.exe"),
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("/r");
                startInfo.ArgumentList.Add("/t");
                startInfo.ArgumentList.Add("0");

                using var process = Process.Start(startInfo)
                    ?? throw new InvalidOperationException("Windows-Neustart konnte nicht angefordert werden.");
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"Windows hat den Neustartauftrag mit Fehlercode {process.ExitCode} abgewiesen.");
            }
            catch (Exception exception)
            {
                if (ContextStillMatches())
                {
                    taskList.AddTaskActionResult(task, new CheckupTaskActionResult
                    {
                        ActionCode = ControlledRestartEvidencePolicy.VerificationCode,
                        ActionTitle = "Neustartauftrag fehlgeschlagen",
                    TargetDescription = "Windows-Systemstart des zugeordneten Kundengeräts",
                        Status = CheckupTaskActionStatus.Failed,
                        Summary = exception.Message,
                        ControlledRestartVisitId = visit.Id,
                        ControlledRestartRequestId = newRequest.Id,
                        RestartStatusWasConclusive = false,
                        StartedAt = DateTimeOffset.Now,
                        FinishedAt = DateTimeOffset.Now
                    });
                }
                throw;
            }
        }
        catch (Exception exception)
        {
            if (interactive)
                ShowPreparationError(exception.Message);
            // Automatische Wiederaufnahme veraendert bei Fehlern keinen Status.
        }
        finally
        {
            _controlledRestartRunning = false;
        }
    }

    private static DateTimeOffset ReadBootTimeUtc()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT LastBootUpTime FROM Win32_OperatingSystem");
        using var results = searcher.Get();
        foreach (ManagementObject item in results)
        {
            using (item)
            {
                if (item["LastBootUpTime"] is string value && !string.IsNullOrWhiteSpace(value))
                    return new DateTimeOffset(ManagementDateTimeConverter.ToDateTime(value)).ToUniversalTime();
            }
        }

        throw new InvalidOperationException("Die Windows-Systemstartzeit konnte nicht sicher ermittelt werden.");
    }
}

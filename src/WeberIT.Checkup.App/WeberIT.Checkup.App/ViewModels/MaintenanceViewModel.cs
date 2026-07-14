using System.Text;
using System.Windows.Input;
using WeberIT.Checkup.App.Infrastructure.Commands;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.ViewModels;

public class MaintenanceViewModel : BaseViewModel
{
    private readonly ISystemFileChecker _systemFileChecker;
    private readonly IWindowsImageRepairService _windowsImageRepairService;

    private readonly AsyncRelayCommand _runSfcCommand;
    private readonly AsyncRelayCommand _runDismCommand;

    private MaintenanceToolResult _sfcResult =
        new();

    private MaintenanceToolResult _dismResult =
        new();

    private bool _isSfcRunning;
    private bool _isDismRunning;

    public MaintenanceViewModel(
        ISystemFileChecker systemFileChecker,
        IWindowsImageRepairService windowsImageRepairService)
    {
        _systemFileChecker = systemFileChecker;
        _windowsImageRepairService = windowsImageRepairService;

        _runSfcCommand =
            new AsyncRelayCommand(
                RunSfcAsync,
                () => CanRunSfc);

        _runDismCommand =
            new AsyncRelayCommand(
                RunDismAsync,
                () => CanRunDism);

        RunSfcCommand = _runSfcCommand;
        RunDismCommand = _runDismCommand;
    }

    public string Title =>
        "Werkzeuge";

    public string Subtitle =>
        "Windows-Wartungswerkzeuge kontrolliert ausführen und Ergebnisse prüfen.";

    public string AdministratorNotice =>
        "Für diese Wartungswerkzeuge sind Administratorrechte erforderlich. "
        + "Windows zeigt beim Start gegebenenfalls eine Sicherheitsabfrage an.";

    public MaintenanceToolResult SfcResult
    {
        get => _sfcResult;

        private set
        {
            if (ReferenceEquals(
                _sfcResult,
                value))
            {
                return;
            }

            _sfcResult = value;

            OnPropertyChanged();
            NotifySfcResultPropertiesChanged();
        }
    }

    public MaintenanceToolResult DismResult
    {
        get => _dismResult;

        private set
        {
            if (ReferenceEquals(
                _dismResult,
                value))
            {
                return;
            }

            _dismResult = value;

            OnPropertyChanged();
            NotifyDismResultPropertiesChanged();
        }
    }

    public bool IsSfcRunning
    {
        get => _isSfcRunning;

        private set
        {
            if (_isSfcRunning == value)
            {
                return;
            }

            _isSfcRunning = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAnyMaintenanceRunning));
            OnPropertyChanged(nameof(SfcButtonText));
            OnPropertyChanged(nameof(CanRunSfc));
            OnPropertyChanged(nameof(CanRunDism));

            RaiseMaintenanceCommandStates();
        }
    }

    public bool IsDismRunning
    {
        get => _isDismRunning;

        private set
        {
            if (_isDismRunning == value)
            {
                return;
            }

            _isDismRunning = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAnyMaintenanceRunning));
            OnPropertyChanged(nameof(DismButtonText));
            OnPropertyChanged(nameof(CanRunSfc));
            OnPropertyChanged(nameof(CanRunDism));

            RaiseMaintenanceCommandStates();
        }
    }

    public bool IsAnyMaintenanceRunning =>
        IsSfcRunning
        || IsDismRunning;

    public bool CanRunSfc =>
        !IsAnyMaintenanceRunning;

    public bool CanRunDism =>
        !IsAnyMaintenanceRunning;

    public bool HasSfcResult =>
        SfcResult.Status != MaintenanceToolStatus.NotRun
        && SfcResult.Status != MaintenanceToolStatus.Running;

    public bool HasDismResult =>
        DismResult.Status != MaintenanceToolStatus.NotRun
        && DismResult.Status != MaintenanceToolStatus.Running;

    public bool HasSfcTechnicalDetails =>
        HasSfcResult
        && HasTechnicalDetails(SfcResult);

    public bool HasDismTechnicalDetails =>
        HasDismResult
        && HasTechnicalDetails(DismResult);

    public string SfcButtonText =>
        IsSfcRunning
            ? "Systemdateiprüfung läuft …"
            : "Systemdateien prüfen";

    public string DismButtonText =>
        IsDismRunning
            ? "Windows-Abbildreparatur läuft …"
            : "Windows-Abbild reparieren";

    public string SfcStatusText =>
        GetStatusText(SfcResult.Status);

    public string DismStatusText =>
        GetStatusText(DismResult.Status);

    public string SfcSummary =>
        SfcResult.Summary;

    public string DismSummary =>
        DismResult.Summary;

    public string SfcDetails =>
        SfcResult.Details;

    public string DismDetails =>
        DismResult.Details;

    public string SfcExitCodeText =>
        GetExitCodeText(SfcResult);

    public string DismExitCodeText =>
        GetExitCodeText(DismResult);

    public string SfcDurationText =>
        GetDurationText(SfcResult);

    public string DismDurationText =>
        GetDurationText(DismResult);

    public string SfcFinishedAtText =>
        GetFinishedAtText(SfcResult);

    public string DismFinishedAtText =>
        GetFinishedAtText(DismResult);

    public string SfcTechnicalDetails =>
        BuildTechnicalDetails(
            SfcResult);

    public string DismTechnicalDetails =>
        BuildTechnicalDetails(
            DismResult);

    public ICommand RunSfcCommand { get; }

    public ICommand RunDismCommand { get; }

    private async Task RunSfcAsync()
    {
        if (!CanRunSfc)
        {
            return;
        }

        IsSfcRunning = true;

        SfcResult =
            new MaintenanceToolResult
            {
                Status = MaintenanceToolStatus.Running,
                Summary =
                    "Die geschützten Windows-Systemdateien werden überprüft.",
                Details =
                    "Dieser Vorgang kann einige Minuten dauern. "
                    + "Die Anwendung kann währenddessen weiter bedient werden.",
                StartedAt = DateTimeOffset.Now
            };

        try
        {
            SfcResult =
                await _systemFileChecker.RunAsync();
        }
        catch (Exception exception)
        {
            SfcResult =
                CreateUnexpectedFailureResult(
                    "Die Systemdateiprüfung konnte nicht abgeschlossen werden.",
                    exception);
        }
        finally
        {
            IsSfcRunning = false;
        }
    }

    private async Task RunDismAsync()
    {
        if (!CanRunDism)
        {
            return;
        }

        IsDismRunning = true;

        DismResult =
            new MaintenanceToolResult
            {
                Status = MaintenanceToolStatus.Running,
                Summary =
                    "Der Windows-Komponentenspeicher wird geprüft und repariert.",
                Details =
                    "DISM kann längere Zeit bei einem Fortschrittswert verweilen. "
                    + "Die Anwendung kann währenddessen weiter bedient werden.",
                StartedAt = DateTimeOffset.Now
            };

        try
        {
            DismResult =
                await _windowsImageRepairService.RunAsync();
        }
        catch (Exception exception)
        {
            DismResult =
                CreateUnexpectedFailureResult(
                    "Die Windows-Abbildreparatur konnte nicht abgeschlossen werden.",
                    exception);
        }
        finally
        {
            IsDismRunning = false;
        }
    }

    private static MaintenanceToolResult CreateUnexpectedFailureResult(
        string summary,
        Exception exception)
    {
        return new MaintenanceToolResult
        {
            Status = MaintenanceToolStatus.Failed,
            Summary = summary,
            Details =
                string.IsNullOrWhiteSpace(exception.Message)
                    ? "Es sind keine weiteren Fehlerdetails verfügbar."
                    : $"Technische Details: {exception.Message}",
            FinishedAt = DateTimeOffset.Now
        };
    }

    private static bool HasTechnicalDetails(
        MaintenanceToolResult result)
    {
        return !string.IsNullOrWhiteSpace(result.StandardOutput)
               || !string.IsNullOrWhiteSpace(result.StandardError)
               || result.ExitCode.HasValue;
    }

    private static string GetStatusText(
        MaintenanceToolStatus status)
    {
        return status switch
        {
            MaintenanceToolStatus.NotRun =>
                "Noch nicht ausgeführt",

            MaintenanceToolStatus.Running =>
                "Wird ausgeführt",

            MaintenanceToolStatus.Successful =>
                "Erfolgreich",

            MaintenanceToolStatus.SuccessfulWithRepairs =>
                "Erfolgreich – Reparatur durchgeführt",

            MaintenanceToolStatus.ActionRequired =>
                "Handlungsbedarf",

            MaintenanceToolStatus.RestartRequired =>
                "Neustart erforderlich",

            MaintenanceToolStatus.Skipped =>
                "Übersprungen",

            MaintenanceToolStatus.Failed =>
                "Fehlgeschlagen",

            _ =>
                "Unbekannter Status"
        };
    }

    private static string GetExitCodeText(
        MaintenanceToolResult result)
    {
        return result.ExitCode.HasValue
            ? result.ExitCode.Value.ToString()
            : "Nicht verfügbar";
    }

    private static string GetDurationText(
        MaintenanceToolResult result)
    {
        if (!result.StartedAt.HasValue
            || !result.FinishedAt.HasValue)
        {
            return "Nicht verfügbar";
        }

        var duration =
            result.Duration;

        if (duration.TotalMinutes >= 1)
        {
            return $"{(int)duration.TotalMinutes} Min. {duration.Seconds} Sek.";
        }

        return $"{Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds))} Sek.";
    }

    private static string GetFinishedAtText(
        MaintenanceToolResult result)
    {
        return result.FinishedAt.HasValue
            ? result.FinishedAt.Value
                .ToLocalTime()
                .ToString("dd.MM.yyyy HH:mm")
            : "Noch nicht ausgeführt";
    }

    private static string BuildTechnicalDetails(
        MaintenanceToolResult result)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            $"Exitcode: {GetExitCodeText(result)}");

        builder.AppendLine(
            $"Dauer: {GetDurationText(result)}");

        builder.AppendLine(
            $"Abgeschlossen: {GetFinishedAtText(result)}");

        if (!string.IsNullOrWhiteSpace(
            result.StandardOutput))
        {
            builder.AppendLine();
            builder.AppendLine("Standardausgabe:");
            builder.AppendLine(result.StandardOutput.Trim());
        }

        if (!string.IsNullOrWhiteSpace(
            result.StandardError))
        {
            builder.AppendLine();
            builder.AppendLine("Fehlerausgabe:");
            builder.AppendLine(result.StandardError.Trim());
        }

        return builder
            .ToString()
            .Trim();
    }

    private void RaiseMaintenanceCommandStates()
    {
        _runSfcCommand.RaiseCanExecuteChanged();
        _runDismCommand.RaiseCanExecuteChanged();
    }

    private void NotifySfcResultPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasSfcResult));
        OnPropertyChanged(nameof(HasSfcTechnicalDetails));
        OnPropertyChanged(nameof(SfcStatusText));
        OnPropertyChanged(nameof(SfcSummary));
        OnPropertyChanged(nameof(SfcDetails));
        OnPropertyChanged(nameof(SfcExitCodeText));
        OnPropertyChanged(nameof(SfcDurationText));
        OnPropertyChanged(nameof(SfcFinishedAtText));
        OnPropertyChanged(nameof(SfcTechnicalDetails));
    }

    private void NotifyDismResultPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasDismResult));
        OnPropertyChanged(nameof(HasDismTechnicalDetails));
        OnPropertyChanged(nameof(DismStatusText));
        OnPropertyChanged(nameof(DismSummary));
        OnPropertyChanged(nameof(DismDetails));
        OnPropertyChanged(nameof(DismExitCodeText));
        OnPropertyChanged(nameof(DismDurationText));
        OnPropertyChanged(nameof(DismFinishedAtText));
        OnPropertyChanged(nameof(DismTechnicalDetails));
    }
}
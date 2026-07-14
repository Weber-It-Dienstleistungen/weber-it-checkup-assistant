using System.Text;
using System.Windows.Input;
using WeberIT.Checkup.App.Infrastructure.Commands;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.ViewModels;

public class MaintenanceViewModel : BaseViewModel
{
    private readonly ISystemFileChecker _systemFileChecker;
    private readonly AsyncRelayCommand _runSfcCommand;

    private MaintenanceToolResult _sfcResult =
        new();

    private bool _isSfcRunning;

    public MaintenanceViewModel(
        ISystemFileChecker systemFileChecker)
    {
        _systemFileChecker = systemFileChecker;

        _runSfcCommand =
            new AsyncRelayCommand(
                RunSfcAsync,
                () => !IsSfcRunning);

        RunSfcCommand = _runSfcCommand;
    }

    public string Title =>
        "Werkzeuge";

    public string Subtitle =>
        "Windows-Wartungswerkzeuge kontrolliert ausführen und Ergebnisse prüfen.";

    public string AdministratorNotice =>
        "Für die Systemdateiprüfung sind Administratorrechte erforderlich. "
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
            OnPropertyChanged(nameof(SfcButtonText));
            OnPropertyChanged(nameof(SfcStatusText));
            OnPropertyChanged(nameof(CanRunSfc));

            _runSfcCommand.RaiseCanExecuteChanged();
        }
    }

    public bool CanRunSfc =>
        !IsSfcRunning;

    public bool HasSfcResult =>
        SfcResult.Status != MaintenanceToolStatus.NotRun
        && SfcResult.Status != MaintenanceToolStatus.Running;

    public bool HasSfcTechnicalDetails =>
        HasSfcResult
        && (!string.IsNullOrWhiteSpace(SfcResult.StandardOutput)
            || !string.IsNullOrWhiteSpace(SfcResult.StandardError)
            || SfcResult.ExitCode.HasValue);

    public string SfcButtonText =>
        IsSfcRunning
            ? "Systemdateiprüfung läuft …"
            : "Systemdateien prüfen";

    public string SfcStatusText =>
        SfcResult.Status switch
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

    public string SfcSummary =>
        SfcResult.Summary;

    public string SfcDetails =>
        SfcResult.Details;

    public string SfcExitCodeText =>
        SfcResult.ExitCode.HasValue
            ? SfcResult.ExitCode.Value.ToString()
            : "Nicht verfügbar";

    public string SfcDurationText
    {
        get
        {
            if (!SfcResult.StartedAt.HasValue
                || !SfcResult.FinishedAt.HasValue)
            {
                return "Nicht verfügbar";
            }

            var duration =
                SfcResult.Duration;

            if (duration.TotalMinutes >= 1)
            {
                return $"{(int)duration.TotalMinutes} Min. {duration.Seconds} Sek.";
            }

            return $"{Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds))} Sek.";
        }
    }

    public string SfcFinishedAtText =>
        SfcResult.FinishedAt.HasValue
            ? SfcResult.FinishedAt.Value
                .ToLocalTime()
                .ToString("dd.MM.yyyy HH:mm")
            : "Noch nicht ausgeführt";

    public string SfcTechnicalDetails =>
        BuildTechnicalDetails();

    public ICommand RunSfcCommand { get; }

    private async Task RunSfcAsync()
    {
        if (IsSfcRunning)
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
                new MaintenanceToolResult
                {
                    Status = MaintenanceToolStatus.Failed,
                    Summary =
                        "Die Systemdateiprüfung konnte nicht abgeschlossen werden.",
                    Details =
                        string.IsNullOrWhiteSpace(exception.Message)
                            ? "Es sind keine weiteren Fehlerdetails verfügbar."
                            : $"Technische Details: {exception.Message}",
                    FinishedAt = DateTimeOffset.Now
                };
        }
        finally
        {
            IsSfcRunning = false;
        }
    }

    private string BuildTechnicalDetails()
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            $"Exitcode: {SfcExitCodeText}");

        builder.AppendLine(
            $"Dauer: {SfcDurationText}");

        builder.AppendLine(
            $"Abgeschlossen: {SfcFinishedAtText}");

        if (!string.IsNullOrWhiteSpace(
            SfcResult.StandardOutput))
        {
            builder.AppendLine();
            builder.AppendLine("Standardausgabe:");
            builder.AppendLine(SfcResult.StandardOutput.Trim());
        }

        if (!string.IsNullOrWhiteSpace(
            SfcResult.StandardError))
        {
            builder.AppendLine();
            builder.AppendLine("Fehlerausgabe:");
            builder.AppendLine(SfcResult.StandardError.Trim());
        }

        return builder
            .ToString()
            .Trim();
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
}
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Views.Dialogs;

public partial class ProgramUpdateExecutionDialog :
    Window,
    INotifyPropertyChanged
{
    private const int MaximumDisplayedOutputLength =
        4000;

    private readonly CheckupTaskActionPlan _plan;

    private readonly IProgramUpdateActionExecutor
        _executor;

    private bool _executionWasStarted;

    private bool _isRunning;

    private string _executionStatusText =
        "Ausführung wird vorbereitet";

    private string _executionDetailText =
        "Der bestätigte Aktionsplan wird vor dem Start erneut geprüft.";

    private string _footerStatusText =
        "Vorbereitung läuft";

    public ProgramUpdateExecutionDialog(
        CheckupTaskActionPlan plan,
        IProgramUpdateActionExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(
            plan);

        ArgumentNullException.ThrowIfNull(
            executor);

        plan.Validate();

        _plan =
            plan;

        _executor =
            executor;

        ActionTitle =
            plan.ActionTitle;

        InitializeComponent();

        DataContext =
            this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ActionTitle { get; }

    public ObservableCollection<CommandExecutionDisplayItem>
        CommandResults
    { get; } =
            new();

    public bool IsRunning
    {
        get =>
            _isRunning;

        private set
        {
            if (_isRunning == value)
            {
                return;
            }

            _isRunning =
                value;

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(CanClose));
        }
    }

    public bool CanClose =>
        !IsRunning;

    public string ExecutionStatusText
    {
        get =>
            _executionStatusText;

        private set
        {
            if (_executionStatusText == value)
            {
                return;
            }

            _executionStatusText =
                value;

            OnPropertyChanged();
        }
    }

    public string ExecutionDetailText
    {
        get =>
            _executionDetailText;

        private set
        {
            if (_executionDetailText == value)
            {
                return;
            }

            _executionDetailText =
                value;

            OnPropertyChanged();
        }
    }

    public string FooterStatusText
    {
        get =>
            _footerStatusText;

        private set
        {
            if (_footerStatusText == value)
            {
                return;
            }

            _footerStatusText =
                value;

            OnPropertyChanged();
        }
    }

    public string ResultCountText =>
        CommandResults.Count switch
        {
            0 =>
                "Noch kein Einzelbefehl abgeschlossen",

            1 =>
                "Ein Einzelbefehl wurde protokolliert",

            _ =>
                $"{CommandResults.Count} Einzelbefehle "
                + "wurden protokolliert"
        };

    private async void Window_OnContentRendered(
        object sender,
        EventArgs e)
    {
        if (_executionWasStarted)
        {
            return;
        }

        _executionWasStarted =
            true;

        await ExecutePlanAsync();
    }

    private async Task ExecutePlanAsync()
    {
        IsRunning =
            true;

        ExecutionStatusText =
            "Programmupdates werden ausgeführt";

        ExecutionDetailText =
            "WinGet verarbeitet ausschließlich die zuvor "
            + "ausgewählten und bestätigten Pakete.";

        FooterStatusText =
            "Ausführung läuft – Fenster nicht schließen";

        try
        {
            var executionResult =
                await _executor.ExecuteAsync(
                    _plan);

            PopulateCommandResults(
                executionResult);

            ApplyExecutionResult(
                executionResult);
        }
        catch (Exception exception)
        {
            ExecutionStatusText =
                "Ausführung unerwartet abgebrochen";

            ExecutionDetailText =
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Der Aktionsplan konnte nicht "
                      + "ausgeführt werden."
                    : exception.Message;

            FooterStatusText =
                "Technischer Fehler";
        }
        finally
        {
            IsRunning =
                false;
        }
    }

    private void PopulateCommandResults(
        ProgramUpdateActionExecutionResult executionResult)
    {
        CommandResults.Clear();

        for (var index = 0;
             index < executionResult.CommandResults.Count;
             index++)
        {
            var processResult =
                executionResult.CommandResults[index];

            var commandText =
                index < _plan.Commands.Count
                    ? _plan.Commands[index].DisplayText
                    : "Nicht zuordenbarer Einzelbefehl";

            CommandResults.Add(
                new CommandExecutionDisplayItem(
                    commandText,
                    processResult));
        }

        OnPropertyChanged(
            nameof(ResultCountText));
    }

    private void ApplyExecutionResult(
        ProgramUpdateActionExecutionResult executionResult)
    {
        if (executionResult.WasBlocked)
        {
            ExecutionStatusText =
                "Ausführung blockiert";

            ExecutionDetailText =
                string.IsNullOrWhiteSpace(
                    executionResult.ErrorMessage)
                    ? "Eine andere Systemaktion wird "
                      + "bereits ausgeführt."
                    : executionResult.ErrorMessage;

            FooterStatusText =
                "Kein Befehl wurde gestartet";

            return;
        }

        if (executionResult.IsSuccessful)
        {
            ExecutionStatusText =
                "Programmupdates technisch abgeschlossen";

            ExecutionDetailText =
                executionResult.CompletedCommandCount == 1
                    ? "Der ausgewählte WinGet-Befehl wurde "
                      + "technisch erfolgreich beendet."
                    : $"{executionResult.CompletedCommandCount} "
                      + "ausgewählte WinGet-Befehle wurden "
                      + "technisch erfolgreich beendet.";

            FooterStatusText =
                "Abschlusskontrolle durch neuen Scan erforderlich";

            return;
        }

        ExecutionStatusText =
            "Programmupdateaktion nicht vollständig abgeschlossen";

        ExecutionDetailText =
            string.IsNullOrWhiteSpace(
                executionResult.ErrorMessage)
                ? "Mindestens ein WinGet-Befehl wurde nicht "
                  + "technisch erfolgreich beendet."
                : executionResult.ErrorMessage;

        FooterStatusText =
            executionResult.WasStarted
                ? "Weitere ausgewählte Befehle wurden nicht gestartet"
                : "Kein Updatebefehl wurde erfolgreich gestartet";
    }

    private void Window_OnClosing(
        object? sender,
        CancelEventArgs e)
    {
        if (!IsRunning)
        {
            return;
        }

        e.Cancel =
            true;
    }

    private void CloseButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (IsRunning)
        {
            return;
        }

        Close();
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }

    public sealed class CommandExecutionDisplayItem
    {
        public CommandExecutionDisplayItem(
            string commandText,
            ProcessExecutionResult result)
        {
            ArgumentNullException.ThrowIfNull(
                result);

            CommandText =
                commandText;

            StatusText =
                BuildStatusText(
                    result);

            DetailText =
                BuildDetailText(
                    result);

            OutputText =
                BuildOutputText(
                    result);
        }

        public string CommandText { get; }

        public string StatusText { get; }

        public string DetailText { get; }

        public string OutputText { get; }

        public bool HasOutput =>
            !string.IsNullOrWhiteSpace(
                OutputText);

        private static string BuildStatusText(
            ProcessExecutionResult result)
        {
            if (result.ElevationWasCancelled)
            {
                return "Abgebrochen";
            }

            if (!result.WasStarted)
            {
                return "Nicht gestartet";
            }

            return result.ExitCode == 0
                ? "Erfolgreich"
                : "Fehlgeschlagen";
        }

        private static string BuildDetailText(
            ProcessExecutionResult result)
        {
            if (!string.IsNullOrWhiteSpace(
                    result.ErrorMessage))
            {
                return result.ErrorMessage;
            }

            if (!result.WasStarted)
            {
                return "Der Prozess wurde nicht gestartet.";
            }

            var exitCodeText =
                result.ExitCode.HasValue
                    ? result.ExitCode.Value.ToString()
                    : "nicht verfügbar";

            return $"Exitcode: {exitCodeText} · "
                   + $"Dauer: {result.Duration.TotalSeconds:0.0} Sekunden";
        }

        private static string BuildOutputText(
            ProcessExecutionResult result)
        {
            var outputParts =
                new List<string>();

            if (!string.IsNullOrWhiteSpace(
                    result.StandardOutput))
            {
                outputParts.Add(
                    "Standardausgabe:"
                    + Environment.NewLine
                    + result.StandardOutput.Trim());
            }

            if (!string.IsNullOrWhiteSpace(
                    result.StandardError))
            {
                outputParts.Add(
                    "Fehlerausgabe:"
                    + Environment.NewLine
                    + result.StandardError.Trim());
            }

            var output =
                string.Join(
                    Environment.NewLine
                    + Environment.NewLine,
                    outputParts);

            if (output.Length
                <= MaximumDisplayedOutputLength)
            {
                return output;
            }

            return output[
                       ..MaximumDisplayedOutputLength]
                   + Environment.NewLine
                   + "… Ausgabe für die Anzeige gekürzt.";
        }
    }
}
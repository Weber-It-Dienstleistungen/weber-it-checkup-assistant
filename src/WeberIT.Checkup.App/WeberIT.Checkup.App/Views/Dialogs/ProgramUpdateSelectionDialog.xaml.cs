using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;
using WeberIT.Checkup.App.Services.Tasks;

namespace WeberIT.Checkup.App.Views.Dialogs;

public partial class ProgramUpdateSelectionDialog :
    Window,
    INotifyPropertyChanged
{
    private const int MaximumPersistedOutputLength =
        4000;

    private readonly CheckupTask _task;

    private readonly ProgramUpdateInformation
        _programUpdateInformation;

    private readonly CheckupTaskList _taskList;

    private string _selectionStatusText =
        "Noch kein Programmupdate ausgewählt.";

    public ProgramUpdateSelectionDialog(
        CheckupTask task,
        ProgramUpdateInformation programUpdateInformation,
        CheckupTaskList taskList)
    {
        ArgumentNullException.ThrowIfNull(
            task);

        ArgumentNullException.ThrowIfNull(
            programUpdateInformation);

        ArgumentNullException.ThrowIfNull(
            taskList);

        _task =
            task;

        _programUpdateInformation =
            programUpdateInformation;

        _taskList =
            taskList;

        AvailableUpdates =
            programUpdateInformation
                .AvailableUpdates
                .OrderBy(
                    update =>
                        update.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(
                    update =>
                        update.PackageId,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        InitializeComponent();

        DataContext =
            this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<AvailableProgramUpdate>
        AvailableUpdates
    { get; }

    public string AvailableUpdateCountText =>
        AvailableUpdates.Count == 1
            ? "1 erkanntes Programmupdate"
            : $"{AvailableUpdates.Count} erkannte Programmupdates";

    public string SelectionStatusText
    {
        get =>
            _selectionStatusText;

        private set
        {
            if (_selectionStatusText == value)
            {
                return;
            }

            _selectionStatusText =
                value;

            OnPropertyChanged();
        }
    }

    private void SelectAllButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        UpdatesListBox.SelectAll();
    }

    private void ClearSelectionButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        UpdatesListBox.UnselectAll();
    }

    private void UpdatesListBox_OnSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        var selectedCount =
            UpdatesListBox.SelectedItems.Count;

        PreviewPlanButton.IsEnabled =
            selectedCount > 0;

        SelectionStatusText =
            selectedCount switch
            {
                0 =>
                    "Noch kein Programmupdate ausgewählt.",

                1 =>
                    "Ein Programmupdate ist für die "
                    + "Planvorschau ausgewählt.",

                _ =>
                    $"{selectedCount} Programmupdates sind "
                    + "für die Planvorschau ausgewählt."
            };
    }

    private void PreviewPlanButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        CheckupTaskActionPlan plan;

        try
        {
            plan =
                BuildPlan();
        }
        catch (Exception exception)
        {
            ShowMessage(
                "Aktionsplan nicht erstellt",
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Der Aktionsplan konnte nicht "
                      + "erstellt werden."
                    : exception.Message);

            return;
        }

        var previewDialog =
            new TaskActionPlanPreviewDialog(
                plan)
            {
                Owner =
                    this
            };

        var previewResult =
            previewDialog.ShowDialog();

        if (previewResult != true
            || !previewDialog.IsExecutionConfirmed)
        {
            return;
        }

        try
        {
            ExecuteConfirmedPlan(
                plan);
        }
        catch (Exception exception)
        {
            ShowMessage(
                "Aktionsplan nicht ausgeführt",
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Der Aktionsplan konnte nicht "
                      + "ausgeführt werden."
                    : exception.Message);
        }
    }

    private CheckupTaskActionPlan BuildPlan()
    {
        var selections =
            UpdatesListBox
                .SelectedItems
                .Cast<AvailableProgramUpdate>()
                .Select(
                    update =>
                        new ProgramUpdateActionSelection
                        {
                            PackageId =
                                update.PackageId,

                            Source =
                                update.Source
                        })
                .ToList();

        var planBuilder =
            new ProgramUpdateActionPlanBuilder();

        return planBuilder.Build(
            _task,
            _programUpdateInformation,
            selections);
    }

    private void ExecuteConfirmedPlan(
        CheckupTaskActionPlan plan)
    {
        var application =
            Application.Current as App
            ?? throw new InvalidOperationException(
                "Der zentrale Anwendungsdienst ist "
                + "nicht verfügbar.");

        var executor =
            application.Services
                .GetRequiredService<
                    IProgramUpdateActionExecutor>();

        var executionDialog =
            new ProgramUpdateExecutionDialog(
                plan,
                executor)
            {
                Owner =
                    this
            };

        executionDialog.ShowDialog();

        var executionResult =
            executionDialog.ExecutionResult;

        if (executionResult is null
            || executionResult.WasBlocked
            || executionResult.CommandResults.Count == 0)
        {
            return;
        }

        PersistExecutionResult(
            plan,
            executionResult);
    }

    private void PersistExecutionResult(
        CheckupTaskActionPlan plan,
        ProgramUpdateActionExecutionResult executionResult)
    {
        var actionResult =
            CreateTaskActionResult(
                plan,
                executionResult);

        try
        {
            _taskList.AddTaskActionResult(
                _task,
                actionResult);
        }
        catch (Exception exception)
        {
            ShowMessage(
                "Aktion ausgeführt – Dokumentation fehlgeschlagen",
                "Mindestens ein WinGet-Befehl wurde bereits "
                + "gestartet, das technische Ergebnis konnte "
                + "jedoch nicht im Checkup gespeichert werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Die Updateaktion darf nicht ungeprüft "
                + "wiederholt werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Technische Ursache:"
                + Environment.NewLine
                + exception.Message);
        }
    }

    private static CheckupTaskActionResult
        CreateTaskActionResult(
            CheckupTaskActionPlan plan,
            ProgramUpdateActionExecutionResult executionResult)
    {
        var lastProcessResult =
            executionResult.CommandResults
                .LastOrDefault();

        return new CheckupTaskActionResult
        {
            ActionCode =
                plan.ActionCode,

            ActionTitle =
                plan.ActionTitle,

            TargetDescription =
                plan.TargetDescription,

            Status =
                DetermineActionStatus(
                    executionResult),

            Summary =
                BuildResultSummary(
                    executionResult),

            Details =
                BuildTechnicalDetails(
                    plan,
                    executionResult),

            ExitCode =
                lastProcessResult?.ExitCode,

            RestartRequired =
                false,

            StartedAt =
                executionResult.StartedAt,

            FinishedAt =
                executionResult.FinishedAt
        };
    }

    private static CheckupTaskActionStatus
        DetermineActionStatus(
            ProgramUpdateActionExecutionResult executionResult)
    {
        if (executionResult.IsSuccessful)
        {
            return CheckupTaskActionStatus.Successful;
        }

        if (!executionResult.WasStarted
            && executionResult.CommandResults.Any(
                result =>
                    result.ElevationWasCancelled))
        {
            return CheckupTaskActionStatus.Cancelled;
        }

        return CheckupTaskActionStatus.Failed;
    }

    private static string BuildResultSummary(
        ProgramUpdateActionExecutionResult executionResult)
    {
        if (executionResult.IsSuccessful)
        {
            return executionResult.CompletedCommandCount == 1
                ? "Ein ausgewähltes Programmupdate wurde "
                  + "technisch erfolgreich verarbeitet. "
                  + "Die Abschlusskontrolle steht aus."
                : $"{executionResult.CompletedCommandCount} "
                  + "ausgewählte Programmupdates wurden "
                  + "technisch erfolgreich verarbeitet. "
                  + "Die Abschlusskontrolle steht aus.";
        }

        if (!executionResult.WasStarted
            && executionResult.CommandResults.Any(
                result =>
                    result.ElevationWasCancelled))
        {
            return "Die technische Aktion wurde vor dem "
                   + "Prozessstart abgebrochen.";
        }

        return "Die Programmupdateaktion wurde nicht "
               + "vollständig technisch erfolgreich beendet.";
    }

    private static string BuildTechnicalDetails(
        CheckupTaskActionPlan plan,
        ProgramUpdateActionExecutionResult executionResult)
    {
        var builder =
            new StringBuilder();

        if (!string.IsNullOrWhiteSpace(
                executionResult.ErrorMessage))
        {
            builder.AppendLine(
                executionResult.ErrorMessage.Trim());

            builder.AppendLine();
        }

        for (var index = 0;
             index < executionResult.CommandResults.Count;
             index++)
        {
            var processResult =
                executionResult.CommandResults[index];

            var commandText =
                index < plan.Commands.Count
                    ? plan.Commands[index].DisplayText
                    : "Nicht zuordenbarer Einzelbefehl";

            builder.AppendLine(
                commandText);

            builder.Append("Gestartet: ");
            builder.AppendLine(
                processResult.WasStarted
                    ? "Ja"
                    : "Nein");

            builder.Append("Exitcode: ");
            builder.AppendLine(
                processResult.ExitCode?.ToString()
                ?? "Nicht verfügbar");

            AppendOutput(
                builder,
                "Standardausgabe",
                processResult.StandardOutput);

            AppendOutput(
                builder,
                "Fehlerausgabe",
                processResult.StandardError);

            if (!string.IsNullOrWhiteSpace(
                    processResult.ErrorMessage))
            {
                builder.Append("Technischer Fehler: ");
                builder.AppendLine(
                    LimitOutput(
                        processResult.ErrorMessage));
            }

            if (index
                < executionResult.CommandResults.Count - 1)
            {
                builder.AppendLine();
            }
        }

        return builder
            .ToString()
            .Trim();
    }

    private static void AppendOutput(
        StringBuilder builder,
        string heading,
        string output)
    {
        if (string.IsNullOrWhiteSpace(
                output))
        {
            return;
        }

        builder.Append(heading);
        builder.AppendLine(":");
        builder.AppendLine(
            LimitOutput(
                output));
    }

    private static string LimitOutput(
        string output)
    {
        var trimmedOutput =
            output.Trim();

        if (trimmedOutput.Length
            <= MaximumPersistedOutputLength)
        {
            return trimmedOutput;
        }

        return trimmedOutput[
                   ..MaximumPersistedOutputLength]
               + Environment.NewLine
               + "… Ausgabe für die Speicherung gekürzt.";
    }

    private void CloseButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }

    private void ShowMessage(
        string title,
        string message)
    {
        var dialog =
            new MessageDialog(
                title,
                message)
            {
                Owner =
                    this
            };

        dialog.ShowDialog();
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }
}
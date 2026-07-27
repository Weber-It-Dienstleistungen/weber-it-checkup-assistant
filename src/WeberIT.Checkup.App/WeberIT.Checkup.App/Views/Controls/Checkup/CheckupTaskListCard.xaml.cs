using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;
using WeberIT.Checkup.App.Services.Tasks;
using WeberIT.Checkup.App.Views.Dialogs;

namespace WeberIT.Checkup.App.Views.Controls.Checkup;

public partial class CheckupTaskListCard : UserControl
{
    public static readonly DependencyProperty CheckupSessionProperty =
        DependencyProperty.Register(
            nameof(CheckupSession),
            typeof(CheckupSession),
            typeof(CheckupTaskListCard),
            new PropertyMetadata(
                null));

    private Button? _completionCheckButton;

    private bool _isCompletionCheckRunning;

    public CheckupTaskListCard()
    {
        InitializeComponent();

        AddCompletionCheckPanel();
        AddStatusEditor();

        Loaded +=
            CheckupTaskListCard_OnLoaded;

        DataContextChanged +=
            CheckupTaskListCard_OnDataContextChanged;
    }

    public CheckupSession? CheckupSession
    {
        get =>
            (CheckupSession?)GetValue(
                CheckupSessionProperty);

        set =>
            SetValue(
                CheckupSessionProperty,
                value);
    }

    private void CheckupTaskListCard_OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        EnsureStandardCustomerCheckupTasks();
    }

    private void CheckupTaskListCard_OnDataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        EnsureStandardCustomerCheckupTasks();
    }

    private void EnsureStandardCustomerCheckupTasks()
    {
        if (CheckupSession is null
            || !CheckupSession
                .HasInProgressCustomerCheckupVisit
            || DataContext
                is not CheckupTaskList taskList
            || !ReferenceEquals(
                CheckupSession.TaskList,
                taskList))
        {
            return;
        }

        try
        {
            taskList.EnsureTask(
                CheckupStandardTaskCatalog
                    .CreateSystemFileCheckTask(
                        taskList.CreatedAt
                        ?? DateTime.Now),
                CheckupStandardTaskCatalog
                    .CurrentTaskListVersion);
        }
        catch (Exception exception)
        {
            ShowPreparationError(
                "Die Standardaufgabe zur "
                + "Systemdateiprüfung konnte für den "
                + "laufenden Kundencheckup nicht sicher "
                + "nachgerüstet und gespeichert werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Technische Ursache: "
                + exception.Message);
        }
    }

    private void AddCompletionCheckPanel()
    {
        if (Content is not Border rootBorder
            || rootBorder.Child
                is not StackPanel rootStackPanel)
        {
            return;
        }

        var panel =
            new Border
            {
                Margin =
                    new Thickness(
                        0,
                        18,
                        0,
                        0),

                Padding =
                    new Thickness(
                        16),

                BorderThickness =
                    new Thickness(
                        1),

                CornerRadius =
                    new CornerRadius(
                        8)
            };

        panel.SetResourceReference(
            Border.BackgroundProperty,
            "SurfaceSecondaryBrush");

        panel.SetResourceReference(
            Border.BorderBrushProperty,
            "InformationBrush");

        panel.SetBinding(
            VisibilityProperty,
            new Binding(
                nameof(
                    CheckupTaskList
                        .ShouldShowCompletionCheckPanel))
            {
                Converter =
                    new BooleanToVisibilityConverter()
            });

        var contentPanel =
            new StackPanel();

        var titleText =
            new TextBlock
            {
                Text =
                    "Abschlusskontrolle",

                FontSize =
                    14,

                FontWeight =
                    FontWeights.SemiBold
            };

        titleText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "TextPrimaryBrush");

        contentPanel.Children.Add(
            titleText);

        var explanationText =
            new TextBlock
            {
                Margin =
                    new Thickness(
                        0,
                        6,
                        0,
                        0),

                Text =
                    "Ein neuer lesender Systemscan prüft, "
                    + "ob erfolgreich bearbeitete Befunde "
                    + "weiterhin vorhanden sind. Der "
                    + "ursprüngliche Checkup bleibt erhalten.",

                TextWrapping =
                    TextWrapping.Wrap
            };

        explanationText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "TextSecondaryBrush");

        contentPanel.Children.Add(
            explanationText);

        var statusText =
            new TextBlock
            {
                Margin =
                    new Thickness(
                        0,
                        9,
                        0,
                        0),

                FontSize =
                    11,

                TextWrapping =
                    TextWrapping.Wrap
            };

        statusText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "InformationBrush");

        statusText.SetBinding(
            TextBlock.TextProperty,
            new Binding(
                nameof(
                    CheckupTaskList
                        .CompletionCheckStatusText)));

        contentPanel.Children.Add(
            statusText);

        _completionCheckButton =
            new Button
            {
                Margin =
                    new Thickness(
                        0,
                        12,
                        0,
                        0),

                MinWidth =
                    220,

                Height =
                    40,

                HorizontalAlignment =
                    HorizontalAlignment.Left
            };

        _completionCheckButton.SetResourceReference(
            FrameworkElement.StyleProperty,
            "AccentButtonStyle");

        _completionCheckButton.SetBinding(
            ContentControl.ContentProperty,
            new Binding(
                nameof(
                    CheckupTaskList
                        .CompletionCheckButtonText)));

        _completionCheckButton.SetBinding(
            VisibilityProperty,
            new Binding(
                nameof(
                    CheckupTaskList
                        .HasTasksAwaitingVerification))
            {
                Converter =
                    new BooleanToVisibilityConverter()
            });

        _completionCheckButton.Click +=
            CompletionCheckButton_OnClick;

        contentPanel.Children.Add(
            _completionCheckButton);

        panel.Child =
            contentPanel;

        rootStackPanel.Children.Add(
            panel);
    }

    private void AddStatusEditor()
    {
        if (Content is not Border rootBorder
            || rootBorder.Child
                is not StackPanel rootStackPanel)
        {
            return;
        }

        var statusEditor =
            new CheckupTaskStatusEditor
            {
                Margin =
                    new Thickness(
                        0,
                        18,
                        0,
                        0)
            };

        rootStackPanel.Children.Add(
            statusEditor);
    }

    private async void ActionDetailsButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button
            || button.DataContext
                is not CheckupTaskActionDefinition definition)
        {
            return;
        }

        var detailsDialog =
            new TaskActionDetailsDialog(
                definition)
            {
                Owner =
                    Window.GetWindow(
                        this)
            };

        detailsDialog.ShowDialog();

        if (!CheckupStandardTaskCatalog
                .IsMaintenanceActionCode(
                    definition.ActionCode))
        {
            return;
        }

        var task =
            FindTaskContext(
                button);

        if (task is null)
        {
            ShowPreparationError(
                "Die Wartungsaktion konnte keiner "
                + "eindeutigen Aufgabe zugeordnet werden.");

            return;
        }

        if (CheckupSession is null
            || DataContext
                is not CheckupTaskList taskList
            || !ReferenceEquals(
                CheckupSession.TaskList,
                taskList))
        {
            ShowPreparationError(
                "Der zugehörige Checkup- und "
                + "Aufgabenkontext ist nicht vollständig "
                + "verfügbar.");

            return;
        }

        await ExecuteMaintenanceActionAsync(
            button,
            task,
            taskList,
            definition);
    }

    private void ProgramUpdateSelectionButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button
            || button.CommandParameter
                is not CheckupTask task)
        {
            ShowPreparationError(
                "Die zugehörige Aufgabe konnte nicht "
                + "eindeutig bestimmt werden.");

            return;
        }

        if (CheckupSession is null)
        {
            ShowPreparationError(
                "Der zugehörige Checkup-Kontext ist "
                + "nicht verfügbar.");

            return;
        }

        var dialog =
            new ProgramUpdateSelectionDialog(
                task,
                CheckupSession.ProgramUpdateInformation,
                CheckupSession.TaskList)
            {
                Owner =
                    Window.GetWindow(
                        this)
            };

        dialog.ShowDialog();
    }

    private void CleanupSelectionButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button
            || button.CommandParameter
                is not CheckupTask task)
        {
            ShowPreparationError(
                "Die zugehörige Aufgabe konnte nicht "
                + "eindeutig bestimmt werden.");

            return;
        }

        if (CheckupSession is null)
        {
            ShowPreparationError(
                "Der zugehörige Checkup-Kontext ist "
                + "nicht verfügbar.");

            return;
        }

        try
        {
            var dialog =
                new CleanupActionSelectionDialog(
                    task,
                    CheckupSession.CleanupPotentialInformation,
                    CheckupSession.TaskList)
                {
                    Owner =
                        Window.GetWindow(
                            this)
                };

            dialog.ShowDialog();
        }
        catch (Exception exception)
        {
            ShowPreparationError(
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Die auswählbaren "
                      + "Bereinigungskategorien konnten "
                      + "nicht sicher bestimmt werden."
                    : exception.Message);
        }
    }

    private async Task ExecuteMaintenanceActionAsync(
        Button sourceButton,
        CheckupTask task,
        CheckupTaskList taskList,
        CheckupTaskActionDefinition definition)
    {
        var application =
            Application.Current as App;

        if (application is null)
        {
            ShowMaintenanceError(
                "Der zentrale Anwendungsdienst ist "
                + "nicht verfügbar.");

            return;
        }

        var dialogService =
            application.Services
                .GetRequiredService<
                    IDialogService>();

        var confirmed =
            dialogService.Confirm(
                GetMaintenanceConfirmationTitle(
                    task.TaskCode),
                BuildMaintenanceConfirmationMessage(
                    task.TaskCode,
                    definition));

        if (!confirmed)
        {
            return;
        }

        var executionCoordinator =
            application.Services
                .GetRequiredService<
                    ICheckupTaskActionExecutionCoordinator>();

        using var executionLease =
            executionCoordinator.TryBeginExecution(
                definition.ActionCode,
                definition.ActionTitle);

        if (executionLease is null)
        {
            var activeActionTitle =
                executionCoordinator
                    .ActiveActionTitle;

            ShowMaintenanceError(
                string.IsNullOrWhiteSpace(
                    activeActionTitle)
                    ? "Die Wartungsaktion kann nicht gestartet "
                      + "werden, weil bereits eine andere "
                      + "Systemaktion läuft."
                    : "Die Wartungsaktion kann nicht gestartet "
                      + "werden. Aktuell läuft bereits: "
                      + activeActionTitle
                      + ".");

            return;
        }

        var previousContent =
            sourceButton.Content;

        sourceButton.IsEnabled =
            false;

        sourceButton.Content =
            GetMaintenanceRunningText(
                task.TaskCode);

        var fallbackStartedAt =
            DateTimeOffset.Now;

        try
        {
            var rawResult =
                await RunMaintenanceToolAsync(
                    application,
                    task.TaskCode);

            var result =
                NormalizeMaintenanceResult(
                    rawResult,
                    fallbackStartedAt);

            var actionResult =
                CreateMaintenanceActionResult(
                    task,
                    definition,
                    result);

            PersistMaintenanceResult(
                task,
                taskList,
                actionResult,
                result);

            ShowMaintenanceResult(
                task,
                result);
        }
        catch (Exception exception)
        {
            ShowMaintenanceError(
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Die Wartungsaktion konnte nicht "
                      + "sicher abgeschlossen und dokumentiert "
                      + "werden."
                    : exception.Message);
        }
        finally
        {
            sourceButton.Content =
                previousContent;

            sourceButton.IsEnabled =
                true;
        }
    }

    private static async Task<MaintenanceToolResult>
        RunMaintenanceToolAsync(
            App application,
            string taskCode)
    {
        if (CheckupStandardTaskCatalog
            .IsSystemFileCheckTask(
                taskCode))
        {
            var systemFileChecker =
                application.Services
                    .GetRequiredService<
                        ISystemFileChecker>();

            return await systemFileChecker
                .RunAsync();
        }

        if (CheckupStandardTaskCatalog
            .IsWindowsImageRepairTask(
                taskCode))
        {
            var windowsImageRepairService =
                application.Services
                    .GetRequiredService<
                        IWindowsImageRepairService>();

            return await windowsImageRepairService
                .RunAsync();
        }

        throw new InvalidOperationException(
            "Die ausgewählte Aufgabe besitzt keine "
            + "freigegebene Wartungsausführung.");
    }

    private static MaintenanceToolResult
        NormalizeMaintenanceResult(
            MaintenanceToolResult result,
            DateTimeOffset fallbackStartedAt)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        var startedAt =
            result.StartedAt
            ?? fallbackStartedAt;

        var finishedAt =
            result.FinishedAt
            ?? DateTimeOffset.Now;

        if (finishedAt < startedAt)
        {
            finishedAt =
                startedAt;
        }

        return new MaintenanceToolResult
        {
            Status =
                result.Status,

            Summary =
                string.IsNullOrWhiteSpace(
                    result.Summary)
                    ? "Die Wartungsaktion lieferte keine "
                      + "eindeutige Zusammenfassung."
                    : result.Summary.Trim(),

            Details =
                result.Details
                ?? string.Empty,

            StandardOutput =
                result.StandardOutput
                ?? string.Empty,

            StandardError =
                result.StandardError
                ?? string.Empty,

            ExitCode =
                result.ExitCode,

            StartedAt =
                startedAt,

            FinishedAt =
                finishedAt
        };
    }

    private static CheckupTaskActionResult
        CreateMaintenanceActionResult(
            CheckupTask task,
            CheckupTaskActionDefinition definition,
            MaintenanceToolResult result)
    {
        return new CheckupTaskActionResult
        {
            ActionCode =
                definition.ActionCode,

            ActionTitle =
                definition.ActionTitle,

            TargetDescription =
                CheckupStandardTaskCatalog
                    .GetTargetDescription(
                        task.TaskCode),

            Status =
                DetermineMaintenanceActionStatus(
                    task.TaskCode,
                    result.Status),

            Summary =
                result.Summary,

            Details =
                BuildMaintenanceTechnicalDetails(
                    result),

            ExitCode =
                result.ExitCode,

            RestartRequired =
                result.Status
                == MaintenanceToolStatus.RestartRequired,

            RestartStatusWasConclusive =
                result.Status
                    is not MaintenanceToolStatus.Failed
                    and not MaintenanceToolStatus.ActionRequired,

            StartedAt =
                result.StartedAt,

            FinishedAt =
                result.FinishedAt
        };
    }

    private static CheckupTaskActionStatus
        DetermineMaintenanceActionStatus(
            string taskCode,
            MaintenanceToolStatus status)
    {
        if (status
            == MaintenanceToolStatus.Skipped)
        {
            return
                CheckupTaskActionStatus.Cancelled;
        }

        if (status
            is MaintenanceToolStatus.Successful
            or MaintenanceToolStatus.SuccessfulWithRepairs
            or MaintenanceToolStatus.RestartRequired)
        {
            return
                CheckupTaskActionStatus.Successful;
        }

        if (CheckupStandardTaskCatalog
                .IsSystemFileCheckTask(
                    taskCode)
            && status
                == MaintenanceToolStatus.ActionRequired)
        {
            return
                CheckupTaskActionStatus.Successful;
        }

        return
            CheckupTaskActionStatus.Failed;
    }

    private static void PersistMaintenanceResult(
        CheckupTask task,
        CheckupTaskList taskList,
        CheckupTaskActionResult actionResult,
        MaintenanceToolResult result)
    {
        var shouldCompleteTask =
            ShouldCompleteMaintenanceTask(
                task.TaskCode,
                result.Status);

        var followUpTask =
            ShouldCreateDismFollowUpTask(
                task.TaskCode,
                result.Status)
                ? CheckupStandardTaskCatalog
                    .CreateWindowsImageRepairTask(
                        DateTime.Now)
                : null;

        try
        {
            if (shouldCompleteTask)
            {
                taskList.ApplyTaskActionOutcome(
                    task,
                    actionResult,
                    CheckupTaskStatus.Completed,
                    BuildMaintenanceStatusReason(
                        task.TaskCode,
                        result),
                    followUpTask);

                return;
            }

            taskList.AddTaskActionResult(
                task,
                actionResult);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Das Wartungswerkzeug wurde bereits ausgeführt, "
                + "das technische Ergebnis konnte jedoch nicht "
                + "vollständig im Kundencheckup gespeichert werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Die Aktion darf nicht ungeprüft wiederholt "
                + "werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Technische Ursache: "
                + exception.Message,
                exception);
        }
    }

    private static bool ShouldCompleteMaintenanceTask(
        string taskCode,
        MaintenanceToolStatus status)
    {
        if (CheckupStandardTaskCatalog
            .IsSystemFileCheckTask(
                taskCode))
        {
            return status
                is MaintenanceToolStatus.Successful
                or MaintenanceToolStatus.SuccessfulWithRepairs
                or MaintenanceToolStatus.ActionRequired
                or MaintenanceToolStatus.RestartRequired;
        }

        if (CheckupStandardTaskCatalog
            .IsWindowsImageRepairTask(
                taskCode))
        {
            return status
                is MaintenanceToolStatus.Successful
                or MaintenanceToolStatus.SuccessfulWithRepairs
                or MaintenanceToolStatus.RestartRequired;
        }

        return false;
    }

    private static bool ShouldCreateDismFollowUpTask(
        string taskCode,
        MaintenanceToolStatus status)
    {
        return CheckupStandardTaskCatalog
                   .IsSystemFileCheckTask(
                       taskCode)
               && status
                   == MaintenanceToolStatus.ActionRequired;
    }

    private static string BuildMaintenanceStatusReason(
        string taskCode,
        MaintenanceToolResult result)
    {
        var finishedAt =
            result.FinishedAt
                ?.ToLocalTime()
                .ToString(
                    "dd.MM.yyyy HH:mm")
            ?? DateTime.Now.ToString(
                "dd.MM.yyyy HH:mm");

        if (ShouldCreateDismFollowUpTask(
                taskCode,
                result.Status))
        {
            return
                $"SFC-Prüfung vom {finishedAt} Uhr abgeschlossen: "
                + result.Summary
                + " Aufgrund des verbliebenen "
                + "Reparaturbedarfs wurde eine separate "
                + "DISM-Aufgabe angelegt.";
        }

        if (CheckupStandardTaskCatalog
            .IsWindowsImageRepairTask(
                taskCode))
        {
            return
                $"DISM-Ausführung vom {finishedAt} Uhr "
                + "abgeschlossen: "
                + result.Summary
                + " Anschließend soll SFC erneut ausgeführt "
                + "werden, um die Systemdateien nochmals "
                + "zu kontrollieren.";
        }

        return
            $"SFC-Prüfung vom {finishedAt} Uhr "
            + "abgeschlossen: "
            + result.Summary;
    }

    private static string BuildMaintenanceTechnicalDetails(
        MaintenanceToolResult result)
    {
        var builder =
            new StringBuilder();

        if (!string.IsNullOrWhiteSpace(
                result.Details))
        {
            builder.AppendLine(
                result.Details.Trim());
        }

        builder.AppendLine();
        builder.AppendLine(
            "Technische Ausführungsdaten:");

        builder.Append(
            "Status: ");

        builder.AppendLine(
            GetMaintenanceStatusText(
                result.Status));

        builder.Append(
            "Exitcode: ");

        builder.AppendLine(
            result.ExitCode?.ToString()
            ?? "Nicht verfügbar");

        builder.Append(
            "Dauer: ");

        builder.AppendLine(
            FormatDuration(
                result.Duration));

        if (!string.IsNullOrWhiteSpace(
                result.StandardOutput))
        {
            builder.AppendLine();
            builder.AppendLine(
                "Standardausgabe:");

            builder.AppendLine(
                result.StandardOutput.Trim());
        }

        if (!string.IsNullOrWhiteSpace(
                result.StandardError))
        {
            builder.AppendLine();
            builder.AppendLine(
                "Fehlerausgabe:");

            builder.AppendLine(
                result.StandardError.Trim());
        }

        return builder
            .ToString()
            .Trim();
    }

    private static string BuildMaintenanceConfirmationMessage(
        string taskCode,
        CheckupTaskActionDefinition definition)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            definition.Description);

        builder.AppendLine();
        builder.AppendLine(
            "Zielbereich:");

        builder.AppendLine(
            CheckupStandardTaskCatalog
                .GetTargetDescription(
                    taskCode));

        builder.AppendLine();
        builder.AppendLine(
            "Zu beachten:");

        builder.AppendLine(
            definition.RiskDescription);

        builder.AppendLine();
        builder.AppendLine(
            definition.AdministratorRequirementText);

        builder.AppendLine(
            definition.RestartPossibilityText);

        builder.AppendLine();

        if (CheckupStandardTaskCatalog
            .IsSystemFileCheckTask(
                taskCode))
        {
            builder.AppendLine(
                "Wird nicht vollständig reparierbarer "
                + "Systemdateischaden erkannt, legt das "
                + "Programm automatisch eine separate "
                + "DISM-Aufgabe an.");
        }
        else
        {
            builder.AppendLine(
                "Nach einer erfolgreichen DISM-Reparatur "
                + "soll die SFC-Aufgabe nochmals ausgeführt "
                + "werden.");
        }

        builder.AppendLine();
        builder.Append(
            "Aktion jetzt starten?");

        return builder.ToString();
    }

    private static string GetMaintenanceConfirmationTitle(
        string taskCode)
    {
        return CheckupStandardTaskCatalog
            .IsSystemFileCheckTask(
                taskCode)
                ? "Systemdateiprüfung starten"
                : "Windows-Abbildreparatur starten";
    }

    private static string GetMaintenanceRunningText(
        string taskCode)
    {
        return CheckupStandardTaskCatalog
            .IsSystemFileCheckTask(
                taskCode)
                ? "SFC-Prüfung läuft …"
                : "DISM-Reparatur läuft …";
    }

    private static string GetMaintenanceStatusText(
        MaintenanceToolStatus status)
    {
        return status switch
        {
            MaintenanceToolStatus.Successful =>
                "Erfolgreich",

            MaintenanceToolStatus.SuccessfulWithRepairs =>
                "Erfolgreich mit Reparaturen",

            MaintenanceToolStatus.ActionRequired =>
                "Weiterer Handlungsbedarf",

            MaintenanceToolStatus.RestartRequired =>
                "Neustart erforderlich",

            MaintenanceToolStatus.Skipped =>
                "Übersprungen",

            MaintenanceToolStatus.Failed =>
                "Fehlgeschlagen",

            MaintenanceToolStatus.Running =>
                "Wird ausgeführt",

            _ =>
                "Nicht ausgeführt"
        };
    }

    private static string FormatDuration(
        TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1)
        {
            return
                $"{(int)duration.TotalMinutes} Min. "
                + $"{duration.Seconds} Sek.";
        }

        return
            $"{Math.Max(
                1,
                (int)Math.Ceiling(
                    duration.TotalSeconds))} Sek.";
    }

    private void ShowMaintenanceResult(
        CheckupTask task,
        MaintenanceToolResult result)
    {
        var message =
            new StringBuilder();

        message.AppendLine(
            result.Summary);

        if (!string.IsNullOrWhiteSpace(
                result.Details))
        {
            message.AppendLine();
            message.AppendLine(
                result.Details);
        }

        if (ShouldCreateDismFollowUpTask(
                task.TaskCode,
                result.Status))
        {
            message.AppendLine();
            message.AppendLine(
                "Eine separate DISM-Aufgabe wurde der "
                + "Aufgabenliste hinzugefügt.");
        }
        else if (CheckupStandardTaskCatalog
                     .IsWindowsImageRepairTask(
                         task.TaskCode)
                 && result.Status
                     is MaintenanceToolStatus.Successful
                     or MaintenanceToolStatus
                         .SuccessfulWithRepairs)
        {
            message.AppendLine();
            message.AppendLine(
                "Führen Sie anschließend die bereits "
                + "vorhandene SFC-Aufgabe erneut aus.");
        }

        if (result.Status
            == MaintenanceToolStatus.RestartRequired)
        {
            message.AppendLine();
            message.AppendLine(
                "Vor weiteren Reparaturprüfungen muss "
                + "Windows neu gestartet werden.");
        }

        var resultWasSuccessful =
            result.Status
                is MaintenanceToolStatus.Successful
                or MaintenanceToolStatus.SuccessfulWithRepairs
                or MaintenanceToolStatus.ActionRequired
                or MaintenanceToolStatus.RestartRequired;

        var dialog =
            new MessageDialog(
                CheckupStandardTaskCatalog
                    .IsSystemFileCheckTask(
                        task.TaskCode)
                    ? "Systemdateiprüfung abgeschlossen"
                    : "Windows-Abbildreparatur abgeschlossen",
                message
                    .ToString()
                    .Trim(),
                resultWasSuccessful
                    ? MessageDialogKind.Information
                    : MessageDialogKind.Error,
                resultWasSuccessful
                    ? "Technisches Ergebnis und Aufgabenstatus "
                      + "wurden im Checkup gespeichert."
                    : "Das technische Fehlerergebnis wurde "
                      + "im Checkup dokumentiert.")
            {
                Owner =
                    Window.GetWindow(
                        this)
            };

        dialog.ShowDialog();
    }

    private static CheckupTask? FindTaskContext(
        DependencyObject source)
    {
        DependencyObject? current =
            source;

        while (current is not null)
        {
            if (current is FrameworkElement element
                && element.DataContext
                    is CheckupTask task)
            {
                return task;
            }

            current =
                VisualTreeHelper.GetParent(
                    current);
        }

        return null;
    }

    private async void CompletionCheckButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_isCompletionCheckRunning)
        {
            return;
        }

        if (CheckupSession is null
            || DataContext
                is not CheckupTaskList taskList)
        {
            ShowCompletionCheckError(
                "Der zugehörige Checkup-Kontext ist "
                + "nicht vollständig verfügbar.");

            return;
        }

        if (!ReferenceEquals(
                CheckupSession.TaskList,
                taskList))
        {
            ShowCompletionCheckError(
                "Die angezeigte Aufgabenliste konnte "
                + "nicht eindeutig dem Checkup "
                + "zugeordnet werden.");

            return;
        }

        var application =
            Application.Current as App;

        if (application is null)
        {
            ShowCompletionCheckError(
                "Der zentrale Anwendungsdienst ist "
                + "nicht verfügbar.");

            return;
        }

        var dialogService =
            application.Services
                .GetRequiredService<
                    IDialogService>();

        var confirmed =
            dialogService.Confirm(
                "Abschlusskontrolle starten",
                "Es wird jetzt ein neuer, vollständig "
                + "lesender Systemscan durchgeführt."
                + Environment.NewLine
                + Environment.NewLine
                + "Der ursprüngliche Checkup wird nicht "
                + "ersetzt. Automatisch verändert werden "
                + "ausschließlich die Status der Aufgaben, "
                + "für die bereits eine erfolgreiche "
                + "technische Aktion dokumentiert ist."
                + Environment.NewLine
                + Environment.NewLine
                + "Abgeschlossene Befunde werden als "
                + "erledigt markiert. Weiterhin vorhandene "
                + "Befunde bleiben offen."
                + Environment.NewLine
                + Environment.NewLine
                + "Abschlusskontrolle jetzt starten?");

        if (!confirmed)
        {
            return;
        }

        var sourceCheckup =
            CheckupSession;

        _isCompletionCheckRunning =
            true;

        if (_completionCheckButton is not null)
        {
            _completionCheckButton.IsEnabled =
                false;
        }

        try
        {
            var completionCheckService =
                ActivatorUtilities.CreateInstance<
                    CheckupCompletionCheckService>(
                    application.Services);

            var completionCheckResult =
                await Task.Run(
                    () =>
                        completionCheckService.Run(
                            sourceCheckup));

            if (!ReferenceEquals(
                    CheckupSession,
                    sourceCheckup)
                || !ReferenceEquals(
                    DataContext,
                    taskList))
            {
                throw new InvalidOperationException(
                    "Der angezeigte Checkup wurde während "
                    + "des Kontrollscans gewechselt. Das "
                    + "Ergebnis wurde nicht übernommen.");
            }

            taskList.ApplyCompletionCheck(
                completionCheckResult);

            ShowCompletionCheckResult(
                taskList
                    .LastCompletionCheckSummary);
        }
        catch (Exception exception)
        {
            ShowCompletionCheckError(
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Die Abschlusskontrolle konnte nicht "
                      + "sicher beendet werden."
                    : exception.Message);
        }
        finally
        {
            _isCompletionCheckRunning =
                false;

            if (_completionCheckButton is not null)
            {
                _completionCheckButton.IsEnabled =
                    true;
            }
        }
    }

    private void ShowPreparationError(
        string message)
    {
        var dialog =
            new MessageDialog(
                "Aktionsvorbereitung nicht möglich",
                message)
            {
                Owner =
                    Window.GetWindow(
                        this)
            };

        dialog.ShowDialog();
    }

    private void ShowMaintenanceError(
        string message)
    {
        var dialog =
            new MessageDialog(
                "Wartungsaktion nicht abgeschlossen",
                message)
            {
                Owner =
                    Window.GetWindow(
                        this)
            };

        dialog.ShowDialog();
    }

    private void ShowCompletionCheckResult(
        string message)
    {
        var dialog =
            new MessageDialog(
                "Abschlusskontrolle abgeschlossen",
                string.IsNullOrWhiteSpace(
                    message)
                    ? "Der Kontrollscan wurde abgeschlossen."
                    : message,
                MessageDialogKind.Information,
                "Kontrollergebnis und Aufgabenstatus "
                + "wurden gespeichert.")
            {
                Owner =
                    Window.GetWindow(
                        this)
            };

        dialog.ShowDialog();
    }

    private void ShowCompletionCheckError(
        string message)
    {
        var dialog =
            new MessageDialog(
                "Abschlusskontrolle nicht möglich",
                message)
            {
                Owner =
                    Window.GetWindow(
                        this)
            };

        dialog.ShowDialog();
    }
}
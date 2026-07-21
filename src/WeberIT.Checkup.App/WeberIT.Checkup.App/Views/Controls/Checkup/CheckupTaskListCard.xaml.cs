using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
            new PropertyMetadata(null));

    private Button? _completionCheckButton;

    private bool _isCompletionCheckRunning;

    public CheckupTaskListCard()
    {
        InitializeComponent();

        AddCompletionCheckPanel();
        AddStatusEditor();
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

    private void ActionDetailsButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button
            || button.DataContext
                is not CheckupTaskActionDefinition definition)
        {
            return;
        }

        var dialog =
            new TaskActionDetailsDialog(
                definition)
            {
                Owner =
                    Window.GetWindow(this)
            };

        dialog.ShowDialog();
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
                    Window.GetWindow(this)
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
                        Window.GetWindow(this)
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
                    Window.GetWindow(this)
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
                    Window.GetWindow(this)
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
                    Window.GetWindow(this)
            };

        dialog.ShowDialog();
    }
}
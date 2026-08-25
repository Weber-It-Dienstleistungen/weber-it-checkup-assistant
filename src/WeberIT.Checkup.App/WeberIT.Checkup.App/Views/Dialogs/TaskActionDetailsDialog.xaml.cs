using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Views.Dialogs;

public partial class TaskActionDetailsDialog : Window
{
    private readonly CheckupTaskActionDefinition
        _definition;

    private readonly IGuidedTaskActionLauncher
        _guidedTaskActionLauncher;

    private readonly bool
        _guidedLaunchAvailable;

    private readonly bool
        _allowStatusDecision;

    public TaskActionDetailsDialog(
        CheckupTaskActionDefinition definition)
        : this(
            definition,
            false)
    {
    }

    public TaskActionDetailsDialog(
        CheckupTaskActionDefinition definition,
        bool allowStatusDecision)
    {
        ArgumentNullException.ThrowIfNull(
            definition);

        _definition =
            definition;

        _allowStatusDecision =
            allowStatusDecision;

        _guidedTaskActionLauncher =
            ResolveGuidedTaskActionLauncher();

        _guidedLaunchAvailable =
            definition.IsGuided
            && _guidedTaskActionLauncher.CanLaunch(
                definition.ActionCode);

        InitializeComponent();

        DataContext =
            definition;

        ApplyGuidedSupportState();
        ApplyResolutionState();
    }

    public CheckupTaskStatus? SelectedTaskStatus
    {
        get;
        private set;
    }

    public string StatusReason
    {
        get;
        private set;
    } = string.Empty;

    public string TechnicianNote
    {
        get;
        private set;
    } = string.Empty;

    private static IGuidedTaskActionLauncher
        ResolveGuidedTaskActionLauncher()
    {
        var application =
            Application.Current as App;

        if (application is null)
        {
            throw new InvalidOperationException(
                "Der zentrale Anwendungsdienst ist für die "
                + "geführte Prüfung nicht verfügbar.");
        }

        return application.Services
            .GetRequiredService<
                IGuidedTaskActionLauncher>();
    }

    private void ApplyGuidedSupportState()
    {
        if (!_guidedLaunchAvailable)
        {
            PreparationOnlyNotice.Visibility =
                Visibility.Visible;

            GuidedSupportNotice.Visibility =
                Visibility.Collapsed;

            OpenGuidedViewButton.Visibility =
                Visibility.Collapsed;

            FooterStatusTextBlock.Text =
                _allowStatusDecision
                    ? "Manuelle Bearbeitung – "
                      + "noch kein Ergebnis dokumentiert"
                    : "Noch keine Ausführung oder Bestätigung";

            return;
        }

        PreparationOnlyNotice.Visibility =
            Visibility.Collapsed;

        GuidedSupportNotice.Visibility =
            Visibility.Visible;

        OpenGuidedViewButton.Visibility =
            Visibility.Visible;

        FooterStatusTextBlock.Text =
            "Noch keine Prüfansicht geöffnet";

        var targetDescription =
            _guidedTaskActionLauncher
                .GetTargetDescription(
                    _definition.ActionCode);

        GuidedTargetDescriptionTextBlock.Text =
            "Vorgesehene Prüfansicht: "
            + targetDescription;

        OpenGuidedViewButton.ToolTip =
            targetDescription;
    }

    private void ApplyResolutionState()
    {
        if (!_allowStatusDecision)
        {
            ResolutionPanel.Visibility =
                Visibility.Collapsed;

            ApplyResolutionButton.Visibility =
                Visibility.Collapsed;

            return;
        }

        ResolutionPanel.Visibility =
            Visibility.Visible;

        ApplyResolutionButton.Visibility =
            Visibility.Visible;

        if (!_guidedLaunchAvailable)
        {
            PreparationOnlyNoticeTextBlock.Text =
                "Für diesen Punkt erfolgt keine automatische "
                + "Änderung durch das Checkup-Tool. Führen Sie "
                + "die erforderliche Prüfung oder Maßnahme "
                + "manuell durch und dokumentieren Sie das "
                + "Ergebnis anschließend unten.";
        }
        else
        {
            GuidedSupportDescriptionTextBlock.Text =
                "Die geführte Unterstützung öffnet "
                + "ausschließlich die passende "
                + "Windows-Prüfansicht. Dort erfolgt keine "
                + "automatische Änderung. Prüfen oder bearbeiten "
                + "Sie den Punkt dort und dokumentieren Sie das "
                + "Ergebnis anschließend unten.";
        }
    }

    private void OpenGuidedViewButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (!_guidedLaunchAvailable)
        {
            return;
        }

        try
        {
            _guidedTaskActionLauncher.Launch(
                _definition.ActionCode);

            FooterStatusTextBlock.Text =
                _allowStatusDecision
                    ? "Prüfansicht geöffnet – "
                      + "Ergebnis anschließend dokumentieren"
                    : "Prüfansicht geöffnet – "
                      + "keine Behebung dokumentiert";
        }
        catch (Exception exception)
        {
            FooterStatusTextBlock.Text =
                "Prüfansicht konnte nicht geöffnet werden";

            var dialog =
                new MessageDialog(
                    "Geführte Prüfung nicht geöffnet",
                    string.IsNullOrWhiteSpace(
                        exception.Message)
                        ? "Die zugehörige Windows-Prüfansicht "
                          + "konnte nicht geöffnet werden."
                        : exception.Message)
                {
                    Owner =
                        this
                };

            dialog.ShowDialog();
        }
    }

    private void ApplyResolutionButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (!_allowStatusDecision)
        {
            return;
        }

        ValidationTextBlock.Text =
            string.Empty;

        if (!TryGetSelectedResolutionStatus(
                out var selectedStatus))
        {
            ValidationTextBlock.Text =
                "Bitte wählen Sie aus, ob die Aufgabe "
                + "erledigt, nicht durchführbar oder "
                + "übersprungen wurde.";

            return;
        }

        var statusReason =
            StatusReasonTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(
                statusReason))
        {
            ValidationTextBlock.Text =
                "Für die Ergebnisdokumentation ist eine "
                + "Begründung erforderlich.";

            StatusReasonTextBox.Focus();

            return;
        }

        SelectedTaskStatus =
            selectedStatus;

        StatusReason =
            statusReason;

        TechnicianNote =
            TechnicianNoteTextBox.Text.Trim();

        DialogResult =
            true;
    }

    private bool TryGetSelectedResolutionStatus(
        out CheckupTaskStatus status)
    {
        foreach (var child
                 in ResolutionStatusOptionsPanel.Children)
        {
            if (child is RadioButton
                {
                    IsChecked: true,
                    Tag: CheckupTaskStatus selectedStatus
                })
            {
                status =
                    selectedStatus;

                return true;
            }
        }

        status =
            CheckupTaskStatus.Open;

        return false;
    }

    private void CloseButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult =
            false;
    }
}
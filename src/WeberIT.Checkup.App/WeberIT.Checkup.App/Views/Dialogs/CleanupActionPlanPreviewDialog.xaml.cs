using System.Windows;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Tasks;

namespace WeberIT.Checkup.App.Views.Dialogs;

public partial class CleanupActionPlanPreviewDialog : Window
{
    private const string SupportedActionCode =
        "action.cleanup.selected-safe-categories";

    private readonly CheckupTaskActionPlan
        _plan;

    private readonly bool
        _executionIsAvailable;

    public CleanupActionPlanPreviewDialog(
        CheckupTaskActionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(
            plan);

        plan.Validate();

        if (!string.Equals(
                plan.ActionCode,
                SupportedActionCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Der dargestellte Aktionsplan ist kein "
                + "freigegebener Bereinigungsplan.");
        }

        if (plan.HasCommands)
        {
            throw new InvalidOperationException(
                "Ein Bereinigungsplan darf keine "
                + "externen Befehle enthalten.");
        }

        if (!plan.HasCleanupCategories)
        {
            throw new InvalidOperationException(
                "Der Bereinigungsplan enthält keine "
                + "validierte Bereinigungskategorie.");
        }

        _plan =
            plan;

        _executionIsAvailable =
            CleanupActionPlanSnapshot.CanExecute(
                plan);

        InitializeComponent();

        DataContext =
            plan;

        ApplyExecutionAvailability();
    }

    public CheckupTaskActionPlan?
        ConfirmedPlan
    {
        get;
        private set;
    }

    private void ApplyExecutionAvailability()
    {
        if (_executionIsAvailable)
        {
            ExecutionAvailableNotice.Visibility =
                Visibility.Visible;

            PurePreviewNotice.Visibility =
                Visibility.Collapsed;

            ExecutionConfirmationPanel.Visibility =
                Visibility.Visible;

            StartCleanupButton.Visibility =
                Visibility.Visible;

            StartCleanupButton.IsEnabled =
                false;

            FooterStatusTextBlock.Text =
                "Ausführung erst nach ausdrücklicher Bestätigung möglich";

            CloseButton.Content =
                "Zurück";

            return;
        }

        ExecutionAvailableNotice.Visibility =
            Visibility.Collapsed;

        PurePreviewNotice.Visibility =
            Visibility.Visible;

        ExecutionConfirmationPanel.Visibility =
            Visibility.Collapsed;

        StartCleanupButton.Visibility =
            Visibility.Collapsed;

        StartCleanupButton.IsEnabled =
            false;

        FooterStatusTextBlock.Text =
            "Keine Löschung und keine Prozessausführung möglich";

        CloseButton.Content =
            "Planvorschau schließen";
    }

    private void ExecutionConfirmationCheckBox_OnChanged(
        object sender,
        RoutedEventArgs e)
    {
        StartCleanupButton.IsEnabled =
            _executionIsAvailable
            && ExecutionConfirmationCheckBox.IsChecked
                == true;
    }

    private void StartCleanupButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (!_executionIsAvailable
            || ExecutionConfirmationCheckBox.IsChecked
                != true)
        {
            return;
        }

        try
        {
            ConfirmedPlan =
                CleanupActionPlanSnapshot
                    .CreateExecutableCopy(
                        _plan);

            DialogResult =
                true;
        }
        catch (Exception exception)
        {
            ConfirmedPlan =
                null;

            var dialog =
                new MessageDialog(
                    "Bereinigungsplan nicht bestätigt",
                    string.IsNullOrWhiteSpace(
                        exception.Message)
                        ? "Der geprüfte Bereinigungsplan "
                          + "konnte nicht sicher für die "
                          + "Ausführung übernommen werden."
                        : exception.Message)
                {
                    Owner =
                        this
                };

            dialog.ShowDialog();
        }
    }

    private void CloseButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        ConfirmedPlan =
            null;

        Close();
    }
}
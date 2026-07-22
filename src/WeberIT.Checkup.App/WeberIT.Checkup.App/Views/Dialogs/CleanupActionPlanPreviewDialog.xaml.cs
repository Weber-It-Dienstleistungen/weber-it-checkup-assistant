using System.Windows;
using System.Windows.Controls;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Cleanup;
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

    private readonly bool
        _isBrowserCachePlan;

    private readonly bool
        _isWindowsTemporaryFilesPlan;

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

        _isBrowserCachePlan =
            plan.CleanupCategories.Count == 1
            && plan.CleanupCategories[0].Category
                == CleanupCategoryType.BrowserCache;

        _isWindowsTemporaryFilesPlan =
            plan.CleanupCategories.Count == 1
            && plan.CleanupCategories[0].Category
                == CleanupCategoryType.WindowsTemporaryFiles;

        InitializeComponent();

        DataContext =
            plan;

        ApplyExecutionAvailability();
        AddCategorySpecificWarning();
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
                _isBrowserCachePlan
                    ? "Ausführung nur bei vollständig "
                      + "beendeten Browsern möglich"
                    : _isWindowsTemporaryFilesPlan
                        ? "Separate Administratorbestätigung "
                          + "beim Start erforderlich"
                        : "Ausführung erst nach ausdrücklicher "
                          + "Bestätigung möglich";

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

    private void AddCategorySpecificWarning()
    {
        if (_isBrowserCachePlan)
        {
            AddExecutionWarning(
                "Browser vor dem Start vollständig schließen",
                "Microsoft Edge, Google Chrome und Mozilla "
                + "Firefox müssen einschließlich möglicher "
                + "Hintergrundprozesse beendet sein. Das "
                + "Checkup-Tool beendet keine Browser und "
                + "erzwingt keinen Prozessabbruch. Laufende "
                + "Browser blockieren die Ausführung vor "
                + "dem ersten Dateizugriff.");

            return;
        }

        if (_isWindowsTemporaryFilesPlan)
        {
            AddExecutionWarning(
                "Administratorbestätigung erforderlich",
                "Windows fordert beim Start der Bereinigung "
                + "eine separate UAC-Bestätigung an. "
                + "Verarbeitet werden ausschließlich Inhalte "
                + "des lokalen Windows-Temp-Ordners. Der "
                + "Stammordner bleibt bestehen, Verknüpfungen "
                + "werden nicht verfolgt und gesperrte Dateien "
                + "werden nicht gewaltsam entfernt.");
        }
    }

    private void AddExecutionWarning(
        string title,
        string text)
    {
        if (ExecutionAvailableNotice.Child
            is not StackPanel noticePanel)
        {
            return;
        }

        var warningTitle =
            new TextBlock
            {
                Margin =
                    new Thickness(
                        0,
                        12,
                        0,
                        0),

                FontWeight =
                    FontWeights.SemiBold,

                Text =
                    title,

                TextWrapping =
                    TextWrapping.Wrap
            };

        warningTitle.SetResourceReference(
            TextBlock.ForegroundProperty,
            "WarningBrush");

        noticePanel.Children.Add(
            warningTitle);

        var warningText =
            new TextBlock
            {
                Margin =
                    new Thickness(
                        0,
                        6,
                        0,
                        0),

                Text =
                    text,

                TextWrapping =
                    TextWrapping.Wrap
            };

        warningText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "TextSecondaryBrush");

        noticePanel.Children.Add(
            warningText);
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

        if (_isBrowserCachePlan
            && !ConfirmBrowserCacheReadiness())
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

    private bool ConfirmBrowserCacheReadiness()
    {
        var processState =
            BrowserCacheRuntimeGuard.Evaluate();

        if (processState.CanProceed)
        {
            FooterStatusTextBlock.Text =
                "Browserprüfung erfolgreich – "
                + "Ausführung wird vorbereitet";

            return true;
        }

        FooterStatusTextBlock.Text =
            "Browsercache-Ausführung blockiert";

        var dialog =
            new MessageDialog(
                "Browsercache noch gesperrt",
                string.IsNullOrWhiteSpace(
                    processState.BlockingMessage)
                    ? "Die Browsercache-Bereinigung kann "
                      + "derzeit nicht sicher gestartet werden."
                    : processState.BlockingMessage)
            {
                Owner =
                    this
            };

        dialog.ShowDialog();

        return false;
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

using System.Windows;
using System.Windows.Controls;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Cleanup;
using WeberIT.Checkup.App.Services.Tasks;

namespace WeberIT.Checkup.App.Views.Dialogs;

/// <summary>
/// Sprint 55.14.33c
///
/// Vorschau und Bestätigung kontrollierter
/// Bereinigungsaktionen.
///
/// Windows-Temp:
/// - Beaufsichtigten Debug-Echt-Test anzeigen.
/// - Tatsächliche Löschwirkung klar benennen.
/// - Keine zusätzliche UAC-Abfrage behaupten.
/// - Scanwerte als Momentaufnahme kennzeichnen.
/// - Ausdrückliche Bestätigung verlangen.
///
/// Keine Änderung an der Lösch-Engine.
/// </summary>
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

    // =============================================
    // AUSFÜHRBARKEIT ANZEIGEN
    // =============================================

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

            // =====================================
            // WINDOWS-TEMP: BEAUFSICHTIGTER TEST
            // =====================================

            if (_isWindowsTemporaryFilesPlan)
            {
                ApplyWindowsTempTrialNotice();

                FooterStatusTextBlock.Text =
                    "Debug-Echt-Test: Administratorrechte "
                    + "bereits beim Programmstart erforderlich";
            }
            else if (_isBrowserCachePlan)
            {
                FooterStatusTextBlock.Text =
                    "Ausführung nur bei vollständig "
                    + "beendeten Browsern möglich";
            }
            else
            {
                FooterStatusTextBlock.Text =
                    "Ausführung erst nach ausdrücklicher "
                    + "Bestätigung möglich";
            }

            CloseButton.Content =
                "Zurück";

            return;
        }

        // =====================================
        // REINE PLANVORSCHAU
        // =====================================

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

    // =============================================
    // WINDOWS-TEMP-TEST HERVORHEBEN
    // =============================================

    private void ApplyWindowsTempTrialNotice()
    {
        /*
         * Die bestehende Hinweisfläche der
         * XAML-Datei wird für den beaufsichtigten
         * Windows-Temp-Test präzisiert.
         *
         * Andere Bereinigungskategorien
         * behalten ihre bisherigen Texte.
         */
        if (ExecutionAvailableNotice.Child
            is not StackPanel noticePanel
            || noticePanel.Children.Count < 2
            || noticePanel.Children[0]
                is not TextBlock noticeTitle
            || noticePanel.Children[1]
                is not TextBlock noticeDescription)
        {
            return;
        }

        noticeTitle.Text =
            "Beaufsichtigter Windows-Temp-Echt-Test";

        noticeDescription.Text =
            "ACHTUNG: Dies ist keine Simulation. "
            + "Nach ausdrücklicher Bestätigung "
            + "führt der Checkup Assistent echte "
            + "Löschoperationen im lokalen "
            + "Windows-Temp-Verzeichnis aus. "
            + "Die allgemeine Produktivfreigabe "
            + "bleibt geschlossen. "
            + "Diese Ausführung ist ausschließlich "
            + "in der aktuell zugelassenen "
            + "Debug-Sitzung möglich.";
    }

    // =============================================
    // KATEGORIESPEZIFISCHE WARNUNGEN
    // =============================================

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

        if (_isWindowsTemporaryFilesPlan
            && _executionIsAvailable)
        {
            AddExecutionWarning(
                "Echte Löschung im Windows-Temp-Verzeichnis",
                "Die Scanwerte sind eine Momentaufnahme "
                + "und keine feste Löschliste. "
                + "Die Bereinigungs-Engine prüft die "
                + "vorhandenen Einträge zum Zeitpunkt "
                + "der Ausführung erneut und versucht, "
                + "zulässige Dateien und leere "
                + "Unterordner zu entfernen. "
                + "Der Windows-Temp-Stammordner "
                + "bleibt bestehen. "
                + "Geschützte, gesperrte und "
                + "sicherheitsbedingt ausgeschlossene "
                + "Einträge werden nicht erzwungen. "
                + "Erfolgreich gelöschte Dateien "
                + "werden nicht automatisch "
                + "wiederhergestellt.");

            AddExecutionWarning(
                "Administratorrechte und Testbedingungen",
                "Die Anwendung fordert Administratorrechte "
                + "gemäß ihrem Manifest bereits beim "
                + "Programmstart an. "
                + "Beim Start dieser Bereinigung erfolgt "
                + "keine zusätzliche UAC-Abfrage. "
                + "Der Test sollte nur bei inaktiven "
                + "Windows-Updates und Installationen "
                + "sowie mit geeigneter Systemsicherung "
                + "ausgeführt werden.");
        }
    }

    // =============================================
    // WARNHINWEIS DARSTELLEN
    // =============================================

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

    // =============================================
    // AUSFÜHRUNG BESTÄTIGEN
    // =============================================

    private void ExecutionConfirmationCheckBox_OnChanged(
        object sender,
        RoutedEventArgs e)
    {
        StartCleanupButton.IsEnabled =
            _executionIsAvailable
            && ExecutionConfirmationCheckBox.IsChecked
                == true;
    }

    // =============================================
    // BEREINIGUNG ANFORDERN
    // =============================================

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
            /*
             * Der bestätigte Plan wird nochmals
             * durch die zentrale Planprüfung
             * validiert.
             *
             * Auch der Executor überprüft
             * anschließend unabhängig seine
             * eigene technische Freigabe.
             */
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

    // =============================================
    // BROWSERCACHE-BEREITSCHAFT
    // =============================================

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

    // =============================================
    // DIALOG SCHLIESSEN
    // =============================================

    private void CloseButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        ConfirmedPlan =
            null;

        Close();
    }
}

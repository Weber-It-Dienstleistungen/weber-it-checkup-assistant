
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;
using WeberIT.Checkup.App.ViewModels;
using WeberIT.Checkup.App.Views.Dialogs;

namespace WeberIT.Checkup.App.Views.Controls.Checkup;

public partial class ProgramUpdatesCard : UserControl
{
    private const string ProgramUpdateTaskCode =
        "task.program-updates.available";

    private Button? _exceptionButton;

    private TextBlock? _exceptionStatusText;

    public ProgramUpdatesCard()
    {
        InitializeComponent();

        AddProgramUpdateExceptionPanel();

        Loaded +=
            ProgramUpdatesCard_OnLoaded;

        DataContextChanged +=
            ProgramUpdatesCard_OnDataContextChanged;
    }

    // =====================================================
    // TECHNIKER-AUSNAHMEN: OBERFLÄCHE
    // =====================================================

    private void AddProgramUpdateExceptionPanel()
    {
        if (Content
                is not CollapsibleCheckupSection section
            || section.SectionContent
                is not StackPanel contentPanel)
        {
            return;
        }

        var exceptionPanel = new StackPanel
        {
            Margin = new Thickness(0, 20, 0, 0)
        };

        var title = new TextBlock
        {
            Text = "Technikerentscheidungen",

            FontSize = 16,

            FontWeight = FontWeights.SemiBold,

            TextWrapping = TextWrapping.Wrap
        };

        title.SetResourceReference(
            TextBlock.ForegroundProperty,
            "TextPrimaryBrush");

        exceptionPanel.Children.Add(title);

        var description = new TextBlock
        {
            Margin = new Thickness(0, 8, 0, 0),

            Text =
                "Einzelne Programmupdates können "
                + "aus fachlichen Gründen bewusst "
                + "zurückgestellt werden. "
                + "Die Entscheidung wird mit "
                + "Begründung im aktuellen "
                + "Kundencheckup dokumentiert.",

            TextWrapping = TextWrapping.Wrap
        };

        description.SetResourceReference(
            TextBlock.ForegroundProperty,
            "TextSecondaryBrush");

        exceptionPanel.Children.Add(description);

        _exceptionButton = new Button
        {
            Content = "Ausnahmen dokumentieren",

            Margin = new Thickness(0, 14, 0, 0),

            Padding = new Thickness(18, 10, 18, 10),

            MinHeight = 40,

            HorizontalAlignment =
                HorizontalAlignment.Left,

            Visibility = Visibility.Collapsed
        };

        _exceptionButton.SetResourceReference(
            FrameworkElement.StyleProperty,
            "AccentButtonStyle");

        _exceptionButton.Click +=
            ProgramUpdateExceptionsButton_OnClick;

        exceptionPanel.Children.Add(
            _exceptionButton);

        _exceptionStatusText = new TextBlock
        {
            Margin = new Thickness(0, 10, 0, 0),

            FontSize = 11,

            TextWrapping = TextWrapping.Wrap,

            Visibility = Visibility.Collapsed
        };

        _exceptionStatusText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "TextSecondaryBrush");

        exceptionPanel.Children.Add(
            _exceptionStatusText);

        contentPanel.Children.Add(
            exceptionPanel);
    }

    private void ProgramUpdatesCard_OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        UpdateExceptionControls();
    }

    private void ProgramUpdatesCard_OnDataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        UpdateExceptionControls();
    }

    private void UpdateExceptionControls()
    {
        if (_exceptionButton is null
            || _exceptionStatusText is null)
        {
            return;
        }

        if (!TryResolveCustomerContext(
                out var viewModel,
                out var session,
                out var visit)
            || DataContext
                is not ProgramUpdateInformation information
            || viewModel.IsScanRunning
            || !visit.IsInProgress
            || visit.IsCompletionPrepared
            || information.IsWingetAvailable != true
            || !information.IsAnalysisPerformed
            || !information.IsAnalysisSuccessful
            || information.AvailableUpdateCount <= 0
            || information.AvailableUpdates.Count == 0)
        {
            _exceptionButton.Visibility =
                Visibility.Collapsed;

            _exceptionStatusText.Visibility =
                Visibility.Collapsed;

            return;
        }

        var documentedCount =
            visit.ProgramUpdateExceptions.Count(
                approval =>
                    approval.CheckupVisitId == visit.Id);

        _exceptionButton.Visibility =
            Visibility.Visible;

        _exceptionStatusText.Visibility =
            Visibility.Visible;

        _exceptionStatusText.Text =
            documentedCount == 0
                ? "Noch keine Ausnahmen dokumentiert. "
                  + "Der Systemscore berücksichtigt "
                  + "weiterhin alle erkannten "
                  + "Programmaktualisierungen."
                : documentedCount == 1
                    ? "Eine Programmupdate-Ausnahme "
                      + "ist in diesem Kundencheckup "
                      + "dokumentiert. Die Auswirkung "
                      + "auf den Systemscore ist "
                      + "noch nicht aktiviert."
                    : $"{documentedCount} "
                      + "Programmupdate-Ausnahmen "
                      + "sind in diesem Kundencheckup "
                      + "dokumentiert. Die Auswirkung "
                      + "auf den Systemscore ist "
                      + "noch nicht aktiviert.";
    }

    // =====================================================
    // TECHNIKER-AUSNAHMEN: KONTEXT
    // =====================================================

    private bool TryResolveCustomerContext(
        out CustomerDevicesViewModel viewModel,
        out CheckupSession session,
        out CustomerCheckupVisit visit)
    {
        viewModel = null!;
        session = null!;
        visit = null!;

        /*
         * Die WinGet-Karte wird sowohl bei einem
         * kundenunabhängigen Diagnosecheckup als
         * auch im Fenster eines Kundengeräts benutzt.
         *
         * Ausnahmen dürfen ausschließlich einem
         * gespeicherten Kundencheckup zugeordnet
         * werden.
         */
        if (Window.GetWindow(this)?.DataContext
                is not CustomerDevicesViewModel
                    customerDevicesViewModel)
        {
            return false;
        }

        var customer =
            customerDevicesViewModel.SelectedCustomer;

        var device =
            customerDevicesViewModel.SelectedDevice;

        if (customer is null
            || device is null
            || !customer.Devices.Any(
                storedDevice =>
                    ReferenceEquals(
                        storedDevice,
                        device)))
        {
            return false;
        }

        var deviceSession =
            device.CheckupSession;

        if (DataContext
                is not ProgramUpdateInformation
                    information
            || !ReferenceEquals(
                deviceSession.ProgramUpdateInformation,
                information))
        {
            return false;
        }

        var currentVisit =
            deviceSession.CurrentCustomerCheckupVisit;

        if (currentVisit is null
            || !deviceSession.CustomerCheckupVisits.Any(
                storedVisit =>
                    ReferenceEquals(
                        storedVisit,
                        currentVisit)))
        {
            return false;
        }

        viewModel =
            customerDevicesViewModel;

        session =
            deviceSession;

        visit =
            currentVisit;

        return true;
    }

    // =====================================================
    // TECHNIKER-AUSNAHMEN: DIALOG
    // =====================================================

    private void ProgramUpdateExceptionsButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (!TryResolveCustomerContext(
                out var viewModel,
                out var session,
                out var visit)
            || viewModel.IsScanRunning)
        {
            ShowError(
                "Kundencheckup nicht verfügbar",
                "Die Ausnahmen können keinem "
                + "eindeutigen, gespeicherten "
                + "Kundencheckup zugeordnet werden.");

            return;
        }

        if (DataContext
            is not ProgramUpdateInformation information)
        {
            ShowError(
                "WinGet-Daten nicht verfügbar",
                "Die analysierten Programmupdates "
                + "sind nicht verfügbar.");

            return;
        }

        try
        {
            var dialog =
                new ProgramUpdateExceptionDialog(
                    visit,
                    information)
                {
                    Owner =
                        Window.GetWindow(this)
                };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            /*
             * Nach dem Dialog wird der Kontext
             * erneut kontrolliert.
             *
             * Dadurch wird vermieden, dass eine
             * Entscheidung versehentlich an ein
             * inzwischen anderes Gerät gebunden
             * wird.
             */
            if (!TryResolveCustomerContext(
                    out var currentViewModel,
                    out var currentSession,
                    out var currentVisit)
                || !ReferenceEquals(
                    viewModel,
                    currentViewModel)
                || !ReferenceEquals(
                    session,
                    currentSession)
                || !ReferenceEquals(
                    visit,
                    currentVisit)
                || viewModel.IsScanRunning)
            {
                throw new InvalidOperationException(
                    "Während der Technikerentscheidung "
                    + "hat sich der Kundencheckup-Kontext "
                    + "verändert. Es wurden keine "
                    + "Ausnahmen gespeichert.");
            }

            SaveExceptionDecision(
                viewModel,
                session,
                visit,
                information,
                dialog);
        }
        catch (Exception exception)
        {
            ShowError(
                "Programmupdate-Ausnahme nicht gespeichert",
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Die Technikerentscheidung "
                      + "konnte nicht sicher gespeichert "
                      + "werden."
                    : exception.Message);
        }
        finally
        {
            UpdateExceptionControls();
        }
    }

    // =====================================================
    // TECHNIKER-AUSNAHMEN: SPEICHERUNG
    // =====================================================

    private void SaveExceptionDecision(
        CustomerDevicesViewModel viewModel,
        CheckupSession session,
        CustomerCheckupVisit visit,
        ProgramUpdateInformation information,
        ProgramUpdateExceptionDialog dialog)
    {
        var customer =
            viewModel.SelectedCustomer
            ?? throw new InvalidOperationException(
                "Der ausgewählte Kunde ist "
                + "nicht mehr verfügbar.");

        var device =
            viewModel.SelectedDevice
            ?? throw new InvalidOperationException(
                "Das ausgewählte Gerät ist "
                + "nicht mehr verfügbar.");

        if (!ReferenceEquals(
                device.CheckupSession,
                session)
            || !session.CustomerCheckupVisits.Any(
                storedVisit =>
                    ReferenceEquals(
                        storedVisit,
                        visit))
            || !visit.IsInProgress
            || visit.IsCompletionPrepared)
        {
            throw new InvalidOperationException(
                "Der Kundencheckup kann aktuell "
                + "nicht verändert werden.");
        }

        /*
         * Zuerst wird ausschließlich eine
         * unabhängige Vorgangskopie bearbeitet.
         *
         * Die bisher gespeicherte Entscheidung
         * bleibt bis zum eigentlichen
         * Speicherversuch unverändert.
         */
        var updatedVisit =
            visit.CreateIndependentCopy();

        switch (dialog.SelectedAction)
        {
            case ProgramUpdateExceptionDialogAction.Approve:
                {
                    var update =
                        dialog.SelectedUpdate
                        ?? throw new InvalidOperationException(
                            "Es wurde kein Programmupdate "
                            + "ausgewählt.");

                    if (!information.AvailableUpdates.Any(
                            existingUpdate =>
                                ReferenceEquals(
                                    existingUpdate,
                                    update)))
                    {
                        throw new InvalidOperationException(
                            "Das ausgewählte Programmupdate "
                            + "gehört nicht zur gespeicherten "
                            + "WinGet-Analyse.");
                    }

                    updatedVisit.ApproveProgramUpdateException(
                        update,
                        dialog.Reason,
                        dialog.TechnicianNote);

                    break;
                }

            case ProgramUpdateExceptionDialogAction.Revoke:
                {
                    if (!dialog.ExceptionIdToRevoke.HasValue)
                    {
                        throw new InvalidOperationException(
                            "Die aufzuhebende Ausnahme "
                            + "wurde nicht eindeutig "
                            + "bestimmt.");
                    }

                    var removed =
                        updatedVisit.RevokeProgramUpdateException(
                            dialog.ExceptionIdToRevoke.Value);

                    if (!removed)
                    {
                        throw new InvalidOperationException(
                            "Die ausgewählte Ausnahme "
                            + "ist im aktuellen Kundencheckup "
                            + "nicht mehr vorhanden.");
                    }

                    break;
                }

            default:
                throw new InvalidOperationException(
                    "Es wurde keine gültige "
                    + "Technikerentscheidung übergeben.");
        }

        var previousVisits =
            session.CustomerCheckupVisits;

        var previousUpdatedAt =
            device.UpdatedAt;

        /*
         * Der bisherige Vorgang wird in der
         * Sitzung durch die vorbereitete Kopie
         * ersetzt.
         *
         * Schlägt das anschließende Speichern
         * fehl, wird die ursprüngliche Liste
         * vollständig wiederhergestellt.
         */
        session.CustomerCheckupVisits =
            previousVisits
                .Select(storedVisit =>
                    ReferenceEquals(
                        storedVisit,
                        visit)
                        ? updatedVisit
                        : storedVisit)
                .ToList();

        device.UpdatedAt =
            DateTime.Now;

        try
        {
            var application =
                Application.Current as App
                ?? throw new InvalidOperationException(
                    "Der Anwendungsdienst "
                    + "ist nicht verfügbar.");

            var customerService =
                application.Services
                    .GetRequiredService<ICustomerService>();

            var saved =
                customerService.UpdateCustomerDevice(
                    customer.Id,
                    device);

            if (!saved)
            {
                throw new InvalidOperationException(
                    "Der Kunde oder das Gerät "
                    + "ist in der Datenbank "
                    + "nicht mehr vorhanden.");
            }
        }
        catch
        {
            session.CustomerCheckupVisits =
                previousVisits;

            device.UpdatedAt =
                previousUpdatedAt;

            throw;
        }

        /*
         * Erst ab hier ist die Entscheidung
         * erfolgreich gespeichert.
         */
        var successMessage =
            dialog.SelectedAction
                == ProgramUpdateExceptionDialogAction.Approve
                ? "Die begründete Ausnahme wurde "
                  + "im Kundencheckup gespeichert."
                : "Die dokumentierte Ausnahme "
                  + "wurde aufgehoben und der "
                  + "Kundencheckup gespeichert.";

        MessageBox.Show(
            Window.GetWindow(this),
            successMessage
            + Environment.NewLine
            + Environment.NewLine
            + "Die erkannten Programmupdates "
            + "und der Systemscore bleiben "
            + "vorerst unverändert.",
            "Technikerentscheidung gespeichert",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    // =====================================================
    // BESTEHENDE PROGRAMMUPDATE-AUSWAHL
    // =====================================================

    private void ResolveProgramUpdatesButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext
            is not ProgramUpdateInformation
                programUpdateInformation)
        {
            ShowError(
                "Programmupdates können nicht geöffnet werden",
                "Die zugehörigen WinGet-Analysedaten "
                + "sind nicht verfügbar.");

            return;
        }

        if (!programUpdateInformation
                .IsAnalysisSuccessful
            || programUpdateInformation
                .AvailableUpdateCount <= 0
            || programUpdateInformation
                .AvailableUpdates.Count == 0)
        {
            ShowError(
                "Keine ausführbaren Programmupdates",
                "Für diese Analyse stehen aktuell keine "
                + "zuverlässig erkannten Programmupdates "
                + "zur Behebung bereit.");

            return;
        }

        var checkupSession =
            ResolveCheckupSession(
                programUpdateInformation);

        if (checkupSession is null)
        {
            ShowError(
                "Checkup-Kontext nicht verfügbar",
                "Die Programmaktualisierungen konnten keinem "
                + "eindeutigen Checkup zugeordnet werden.");

            return;
        }

        var task =
            checkupSession
                .TaskList
                .Tasks
                .SingleOrDefault(
                    currentTask =>
                        string.Equals(
                            currentTask.TaskCode,
                            ProgramUpdateTaskCode,
                            StringComparison.Ordinal));

        if (task is null)
        {
            ShowError(
                "Updateaufgabe nicht verfügbar",
                "Die WinGet-Analyse enthält verfügbare "
                + "Programmupdates, die zugehörige "
                + "Checkup-Aufgabe wurde jedoch nicht gefunden."
                + Environment.NewLine
                + Environment.NewLine
                + "Die Updates wurden nicht verändert.");

            return;
        }

        try
        {
            var dialog =
                new ProgramUpdateSelectionDialog(
                    task,
                    programUpdateInformation,
                    checkupSession.TaskList)
                {
                    Owner =
                        Window.GetWindow(
                            this)
                };

            dialog.ShowDialog();
        }
        catch (Exception exception)
        {
            ShowError(
                "Programmupdate-Auswahl konnte nicht geöffnet werden",
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Die Programmupdate-Auswahl konnte "
                      + "nicht sicher vorbereitet werden."
                    : exception.Message);
        }
    }

    private CheckupSession? ResolveCheckupSession(
        ProgramUpdateInformation
            programUpdateInformation)
    {
        /*
         * Gespeicherter Kundencheckup:
         * Die Programmupdates gehören zum
         * aktuell ausgewählten Kundengerät.
         */
        if (TryResolveCustomerContext(
                out _,
                out var customerSession,
                out _)
            && ReferenceEquals(
                customerSession.ProgramUpdateInformation,
                programUpdateInformation))
        {
            return customerSession;
        }

        /*
         * Kundenunabhängiger Diagnosecheckup:
         * Bisherige Auflösung beibehalten.
         */
        DependencyObject? current =
            this;

        while (current is not null)
        {
            current =
                VisualTreeHelper.GetParent(
                    current);

            if (current
                is not FrameworkElement element)
            {
                continue;
            }

            if (element.DataContext
                    is CheckupSession session
                && ReferenceEquals(
                    session.ProgramUpdateInformation,
                    programUpdateInformation))
            {
                return session;
            }

            if (element.DataContext
                    is CheckupViewModel checkupViewModel
                && ReferenceEquals(
                    checkupViewModel
                        .CurrentCheckup
                        .ProgramUpdateInformation,
                    programUpdateInformation))
            {
                return checkupViewModel
                    .CurrentCheckup;
            }
        }

        return null;
    }

    private void ShowError(
        string title,
        string message)
    {
        MessageBox.Show(
            Window.GetWindow(this),
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}

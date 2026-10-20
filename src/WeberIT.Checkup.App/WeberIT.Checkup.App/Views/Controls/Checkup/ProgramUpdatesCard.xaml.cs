
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Assessment;
using WeberIT.Checkup.App.Services.Interfaces;
using WeberIT.Checkup.App.ViewModels;
using WeberIT.Checkup.App.Views.Dialogs;

namespace WeberIT.Checkup.App.Views.Controls.Checkup;

public partial class ProgramUpdatesCard : UserControl
{
    private const string ProgramUpdateTaskCode =
        "task.program-updates.available";

    private const string ExceptionMarkerTag =
        "WeberIT.ProgramUpdateExceptionMarker";

    private const string ExtendedGridTag =
        "WeberIT.ProgramUpdateExceptionExtendedGrid";

    private Button? _exceptionButton;

    private TextBlock? _exceptionStatusText;

    private ItemsControl? _programUpdatesItemsControl;

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

        /*
         * Die ursprüngliche Programmliste
         * aus dem XAML bleibt erhalten.
         */
        _programUpdatesItemsControl =
            contentPanel.Children
                .OfType<ItemsControl>()
                .FirstOrDefault();

        if (_programUpdatesItemsControl is not null)
        {
            _programUpdatesItemsControl
                .ItemContainerGenerator
                .StatusChanged +=
                    ProgramUpdateItemContainers_OnStatusChanged;
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

    // =====================================================
    // LEBENSZYKLUS DER OBERFLÄCHE
    // =====================================================

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

    private void ProgramUpdateItemContainers_OnStatusChanged(
        object? sender,
        EventArgs e)
    {
        if (_programUpdatesItemsControl
                ?.ItemContainerGenerator.Status
            != GeneratorStatus.ContainersGenerated)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            new Action(UpdateExceptionControls),
            DispatcherPriority.Loaded);
    }

    // =====================================================
    // TECHNIKER-AUSNAHMEN: STATUSANZEIGE
    // =====================================================

    private void UpdateExceptionControls()
    {
        if (_exceptionButton is null
            || _exceptionStatusText is null)
        {
            return;
        }

        if (!TryResolveCustomerContext(
                out var viewModel,
                out _,
                out var visit)
            || DataContext
                is not ProgramUpdateInformation information)
        {
            _exceptionButton.Visibility =
                Visibility.Collapsed;

            _exceptionStatusText.Visibility =
                Visibility.Collapsed;

            UpdateProgramUpdateRowMarkers(null);

            return;
        }

        var evaluation =
            ProgramUpdateExceptionEvaluationService
                .Evaluate(
                    information,
                    visit);

        var summary =
            evaluation.SummaryText;

        summary +=
            Environment.NewLine
            + Environment.NewLine
            + "Technischer Systemscore: "
            + "Die ursprüngliche Bewertung "
            + "berücksichtigt weiterhin alle "
            + "tatsächlich ausstehenden Updates. "
            + "Dokumentierte Ausnahmen verändern "
            + "den technischen Score derzeit nicht.";

        if (evaluation.IsAnalysisReliable
            && evaluation.ApplicableExceptionCount > 0)
        {
            summary +=
                Environment.NewLine
                + Environment.NewLine
                + "Begründet zurückgestellte Programme "
                + "sind von der Installationsauswahl "
                + "ausgeschlossen. Nach Aufhebung "
                + "einer Ausnahme kann das betreffende "
                + "Programm wieder ausgewählt werden.";
        }

        if (visit.IsCompletionPrepared)
        {
            summary +=
                Environment.NewLine
                + Environment.NewLine
                + "Der Abschlussentwurf wurde bereits "
                + "vorbereitet. Technikerentscheidungen "
                + "können in diesem Zustand "
                + "nicht mehr verändert werden.";
        }

        _exceptionStatusText.Text =
            summary;

        _exceptionStatusText.Visibility =
            Visibility.Visible;

        var canEditExceptions =
            !viewModel.IsScanRunning
            && visit.IsInProgress
            && !visit.IsCompletionPrepared
            && evaluation.IsAnalysisReliable
            && information.AvailableUpdateCount > 0
            && information.AvailableUpdates.Count > 0;

        _exceptionButton.Visibility =
            canEditExceptions
                ? Visibility.Visible
                : Visibility.Collapsed;

        UpdateProgramUpdateRowMarkers(
            evaluation.IsAnalysisReliable
                ? visit
                : null);
    }

    // =====================================================
    // SCHRITT 28: PROGRAMME DIREKT KENNZEICHNEN
    // =====================================================

    /// <summary>
    /// Ergänzt die tatsächlich sichtbaren
    /// Programmzeilen um eine Kennzeichnung
    /// bei gültigen Technikerentscheidungen.
    ///
    /// Die ursprünglichen Programmdaten,
    /// Befunde und Scores bleiben unverändert.
    /// </summary>
    private void UpdateProgramUpdateRowMarkers(
        CustomerCheckupVisit? visit)
    {
        if (_programUpdatesItemsControl is null)
        {
            return;
        }

        foreach (var item
                 in _programUpdatesItemsControl.Items)
        {
            if (item is not AvailableProgramUpdate update)
            {
                continue;
            }

            var container =
                _programUpdatesItemsControl
                    .ItemContainerGenerator
                    .ContainerFromItem(update);

            if (container is null)
            {
                continue;
            }

            if (container is FrameworkElement element)
            {
                element.ApplyTemplate();
            }

            var updateGrid =
                FindUpdateEntryGrid(container);

            if (updateGrid is null)
            {
                continue;
            }

            ProgramUpdateException? matchingException =
                null;

            if (visit is not null)
            {
                matchingException =
                    visit.ProgramUpdateExceptions
                        .Where(approval =>
                            approval is not null
                            && approval.AppliesTo(
                                visit.Id,
                                update))
                        .OrderByDescending(
                            approval =>
                                approval.ApprovedAt)
                        .FirstOrDefault();
            }

            UpdateSingleRowMarker(
                updateGrid,
                matchingException);
        }
    }

    /// <summary>
    /// Findet das ursprüngliche Fünf-Spalten-Grid
    /// einer Programmzeile.
    ///
    /// Die fünf Spalten enthalten:
    /// Programmname, Paket-ID, installierte
    /// Version, Pfeil und verfügbare Version.
    /// </summary>
    private static Grid? FindUpdateEntryGrid(
        DependencyObject root)
    {
        if (root is Grid grid
            && grid.ColumnDefinitions.Count == 5)
        {
            return grid;
        }

        var childrenCount =
            VisualTreeHelper.GetChildrenCount(root);

        for (var index = 0;
             index < childrenCount;
             index++)
        {
            var child =
                VisualTreeHelper.GetChild(
                    root,
                    index);

            var matchingGrid =
                FindUpdateEntryGrid(child);

            if (matchingGrid is not null)
            {
                return matchingGrid;
            }
        }

        return null;
    }

    /// <summary>
    /// Fügt die Kennzeichnung hinzu oder
    /// entfernt sie nach dem Widerruf.
    /// </summary>
    private static void UpdateSingleRowMarker(
        Grid updateGrid,
        ProgramUpdateException? approval)
    {
        var existingMarker =
            updateGrid.Children
                .OfType<Border>()
                .FirstOrDefault(border =>
                    string.Equals(
                        border.Tag as string,
                        ExceptionMarkerTag,
                        StringComparison.Ordinal));

        if (existingMarker is not null)
        {
            updateGrid.Children.Remove(
                existingMarker);
        }

        if (approval is null)
        {
            if (string.Equals(
                    updateGrid.Tag as string,
                    ExtendedGridTag,
                    StringComparison.Ordinal))
            {
                updateGrid.RowDefinitions.Clear();

                updateGrid.Tag = null;
            }

            return;
        }

        /*
         * Die vorhandenen fünf Spalten
         * bleiben unverändert.
         *
         * Die Begründung wird als zweite
         * Zeile darunter angezeigt.
         */
        if (!string.Equals(
                updateGrid.Tag as string,
                ExtendedGridTag,
                StringComparison.Ordinal))
        {
            if (updateGrid.RowDefinitions.Count != 0)
            {
                return;
            }

            updateGrid.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });

            updateGrid.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });

            updateGrid.Tag =
                ExtendedGridTag;
        }

        var markerBackground =
            new SolidColorBrush(
                Color.FromRgb(45, 44, 44));

        var markerBorder =
            new SolidColorBrush(
                Color.FromRgb(158, 112, 44));

        var markerAccent =
            new SolidColorBrush(
                Color.FromRgb(255, 194, 99));

        var markerText =
            new SolidColorBrush(
                Color.FromRgb(225, 231, 240));

        var markerPanel = new StackPanel();

        var markerTitle = new TextBlock
        {
            Text =
                "Begründet zurückgestellt "
                + "· Aktualisierung weiterhin ausstehend",

            FontWeight =
                FontWeights.SemiBold,

            FontSize = 12,

            Foreground = markerAccent,

            TextWrapping =
                TextWrapping.Wrap
        };

        markerPanel.Children.Add(
            markerTitle);

        var reasonText = new TextBlock
        {
            Text =
                "Begründung: "
                + approval.Reason,

            Margin =
                new Thickness(0, 5, 0, 0),

            FontSize = 11,

            Foreground = markerText,

            TextWrapping = TextWrapping.Wrap
        };

        markerPanel.Children.Add(
            reasonText);

        if (!string.IsNullOrWhiteSpace(
                approval.TechnicianNote))
        {
            var noteText = new TextBlock
            {
                Text =
                    "Technikernotiz: "
                    + approval.TechnicianNote,

                Margin =
                    new Thickness(0, 4, 0, 0),

                FontSize = 11,

                Foreground = markerText,

                TextWrapping =
                    TextWrapping.Wrap
            };

            markerPanel.Children.Add(
                noteText);
        }

        var marker = new Border
        {
            Tag = ExceptionMarkerTag,

            Margin =
                new Thickness(0, 12, 0, 0),

            Padding =
                new Thickness(12, 9, 12, 9),

            Background = markerBackground,

            BorderBrush = markerBorder,

            BorderThickness =
                new Thickness(1),

            CornerRadius =
                new CornerRadius(6),

            Child = markerPanel
        };

        Grid.SetRow(marker, 1);

        Grid.SetColumn(marker, 0);

        Grid.SetColumnSpan(marker, 5);

        updateGrid.Children.Add(
            marker);
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
         * Die WinGet-Karte wird auch beim
         * allgemeinen Checkup verwendet.
         *
         * Ausnahmen dürfen nur einem
         * gespeicherten Kundenvorgang
         * zugeordnet werden.
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
             * Nach der Entscheidung prüfen wir,
             * ob derselbe Kundenvorgang
             * weiterhin aktiv ist.
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
         * Die neue Entscheidung wird nur auf
         * einer unabhängigen Kopie verändert.
         *
         * Beim fehlgeschlagenen Speichern
         * wird der ursprüngliche Zustand
         * wiederhergestellt.
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
            + "und der technische Systemscore "
            + "bleiben unverändert.",
            "Technikerentscheidung gespeichert",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    // =====================================================
    // SCHRITT 29: PROGRAMMUPDATE-INSTALLATIONSAUSWAHL
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

        if (programUpdateInformation.IsWingetAvailable != true
            || !programUpdateInformation.IsAnalysisPerformed
            || !programUpdateInformation.IsAnalysisSuccessful
            || programUpdateInformation.AvailableUpdateCount <= 0
            || programUpdateInformation.AvailableUpdates.Count == 0)
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
            /*
             * Außerhalb eines Kundencheckups
             * bleibt die bisherige allgemeine
             * Programmupdate-Auswahl erhalten.
             *
             * Innerhalb eines Kundencheckups
             * verwenden wir ausschließlich eine
             * separat gefilterte Arbeitsliste.
             */
            var selectionInformation =
                programUpdateInformation;

            if (Window.GetWindow(this)?.DataContext
                is CustomerDevicesViewModel)
            {
                /*
                 * Bei einem Kundengerät gilt:
                 * Ohne eindeutig erkannten,
                 * bearbeitbaren Vorgang wird
                 * kein Installationsplan erstellt.
                 */
                if (!TryResolveCustomerContext(
                        out var viewModel,
                        out var customerSession,
                        out var visit)
                    || !ReferenceEquals(
                        checkupSession,
                        customerSession)
                    || viewModel.IsScanRunning
                    || !visit.IsInProgress
                    || visit.IsCompletionPrepared)
                {
                    ShowError(
                        "Installationsauswahl nicht verfügbar",
                        "Die Programmaktualisierungen "
                        + "können keinem eindeutig "
                        + "bearbeitbaren Kundencheckup "
                        + "zugeordnet werden."
                        + Environment.NewLine
                        + Environment.NewLine
                        + "Es wurde kein Installationsplan "
                        + "erstellt.");

                    return;
                }

                selectionInformation =
                    CreateInstallationSelectionInformation(
                        programUpdateInformation,
                        visit);

                if (selectionInformation
                        .AvailableUpdates.Count == 0)
                {
                    ShowError(
                        "Keine Programmupdates zur Auswahl",
                        "Alle in der gespeicherten "
                        + "Programmliste enthaltenen "
                        + "Aktualisierungen wurden "
                        + "begründet zurückgestellt."
                        + Environment.NewLine
                        + Environment.NewLine
                        + "Um eines dieser Programme "
                        + "zu aktualisieren, muss "
                        + "zunächst die zugehörige "
                        + "Technikerentscheidung "
                        + "aufgehoben werden.");

                    return;
                }
            }

            /*
             * Dialog und Plan-Builder erhalten
             * dieselbe zulässige Arbeitsliste.
             *
             * Dadurch können zurückgestellte
             * Programme weder durch
             * "Alle auswählen" noch durch
             * die anschließende Planerstellung
             * in den Wartungslauf gelangen.
             */
            var dialog =
                new ProgramUpdateSelectionDialog(
                    task,
                    selectionInformation,
                    checkupSession.TaskList)
                {
                    Owner =
                        Window.GetWindow(this)
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

    // =====================================================
    // SCHRITT 29: GEFILTERTE INSTALLATIONSARBEITSLISTE
    // =====================================================

    /// <summary>
    /// Erstellt eine unabhängige Arbeitsliste
    /// der für die Installation zulässigen
    /// Programmaktualisierungen.
    ///
    /// Eine Ausnahme wird nur berücksichtigt,
    /// wenn sie zur aktuellen Vorgangs-ID,
    /// Paket-ID, Paketquelle und den exakten
    /// Versionsständen passt.
    ///
    /// Der ursprüngliche WinGet-Scan bleibt
    /// vollständig unverändert.
    /// </summary>
    private static ProgramUpdateInformation
        CreateInstallationSelectionInformation(
            ProgramUpdateInformation information,
            CustomerCheckupVisit visit)
    {
        ArgumentNullException.ThrowIfNull(information);
        ArgumentNullException.ThrowIfNull(visit);

        if (!visit.IsInProgress
            || visit.IsCompletionPrepared)
        {
            throw new InvalidOperationException(
                "Für diesen Kundencheckup dürfen "
                + "aktuell keine Installationspläne "
                + "vorbereitet werden.");
        }

        if (information.IsWingetAvailable != true
            || !information.IsAnalysisPerformed
            || !information.IsAnalysisSuccessful)
        {
            throw new InvalidOperationException(
                "Für die Installation liegt keine "
                + "erfolgreiche WinGet-Analyse vor.");
        }

        /*
         * Die aktuelle Auswertung wird auch
         * hier zur Validierung verwendet.
         */
        var evaluation =
            ProgramUpdateExceptionEvaluationService
                .Evaluate(
                    information,
                    visit);

        if (!evaluation.IsAnalysisReliable)
        {
            throw new InvalidOperationException(
                "Der WinGet-Status ist nicht "
                + "zuverlässig auswertbar.");
        }

        /*
         * Zunächst die zur aktuellen
         * Vorgangs-ID gehörenden Ausnahmen
         * bestimmen.
         *
         * Fremde und historische Vorgänge
         * dürfen sich nicht auf die aktuelle
         * Installationsauswahl auswirken.
         */
        var applicableExceptions =
            visit.ProgramUpdateExceptions
                .Where(approval =>
                    approval is not null
                    && approval.CheckupVisitId == visit.Id)
                .ToList();

        /*
         * Für jedes Paket prüfen wir über
         * AppliesTo die vollständige Identität
         * einschließlich Versionsständen.
         *
         * Eine Ausnahme für eine alte Version
         * sperrt keine neue Aktualisierung.
         */
        var selectableUpdates =
            information.AvailableUpdates
                .Where(update =>
                    update is not null
                    && !applicableExceptions.Any(
                        approval =>
                            approval.AppliesTo(
                                visit.Id,
                                update)))
                .Select(update =>
                    new AvailableProgramUpdate
                    {
                        PackageId = update.PackageId,

                        Name = update.Name,

                        InstalledVersion =
                            update.InstalledVersion,

                        AvailableVersion =
                            update.AvailableVersion,

                        Source = update.Source
                    })
                .ToList();

        /*
         * Ausschließlich diese neue
         * Programmliste wird an den
         * Installationsdialog übergeben.
         *
         * Auch die Anzahl wird für diese
         * Arbeitskopie angepasst.
         *
         * Die gespeicherte Originalanalyse
         * behält weiterhin ihre tatsächliche
         * Anzahl erkannter Updates.
         */
        return new ProgramUpdateInformation
        {
            IsWingetAvailable =
                information.IsWingetAvailable,

            WingetVersion =
                information.WingetVersion,

            IsAnalysisPerformed =
                information.IsAnalysisPerformed,

            IsAnalysisSuccessful =
                information.IsAnalysisSuccessful,

            AnalysisDate =
                information.AnalysisDate,

            AnalysisDetails =
                information.AnalysisDetails,

            AvailableUpdateCount =
                selectableUpdates.Count,

            IsResultTruncated =
                information.IsResultTruncated,

            AvailableUpdates =
                selectableUpdates
        };
    }

    // =====================================================
    // CHECKUP-KONTEXT AUFLÖSEN
    // =====================================================

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

    // =====================================================
    // FEHLERMELDUNGEN
    // =====================================================

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
